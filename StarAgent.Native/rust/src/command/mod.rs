//! Command execution: deduplication, expiry, delay, whitelist, timeout and
//! output limits.

pub mod output;
pub mod platform;

use std::collections::HashMap;
use std::sync::Arc;
use std::time::{Duration, Instant};

use anyhow::{bail, Result};
use chrono::{DateTime, Utc};
use futures_util::future::BoxFuture;
use serde::Deserialize;
use tokio::sync::{Mutex, Semaphore};
use tokio_util::sync::CancellationToken;
use tracing::{info, warn};

use crate::protocol::{
    CommandModel, CommandReplyModel, STATUS_CANCELLED, STATUS_COMPLETED, STATUS_ERROR,
    STATUS_PROCESSING,
};
use output::truncate_output;

/// Maximum accepted argument size (shell scripts included).
pub const MAX_ARGUMENT_SIZE: usize = 8 * 1024;
/// Deduplication window.
pub const DEDUP_TTL: Duration = Duration::from_secs(3600);
/// Maximum number of concurrent commands.
pub const MAX_CONCURRENCY: usize = 4;
/// Default command timeout.
pub const DEFAULT_TIMEOUT: Duration = Duration::from_secs(30);
/// Maximum command timeout.
pub const MAX_TIMEOUT: Duration = Duration::from_secs(300);

/// Reply callback used by the executor.
pub type ReplyFn = Arc<dyn Fn(CommandReplyModel) -> BoxFuture<'static, ()> + Send + Sync>;

/// Executor configuration.
pub struct ExecutorOptions {
    /// Enables `bash` / `cmd`.
    pub allow_exec: bool,
    /// Command whitelist; empty allows all built-in commands.
    pub allowed_commands: Vec<String>,
    /// Default timeout, clamped to 1..=300 seconds.
    pub timeout: Duration,
    /// Returns the current server time for `node/synctime`.
    pub server_time: Arc<dyn Fn() -> DateTime<Utc> + Send + Sync>,
    /// Persists the update channel for `node/setchannel`.
    pub set_channel: Arc<dyn Fn(&str) -> Result<()> + Send + Sync>,
    /// Agent cancellation token.
    pub cancel: CancellationToken,
}

/// Executes remote commands according to the protocol rules.
pub struct Executor {
    opts: ExecutorOptions,
    seen: std::sync::Mutex<HashMap<i64, Instant>>,
    permits: Arc<Semaphore>,
    shell_lock: Mutex<()>,
}

impl Executor {
    /// Creates an executor.
    pub fn new(mut opts: ExecutorOptions) -> Arc<Self> {
        if opts.timeout < Duration::from_secs(1) {
            opts.timeout = DEFAULT_TIMEOUT;
        }
        if opts.timeout > MAX_TIMEOUT {
            opts.timeout = MAX_TIMEOUT;
        }
        Arc::new(Self {
            opts,
            seen: std::sync::Mutex::new(HashMap::new()),
            permits: Arc::new(Semaphore::new(MAX_CONCURRENCY)),
            shell_lock: Mutex::new(()),
        })
    }

    /// Handles a command in a background task.
    pub fn spawn(self: &Arc<Self>, command: CommandModel, reply: ReplyFn) {
        let executor = self.clone();
        tokio::spawn(async move {
            executor.handle(command, reply).await;
        });
    }

    /// Handles a command, replying once or twice (delayed commands).
    pub async fn handle(self: &Arc<Self>, mut command: CommandModel, reply: ReplyFn) {
        command.command = command.command.trim().to_string();

        if command.id != 0 && self.is_duplicate(command.id) {
            info!(
                id = command.id,
                command = %command.command,
                "duplicate command ignored"
            );
            return;
        }

        let now = Utc::now();
        if command.expire.is_set() && command.expire.is_before(now) {
            self.audit(&command, STATUS_CANCELLED, "expired");
            reply(CommandReplyModel {
                id: command.id,
                status: STATUS_CANCELLED,
                data: Some("命令已过期".to_string()),
            })
            .await;
            return;
        }

        if command.start_time.is_set() && command.start_time.is_after(now) {
            self.audit(&command, STATUS_PROCESSING, "delayed");
            reply(CommandReplyModel {
                id: command.id,
                status: STATUS_PROCESSING,
                data: Some("已安排执行".to_string()),
            })
            .await;
            let executor = self.clone();
            tokio::spawn(async move {
                executor.run_delayed(command, reply).await;
            });
            return;
        }

        let result = self.execute(&command).await;
        reply(result).await;
    }

    async fn run_delayed(self: Arc<Self>, command: CommandModel, reply: ReplyFn) {
        let target = match command.start_time.value() {
            Some(value) => value,
            None => return,
        };

        loop {
            let now = Utc::now();
            if now >= target {
                break;
            }
            let remaining = (target - now)
                .to_std()
                .unwrap_or_else(|_| Duration::from_secs(0));
            let wait = remaining.min(Duration::from_secs(3600));
            tokio::select! {
                _ = self.opts.cancel.cancelled() => {
                    self.audit(&command, STATUS_CANCELLED, "canceled while waiting");
                    reply(CommandReplyModel {
                        id: command.id,
                        status: STATUS_CANCELLED,
                        data: Some("Agent 已停止，延迟命令取消".to_string()),
                    }).await;
                    return;
                }
                _ = tokio::time::sleep(wait) => {}
            }
        }

        if command.expire.is_set() && command.expire.is_before(Utc::now()) {
            self.audit(&command, STATUS_CANCELLED, "expired before start");
            reply(CommandReplyModel {
                id: command.id,
                status: STATUS_CANCELLED,
                data: Some("命令已过期".to_string()),
            })
            .await;
            return;
        }

        let result = self.execute(&command).await;
        reply(result).await;
    }

    async fn execute(self: &Arc<Self>, command: &CommandModel) -> CommandReplyModel {
        let started = Instant::now();
        let permit = tokio::select! {
            result = self.permits.clone().acquire_owned() => match result {
                Ok(permit) => permit,
                Err(_) => return reply(STATUS_ERROR, "命令执行器已关闭"),
            },
            _ = self.opts.cancel.cancelled() => return reply(STATUS_CANCELLED, "Agent 已停止"),
        };

        let mut result = self.dispatch(command).await;
        drop(permit);
        result.id = command.id;
        result.data = Some(truncate_output(result.data.as_deref().unwrap_or_default()));
        self.audit_duration(command, result.status, started.elapsed());

        result
    }

    async fn dispatch(self: &Arc<Self>, command: &CommandModel) -> CommandReplyModel {
        let name = command.command.as_str();
        if !self.is_allowed(name) {
            return reply(STATUS_CANCELLED, &format!("命令不在白名单: {name}"));
        }

        match name.to_ascii_lowercase().as_str() {
            "bash" => self.run_shell(command, "bash").await,
            "cmd" => self.run_shell(command, "cmd").await,
            "node/synctime" => self.sync_time(command),
            "node/setchannel" => self.set_channel(command),
            "node/restart" => self.restart(),
            "node/reboot" | "framework/install" => unsupported(name),
            other if other.starts_with("deploy/") => unsupported(name),
            _ => unsupported(name),
        }
    }

    async fn run_shell(&self, command: &CommandModel, shell: &str) -> CommandReplyModel {
        if !self.opts.allow_exec {
            return reply(STATUS_CANCELLED, "命令执行未启用");
        }

        let supported = if cfg!(windows) {
            shell == "cmd"
        } else {
            shell == "bash"
        };
        if !supported {
            return unsupported(&command.command);
        }

        let argument = command.argument.as_deref().unwrap_or_default();
        if argument.len() > MAX_ARGUMENT_SIZE {
            return reply(
                STATUS_ERROR,
                &format!(
                    "命令参数过长（{} 字节，上限 {}）",
                    argument.len(),
                    MAX_ARGUMENT_SIZE
                ),
            );
        }

        let (script, timeout) = match self.parse_shell_argument(argument) {
            Ok(value) => value,
            Err(error) => return reply(STATUS_ERROR, &error.to_string()),
        };

        // Shell commands run serially so a single agent cannot exhaust the host.
        let _guard = self.shell_lock.lock().await;
        let run = platform::run_shell(shell, &script, timeout, &self.opts.cancel).await;
        match run {
            Err(error) => reply(STATUS_ERROR, &format!("执行失败: {error}")),
            Ok(run) if run.cancelled => {
                reply(STATUS_CANCELLED, &format!("命令执行已取消\n{}", run.output))
            }
            Ok(run) if run.timed_out => reply(
                STATUS_ERROR,
                &format!("命令执行超时（{}s）\n{}", timeout.as_secs(), run.output),
            ),
            Ok(run) if run.exit_code != 0 => reply(
                STATUS_ERROR,
                &format!("{}\n(exit code: {})", run.output, run.exit_code),
            ),
            Ok(run) => {
                if run.output.trim().is_empty() {
                    reply(STATUS_COMPLETED, "命令执行成功")
                } else {
                    reply(STATUS_COMPLETED, &run.output)
                }
            }
        }
    }

    fn parse_shell_argument(&self, argument: &str) -> Result<(String, Duration)> {
        let mut script = argument.trim().to_string();
        let mut timeout = self.opts.timeout;

        if script.starts_with('{') {
            if let Ok(payload) = serde_json::from_str::<ShellPayload>(&script) {
                if !payload.cmd.trim().is_empty() {
                    script = payload.cmd;
                }
                if payload.timeout > 0 {
                    timeout = Duration::from_millis(payload.timeout as u64);
                }
            }
        }

        let script = script.trim().to_string();
        if script.is_empty() {
            bail!("命令为空");
        }
        if script.len() > MAX_ARGUMENT_SIZE {
            bail!(
                "命令过长（{} 字节，上限 {}）",
                script.len(),
                MAX_ARGUMENT_SIZE
            );
        }

        if timeout <= Duration::from_millis(0) {
            timeout = DEFAULT_TIMEOUT;
        }
        if timeout > MAX_TIMEOUT {
            timeout = MAX_TIMEOUT;
        }

        Ok((script, timeout))
    }

    fn sync_time(&self, command: &CommandModel) -> CommandReplyModel {
        let now = Utc::now();
        let mut target = None;
        if let Some(text) = command.argument.as_deref().map(str::trim) {
            if !text.is_empty() {
                match crate::protocol::parse_time_text(text) {
                    Ok(value) => target = value,
                    Err(_) => return reply(STATUS_ERROR, &format!("时间格式无效: {text}")),
                }
            }
        }
        let target = target.unwrap_or_else(|| (self.opts.server_time)());
        let diff = target - now;

        if diff.num_milliseconds().abs() < 1000 {
            return reply(STATUS_COMPLETED, "无需同步");
        }

        match platform::sync_time(target) {
            Ok(()) => reply(
                STATUS_COMPLETED,
                &format!(
                    "同步时间为 {}，偏差 {}ms",
                    target.format("%Y-%m-%d %H:%M:%S"),
                    diff.num_milliseconds()
                ),
            ),
            Err(error) => reply(STATUS_ERROR, &format!("同步时间失败: {error}")),
        }
    }

    fn set_channel(&self, command: &CommandModel) -> CommandReplyModel {
        let channel = command.argument.as_deref().unwrap_or_default().trim();
        if channel.is_empty() {
            return reply(STATUS_ERROR, "参数为空");
        }
        if !valid_channel(channel) {
            return reply(
                STATUS_ERROR,
                "通道名称无效（仅允许字母、数字、点、下划线、短横线，最长 64）",
            );
        }

        match (self.opts.set_channel)(channel) {
            Ok(()) => reply(STATUS_COMPLETED, &format!("success {channel}")),
            Err(error) => reply(STATUS_ERROR, &format!("保存通道失败: {error}")),
        }
    }

    fn restart(&self) -> CommandReplyModel {
        let cancel = self.opts.cancel.clone();
        tokio::spawn(async move {
            tokio::select! {
                _ = cancel.cancelled() => return,
                _ = tokio::time::sleep(Duration::from_secs(1)) => {}
            }
            if let Err(error) = platform::restart_process() {
                warn!(error = %error, "restart process failed");
            }
        });

        reply(STATUS_COMPLETED, "正在重启")
    }

    fn is_allowed(&self, name: &str) -> bool {
        if self.opts.allowed_commands.is_empty() {
            return true;
        }

        let name = name.to_ascii_lowercase();
        self.opts.allowed_commands.iter().any(|pattern| {
            let pattern = pattern.trim().to_ascii_lowercase();
            if pattern.is_empty() {
                return false;
            }
            if pattern == name {
                return true;
            }
            match pattern.strip_suffix('*') {
                Some(prefix) => name.starts_with(prefix),
                None => false,
            }
        })
    }

    fn is_duplicate(&self, id: i64) -> bool {
        let now = Instant::now();
        let mut seen = self
            .seen
            .lock()
            .unwrap_or_else(|poison| poison.into_inner());
        seen.retain(|_, at| now.duration_since(*at) <= DEDUP_TTL);
        if seen.contains_key(&id) {
            return true;
        }
        seen.insert(id, now);
        false
    }

    fn audit(&self, command: &CommandModel, status: i32, note: &str) {
        info!(
            id = command.id,
            command = %command.command,
            status,
            note,
            argument_bytes = command.argument.as_deref().map(str::len).unwrap_or(0),
            "command audit"
        );
    }

    fn audit_duration(&self, command: &CommandModel, status: i32, elapsed: Duration) {
        info!(
            id = command.id,
            command = %command.command,
            status,
            duration_ms = elapsed.as_millis(),
            argument_bytes = command.argument.as_deref().map(str::len).unwrap_or(0),
            "command finished"
        );
    }
}

#[derive(Deserialize)]
struct ShellPayload {
    #[serde(default, alias = "Cmd")]
    cmd: String,
    #[serde(default, alias = "Timeout")]
    timeout: i64,
}

fn valid_channel(channel: &str) -> bool {
    if channel.is_empty() || channel.len() > 64 {
        return false;
    }
    channel
        .chars()
        .all(|value| value.is_ascii_alphanumeric() || matches!(value, '.' | '_' | '-'))
}

fn reply(status: i32, data: &str) -> CommandReplyModel {
    CommandReplyModel {
        id: 0,
        status,
        data: Some(data.to_string()),
    }
}

fn unsupported(name: &str) -> CommandReplyModel {
    reply(STATUS_ERROR, &format!("不支持命令: {name}"))
}

/// Creates a reply collector for tests.
#[cfg(test)]
fn collector() -> (ReplyFn, Arc<std::sync::Mutex<Vec<CommandReplyModel>>>) {
    let replies = Arc::new(std::sync::Mutex::new(Vec::new()));
    let sink = replies.clone();
    let reply: ReplyFn = Arc::new(move |value| {
        let sink = sink.clone();
        Box::pin(async move {
            sink.lock().unwrap().push(value);
        })
    });
    (reply, replies)
}

#[cfg(test)]
fn test_executor(allow_exec: bool, allowed_commands: Vec<String>) -> Arc<Executor> {
    Executor::new(ExecutorOptions {
        allow_exec,
        allowed_commands,
        timeout: DEFAULT_TIMEOUT,
        server_time: Arc::new(Utc::now),
        set_channel: Arc::new(|_| Ok(())),
        cancel: CancellationToken::new(),
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::protocol::ProtoTime;

    #[tokio::test]
    async fn deduplicates_command_ids() {
        let executor = test_executor(false, Vec::new());
        let (reply, replies) = collector();
        let command = CommandModel {
            id: 11,
            command: "node/synctime".into(),
            ..Default::default()
        };

        executor.handle(command.clone(), reply.clone()).await;
        executor.handle(command, reply).await;

        let replies = replies.lock().unwrap();
        assert_eq!(replies.len(), 1);
        assert_eq!(replies[0].status, STATUS_COMPLETED);
    }

    #[tokio::test]
    async fn rejects_expired_commands() {
        let executor = test_executor(false, Vec::new());
        let (reply, replies) = collector();
        let command = CommandModel {
            id: 12,
            command: "node/synctime".into(),
            expire: ProtoTime::new(Utc::now() - chrono::Duration::minutes(1)),
            ..Default::default()
        };

        executor.handle(command, reply).await;
        let replies = replies.lock().unwrap();
        assert_eq!(replies[0].status, STATUS_CANCELLED);
        assert!(replies[0]
            .data
            .as_deref()
            .unwrap_or_default()
            .contains("过期"));
    }

    #[tokio::test]
    async fn schedules_delayed_commands() {
        let executor = test_executor(false, Vec::new());
        let (reply, replies) = collector();
        let command = CommandModel {
            id: 13,
            command: "node/synctime".into(),
            start_time: ProtoTime::new(Utc::now() + chrono::Duration::milliseconds(120)),
            ..Default::default()
        };

        executor.handle(command, reply).await;
        assert_eq!(replies.lock().unwrap()[0].status, STATUS_PROCESSING);

        tokio::time::sleep(Duration::from_millis(400)).await;
        let replies = replies.lock().unwrap();
        assert_eq!(replies.len(), 2);
        assert_eq!(replies[1].status, STATUS_COMPLETED);
    }

    #[tokio::test]
    async fn shell_commands_are_disabled_by_default() {
        let executor = test_executor(false, Vec::new());
        let (reply, replies) = collector();
        let shell = if cfg!(windows) { "cmd" } else { "bash" };
        let command = CommandModel {
            id: 14,
            command: shell.into(),
            argument: Some("echo hello".into()),
            ..Default::default()
        };

        executor.handle(command, reply).await;
        let replies = replies.lock().unwrap();
        assert_eq!(replies[0].status, STATUS_CANCELLED);
        assert!(replies[0]
            .data
            .as_deref()
            .unwrap_or_default()
            .contains("未启用"));
    }

    #[tokio::test]
    async fn unsupported_commands_are_reported() {
        let executor = test_executor(false, Vec::new());
        let (reply, replies) = collector();
        let command = CommandModel {
            id: 15,
            command: "node/reboot".into(),
            ..Default::default()
        };

        executor.handle(command, reply).await;
        let replies = replies.lock().unwrap();
        assert_eq!(replies[0].status, STATUS_ERROR);
        assert!(replies[0]
            .data
            .as_deref()
            .unwrap_or_default()
            .starts_with("不支持命令"));
    }

    #[tokio::test]
    async fn whitelist_blocks_commands() {
        let executor = test_executor(false, vec!["node/synctime".into()]);
        let (reply, replies) = collector();
        let command = CommandModel {
            id: 16,
            command: "node/reboot".into(),
            ..Default::default()
        };

        executor.handle(command, reply).await;
        let replies = replies.lock().unwrap();
        assert_eq!(replies[0].status, STATUS_CANCELLED);
        assert!(replies[0]
            .data
            .as_deref()
            .unwrap_or_default()
            .contains("白名单"));
    }

    #[test]
    fn parses_shell_argument_forms() {
        let executor = test_executor(true, Vec::new());
        let (script, timeout) = executor.parse_shell_argument("  echo hi  ").unwrap();
        assert_eq!(script, "echo hi");
        assert_eq!(timeout, DEFAULT_TIMEOUT);

        let (script, timeout) = executor
            .parse_shell_argument(r#"{"cmd":"echo hi","timeout":1500}"#)
            .unwrap();
        assert_eq!(script, "echo hi");
        assert_eq!(timeout, Duration::from_millis(1500));

        let (script, timeout) = executor
            .parse_shell_argument(r#"{"cmd":"echo hi","timeout":999999}"#)
            .unwrap();
        assert_eq!(script, "echo hi");
        assert_eq!(timeout, MAX_TIMEOUT);

        assert!(executor.parse_shell_argument("  ").is_err());
        let long = "a".repeat(MAX_ARGUMENT_SIZE + 1);
        assert!(executor.parse_shell_argument(&long).is_err());
    }

    #[test]
    fn channel_validation() {
        assert!(valid_channel("Release"));
        assert!(valid_channel("beta_1.2-3"));
        assert!(!valid_channel(""));
        assert!(!valid_channel("bad/channel"));
        assert!(!valid_channel(&"a".repeat(65)));
    }

    #[cfg(windows)]
    #[tokio::test]
    async fn executes_cmd_when_enabled() {
        let executor = test_executor(true, Vec::new());
        let (reply, replies) = collector();
        let command = CommandModel {
            id: 17,
            command: "cmd".into(),
            argument: Some(r#"{"cmd":"echo hello-rust","timeout":10000}"#.into()),
            ..Default::default()
        };

        executor.handle(command, reply).await;
        let replies = replies.lock().unwrap();
        assert_eq!(replies[0].status, STATUS_COMPLETED);
        assert!(replies[0]
            .data
            .as_deref()
            .unwrap_or_default()
            .contains("hello-rust"));
    }

    #[cfg(unix)]
    #[tokio::test]
    async fn executes_bash_when_enabled() {
        let executor = test_executor(true, Vec::new());
        let (reply, replies) = collector();
        let command = CommandModel {
            id: 18,
            command: "bash".into(),
            argument: Some(r#"{"cmd":"echo hello-rust","timeout":10000}"#.into()),
            ..Default::default()
        };

        executor.handle(command, reply).await;
        let replies = replies.lock().unwrap();
        assert_eq!(replies[0].status, STATUS_COMPLETED);
        assert!(replies[0]
            .data
            .as_deref()
            .unwrap_or_default()
            .contains("hello-rust"));
    }
}
