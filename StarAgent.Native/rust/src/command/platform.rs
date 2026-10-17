//! Platform-specific shell execution, time sync and restart helpers.

use std::time::Duration;

use anyhow::Result;
use chrono::{DateTime, Utc};
use tokio_util::sync::CancellationToken;

/// Result of one shell execution.
#[derive(Debug)]
pub struct ShellRun {
    /// Combined standard output and standard error.
    pub output: String,
    /// Exit code, or -1 when the process was killed.
    pub exit_code: i32,
    /// True when the timeout elapsed.
    pub timed_out: bool,
    /// True when cancellation killed the process.
    pub cancelled: bool,
}

/// Executes a shell script, merging stdout and stderr and killing the process
/// tree on timeout or cancellation.
pub async fn run_shell(
    shell: &str,
    script: &str,
    timeout: Duration,
    cancel: &CancellationToken,
) -> Result<ShellRun> {
    imp::run_shell(shell, script, timeout, cancel).await
}

/// Sets the system clock.
pub fn sync_time(target: DateTime<Utc>) -> Result<()> {
    imp::sync_time(target)
}

/// Starts a new instance of this executable and exits the current process.
pub fn restart_process() -> Result<()> {
    imp::restart_process()
}

#[cfg(unix)]
mod imp {
    use std::time::Duration;

    use anyhow::{bail, Context, Result};
    use chrono::{DateTime, Utc};
    use std::os::unix::process::CommandExt;
    use tokio::io::AsyncReadExt;
    use tokio_util::sync::CancellationToken;

    use super::ShellRun;
    use crate::command::output::HeadTailBuffer;
    use crate::protocol::MAX_OUTPUT_BYTES;

    pub async fn run_shell(
        _shell: &str,
        script: &str,
        timeout: Duration,
        cancel: &CancellationToken,
    ) -> Result<ShellRun> {
        let bash = if std::path::Path::new("/bin/bash").exists() {
            "/bin/bash"
        } else {
            "bash"
        };
        let mut command = tokio::process::Command::new(bash);
        command
            .arg("-c")
            .arg(script)
            .stdin(std::process::Stdio::null())
            .stdout(std::process::Stdio::piped())
            .stderr(std::process::Stdio::piped())
            .kill_on_drop(true);
        // Start the shell in its own process group so timeouts can kill the
        // whole tree.
        command.as_std_mut().process_group(0);
        run_process(command, timeout, cancel).await
    }

    pub(super) async fn run_process(
        mut command: tokio::process::Command,
        timeout: Duration,
        cancel: &CancellationToken,
    ) -> Result<ShellRun> {
        let mut child = command.spawn().context("启动进程失败")?;
        let pid = child.id();
        let buffer =
            std::sync::Arc::new(std::sync::Mutex::new(HeadTailBuffer::new(MAX_OUTPUT_BYTES)));

        let stdout = child.stdout.take();
        let stderr = child.stderr.take();
        let out_buffer = buffer.clone();
        let err_buffer = buffer.clone();
        let out_task = tokio::spawn(async move { read_stream(stdout, out_buffer).await });
        let err_task = tokio::spawn(async move { read_stream(stderr, err_buffer).await });

        enum Outcome {
            Status(std::io::Result<std::process::ExitStatus>),
            Timeout,
            Cancelled,
        }

        let outcome = tokio::select! {
            _ = cancel.cancelled() => Outcome::Cancelled,
            result = tokio::time::timeout(timeout, child.wait()) => match result {
                Ok(status) => Outcome::Status(status),
                Err(_) => Outcome::Timeout,
            },
        };

        let (timed_out, cancelled) = match outcome {
            Outcome::Status(status) => {
                let code = match status {
                    Ok(status) => status.code().unwrap_or(-1),
                    Err(_) => -1,
                };
                let _ = tokio::time::timeout(Duration::from_secs(2), async {
                    let _ = out_task.await;
                    let _ = err_task.await;
                })
                .await;
                return Ok(ShellRun {
                    output: render(&buffer),
                    exit_code: code,
                    timed_out: false,
                    cancelled: false,
                });
            }
            Outcome::Timeout => (true, false),
            Outcome::Cancelled => (false, true),
        };

        if let Some(pid) = pid {
            unsafe {
                libc::kill(-(pid as i32), libc::SIGKILL);
            }
        }
        let _ = child.kill().await;
        let _ = child.wait().await;
        let _ = tokio::time::timeout(Duration::from_secs(2), async {
            let _ = out_task.await;
            let _ = err_task.await;
        })
        .await;

        Ok(ShellRun {
            output: render(&buffer),
            exit_code: -1,
            timed_out,
            cancelled,
        })
    }

    async fn read_stream<R>(
        stream: Option<R>,
        buffer: std::sync::Arc<std::sync::Mutex<HeadTailBuffer>>,
    ) where
        R: tokio::io::AsyncRead + Unpin,
    {
        let Some(mut stream) = stream else {
            return;
        };
        let mut chunk = [0u8; 8192];
        loop {
            match stream.read(&mut chunk).await {
                Ok(0) => break,
                Ok(count) => {
                    if let Ok(mut buffer) = buffer.lock() {
                        buffer.push(&chunk[..count]);
                    }
                }
                Err(_) => break,
            }
        }
    }

    fn render(buffer: &std::sync::Arc<std::sync::Mutex<HeadTailBuffer>>) -> String {
        buffer
            .lock()
            .map(|buffer| buffer.render())
            .unwrap_or_default()
    }

    pub fn sync_time(target: DateTime<Utc>) -> Result<()> {
        let is_root = unsafe { libc::geteuid() == 0 };
        if !is_root {
            bail!("需要 root 权限");
        }

        let output = if cfg!(target_os = "macos") {
            std::process::Command::new("date")
                .args(["-u", &target.format("%m%d%H%M%Y").to_string()])
                .output()
                .context("执行 date 失败")?
        } else {
            std::process::Command::new("date")
                .args(["-u", "-s", &target.format("%Y-%m-%d %H:%M:%S").to_string()])
                .output()
                .context("执行 date 失败")?
        };

        if !output.status.success() {
            let detail = String::from_utf8_lossy(&output.stderr);
            bail!("date 执行失败: {}", detail.trim());
        }

        Ok(())
    }

    pub fn restart_process() -> Result<()> {
        let exe = std::env::current_exe().context("定位当前可执行文件失败")?;
        let args: Vec<std::ffi::OsString> = std::env::args_os().skip(1).collect();
        std::process::Command::new(exe)
            .args(args)
            .spawn()
            .context("重新拉起进程失败")?;
        std::process::exit(0);
    }
}

#[cfg(windows)]
mod imp {
    use std::os::windows::process::CommandExt;
    use std::time::Duration;

    use anyhow::{bail, Context, Result};
    use chrono::{DateTime, Utc};
    use tokio::io::AsyncReadExt;
    use tokio_util::sync::CancellationToken;

    use super::ShellRun;
    use crate::command::output::HeadTailBuffer;
    use crate::protocol::MAX_OUTPUT_BYTES;

    const CREATE_NO_WINDOW: u32 = 0x0800_0000;

    pub async fn run_shell(
        _shell: &str,
        script: &str,
        timeout: Duration,
        cancel: &CancellationToken,
    ) -> Result<ShellRun> {
        let comspec = std::env::var("COMSPEC").unwrap_or_else(|_| "cmd.exe".to_string());
        let mut command = tokio::process::Command::new(comspec);
        command
            .args(["/c", script])
            .stdin(std::process::Stdio::null())
            .stdout(std::process::Stdio::piped())
            .stderr(std::process::Stdio::piped())
            .kill_on_drop(true);
        command.as_std_mut().creation_flags(CREATE_NO_WINDOW);
        run_process(command, timeout, cancel).await
    }

    pub(super) async fn run_process(
        mut command: tokio::process::Command,
        timeout: Duration,
        cancel: &CancellationToken,
    ) -> Result<ShellRun> {
        let mut child = command.spawn().context("启动进程失败")?;
        let pid = child.id();
        let buffer =
            std::sync::Arc::new(std::sync::Mutex::new(HeadTailBuffer::new(MAX_OUTPUT_BYTES)));

        let stdout = child.stdout.take();
        let stderr = child.stderr.take();
        let out_buffer = buffer.clone();
        let err_buffer = buffer.clone();
        let out_task = tokio::spawn(async move { read_stream(stdout, out_buffer).await });
        let err_task = tokio::spawn(async move { read_stream(stderr, err_buffer).await });

        enum Outcome {
            Status(std::io::Result<std::process::ExitStatus>),
            Timeout,
            Cancelled,
        }

        let outcome = tokio::select! {
            _ = cancel.cancelled() => Outcome::Cancelled,
            result = tokio::time::timeout(timeout, child.wait()) => match result {
                Ok(status) => Outcome::Status(status),
                Err(_) => Outcome::Timeout,
            },
        };

        let (timed_out, cancelled) = match outcome {
            Outcome::Status(status) => {
                let code = match status {
                    Ok(status) => status.code().unwrap_or(-1),
                    Err(_) => -1,
                };
                let _ = tokio::time::timeout(Duration::from_secs(2), async {
                    let _ = out_task.await;
                    let _ = err_task.await;
                })
                .await;
                return Ok(ShellRun {
                    output: render(&buffer),
                    exit_code: code,
                    timed_out: false,
                    cancelled: false,
                });
            }
            Outcome::Timeout => (true, false),
            Outcome::Cancelled => (false, true),
        };

        if let Some(pid) = pid {
            let _ = std::process::Command::new("taskkill")
                .args(["/T", "/F", "/PID", &pid.to_string()])
                .creation_flags(CREATE_NO_WINDOW)
                .status();
        }
        let _ = child.kill().await;
        let _ = child.wait().await;
        let _ = tokio::time::timeout(Duration::from_secs(2), async {
            let _ = out_task.await;
            let _ = err_task.await;
        })
        .await;

        Ok(ShellRun {
            output: render(&buffer),
            exit_code: -1,
            timed_out,
            cancelled,
        })
    }

    async fn read_stream<R>(
        stream: Option<R>,
        buffer: std::sync::Arc<std::sync::Mutex<HeadTailBuffer>>,
    ) where
        R: tokio::io::AsyncRead + Unpin,
    {
        let Some(mut stream) = stream else {
            return;
        };
        let mut chunk = [0u8; 8192];
        loop {
            match stream.read(&mut chunk).await {
                Ok(0) => break,
                Ok(count) => {
                    if let Ok(mut buffer) = buffer.lock() {
                        buffer.push(&chunk[..count]);
                    }
                }
                Err(_) => break,
            }
        }
    }

    fn render(buffer: &std::sync::Arc<std::sync::Mutex<HeadTailBuffer>>) -> String {
        buffer
            .lock()
            .map(|buffer| buffer.render())
            .unwrap_or_default()
    }

    pub fn sync_time(target: DateTime<Utc>) -> Result<()> {
        let script = format!("Set-Date -Date '{}'", target.to_rfc3339());
        let output = std::process::Command::new("powershell")
            .args(["-NoProfile", "-NonInteractive", "-Command", &script])
            .creation_flags(CREATE_NO_WINDOW)
            .output()
            .context("执行 Set-Date 失败")?;
        if !output.status.success() {
            let detail = String::from_utf8_lossy(&output.stderr);
            bail!("Set-Date 执行失败: {}", detail.trim());
        }
        Ok(())
    }

    pub fn restart_process() -> Result<()> {
        let exe = std::env::current_exe().context("定位当前可执行文件失败")?;
        let args: Vec<std::ffi::OsString> = std::env::args_os().skip(1).collect();
        std::process::Command::new(exe)
            .args(args)
            .creation_flags(CREATE_NO_WINDOW)
            .spawn()
            .context("重新拉起进程失败")?;
        std::process::exit(0);
    }
}

#[cfg(not(any(unix, windows)))]
mod imp {
    use std::time::Duration;

    use anyhow::{bail, Result};
    use chrono::{DateTime, Utc};
    use tokio_util::sync::CancellationToken;

    use super::ShellRun;

    pub async fn run_shell(
        _shell: &str,
        _script: &str,
        _timeout: Duration,
        _cancel: &CancellationToken,
    ) -> Result<ShellRun> {
        bail!("当前平台不支持命令执行")
    }

    pub fn sync_time(_target: DateTime<Utc>) -> Result<()> {
        bail!("当前平台不支持时间同步")
    }

    pub fn restart_process() -> Result<()> {
        bail!("当前平台不支持进程重启")
    }
}
