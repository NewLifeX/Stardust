//! Agent main loop: login, heartbeat, notifications, migration and events.

use std::path::PathBuf;
use std::sync::atomic::{AtomicI64, AtomicU64, Ordering};
use std::sync::{Arc, Mutex as StdMutex};
use std::time::{Duration, Instant};

use anyhow::{anyhow, Context, Result};
use chrono::{DateTime, Utc};
use tokio::sync::Mutex;
use tokio_util::sync::CancellationToken;
use tracing::{debug, error, info, warn};

use crate::client::{status_of, Backoff, Client, DelayGauge};
use crate::command::{Executor, ExecutorOptions, ReplyFn};
use crate::config::{Config, Store};
use crate::events::EventQueue;
use crate::identity;
use crate::metrics::{MetricsCollector, Snapshot};
use crate::notify::{self, NotifyError};
use crate::protocol::{
    CommandModel, CommandReplyModel, LoginInfo, NodeInfo, PingInfo, PingResponse, ProtoTime,
};

const MAX_EVENT_BATCH: usize = 100;
const UPGRADE_PERIOD: Duration = Duration::from_secs(6 * 3600);
const NETWORK_TTL: Duration = Duration::from_secs(5 * 60);

/// Marker error: the agent migrated to a new StarServer.
#[derive(Debug, thiserror::Error)]
#[error("server migrated")]
pub struct ServerMigrated;

/// Newtype carrying the login rejection cause.
#[derive(Debug, thiserror::Error)]
#[error("{0}")]
pub struct LoginRejected(anyhow::Error);

#[derive(Clone, Debug, Default)]
struct NetworkSnapshot {
    ip: String,
    gateway: String,
    dns: String,
    at: Option<Instant>,
}

impl NetworkSnapshot {
    fn gather() -> Self {
        Self {
            ip: identity::local_ips(),
            gateway: identity::gateway(),
            dns: identity::dns(),
            at: Some(Instant::now()),
        }
    }
}

/// StarAgent instance.
pub struct Agent {
    config_path: PathBuf,
    store: Arc<Store>,
    client: Arc<Client>,
    events: Arc<EventQueue>,
    metrics: Mutex<MetricsCollector>,
    executor: Arc<Executor>,
    hardware: identity::Info,
    system: identity::SystemInfo,
    network: StdMutex<NetworkSnapshot>,
    login_lock: Mutex<()>,
    upgrade_lock: Mutex<Option<Instant>>,
    offset_ms: Arc<AtomicI64>,
    last_delay: DelayGauge,
    period: Arc<AtomicU64>,
    cancel: CancellationToken,
}

impl Agent {
    /// Creates an agent from a validated config.
    pub async fn new(
        config_path: PathBuf,
        config: Config,
        cancel: CancellationToken,
    ) -> Result<Arc<Self>> {
        let data_dir = PathBuf::from(&config.data_dir);
        std::fs::create_dir_all(&data_dir)
            .with_context(|| format!("create data directory {}", data_dir.display()))?;
        #[cfg(unix)]
        {
            use std::os::unix::fs::PermissionsExt;
            let _ = std::fs::set_permissions(&data_dir, std::fs::Permissions::from_mode(0o700));
        }

        let store = Arc::new(Store::new(config_path.clone(), config.clone()));
        let client = Arc::new(Client::new(
            &config.server,
            config.tls_verify,
            format!("StarAgent-Rust/{}", crate::AGENT_VERSION),
        )?);
        let events = Arc::new(EventQueue::new(
            Some(data_dir.join("events.json")),
            crate::events::MAX_EVENTS,
        ));

        let mut metrics = MetricsCollector::new(config.data_dir.clone());
        metrics.prime().await;

        let hardware = tokio::task::spawn_blocking(identity::gather)
            .await
            .unwrap_or_default();
        let system = tokio::task::spawn_blocking(identity::system_info)
            .await
            .unwrap_or_default();
        let network = tokio::task::spawn_blocking(NetworkSnapshot::gather)
            .await
            .unwrap_or_default();

        let offset_ms = Arc::new(AtomicI64::new(0));
        let timeout = Duration::from_secs(config.command_timeout.clamp(1, 300) as u64);
        let executor = {
            let offset = offset_ms.clone();
            let store = store.clone();
            Executor::new(ExecutorOptions {
                allow_exec: config.allow_exec,
                allowed_commands: config.allowed_commands.clone(),
                timeout,
                server_time: Arc::new(move || {
                    Utc::now() + chrono::Duration::milliseconds(offset.load(Ordering::Relaxed))
                }),
                set_channel: Arc::new(move |channel: &str| {
                    store
                        .update(|current| current.channel = channel.to_string())
                        .map(|_| ())
                }),
                cancel: cancel.clone(),
            })
        };

        let period = Arc::new(AtomicU64::new(config.metrics_interval.clamp(5, 3600) as u64));

        Ok(Arc::new(Self {
            config_path,
            store,
            client,
            events,
            metrics: Mutex::new(metrics),
            executor,
            hardware,
            system,
            network: StdMutex::new(network),
            login_lock: Mutex::new(()),
            upgrade_lock: Mutex::new(None),
            offset_ms,
            last_delay: DelayGauge::default(),
            period,
            cancel,
        }))
    }

    /// Returns the config path.
    pub fn config_path(&self) -> &PathBuf {
        &self.config_path
    }

    /// Runs the agent until cancellation.
    pub async fn run(self: Arc<Self>) -> Result<()> {
        info!(
            version = crate::AGENT_VERSION,
            server = %self.client.base_url(),
            allow_exec = self.store.snapshot().allow_exec,
            "staragent starting"
        );
        self.enqueue(
            "info",
            "Startup",
            &format!("agent started version={}", crate::AGENT_VERSION),
        );

        let result = self.clone().run_loop().await;

        self.enqueue("info", "Stop", "agent stopping");
        let shutdown = CancellationToken::new();
        let _ = tokio::time::timeout(Duration::from_secs(10), self.flush_events(&shutdown)).await;
        if !self.client.token().is_empty() {
            match tokio::time::timeout(
                Duration::from_secs(10),
                self.client.logout("agent stopping"),
            )
            .await
            {
                Ok(Err(error)) => warn!(error = %error, "logout failed"),
                Err(_) => warn!("logout timed out"),
                _ => {}
            }
        }

        result
    }

    async fn run_loop(self: Arc<Self>) -> Result<()> {
        let mut backoff = Backoff::new();
        loop {
            if self.cancel.is_cancelled() {
                return Ok(());
            }

            match self.clone().run_session(&mut backoff).await {
                Ok(()) => return Ok(()),
                Err(error) => {
                    if error.downcast_ref::<ServerMigrated>().is_some() {
                        info!("server migration completed");
                        backoff.reset();
                        continue;
                    }
                    if let Some(rejected) = error.downcast_ref::<LoginRejected>() {
                        error!(error = %rejected.0, "login rejected, exiting");
                        return Err(error);
                    }

                    warn!(error = %error, "connection lost, reconnecting with backoff");
                    self.client.set_token("", 0);
                    if !backoff.wait(&self.cancel).await {
                        return Ok(());
                    }
                }
            }
        }
    }

    async fn run_session(self: Arc<Self>, backoff: &mut Backoff) -> Result<()> {
        if self.client.token().is_empty() {
            match self.login().await {
                Ok(()) => {}
                Err(error) => {
                    if let Some(status) = status_of(&error) {
                        if (400..500).contains(&status) && status != 408 && status != 429 {
                            return Err(anyhow::Error::new(LoginRejected(error)));
                        }
                    }
                    return Err(error);
                }
            }
        }

        let session_cancel = self.cancel.child_token();
        let notify_cancel = session_cancel.clone();
        let notify_agent = self.clone();
        let notify_task = tokio::spawn(async move {
            notify_agent.notify_loop(notify_cancel).await;
        });

        let result = self.session_loop(backoff).await;

        session_cancel.cancel();
        notify_task.abort();
        let _ = notify_task.await;

        result
    }

    async fn session_loop(self: &Arc<Self>, backoff: &mut Backoff) -> Result<()> {
        let config = self.store.snapshot();
        let mut period = config.metrics_interval.clamp(5, 3600) as u64;
        self.period.store(period, Ordering::Relaxed);

        self.check_upgrade().await;

        let mut reset_backoff = true;
        loop {
            let started = Instant::now();
            let response = self.ping().await?;
            self.last_delay
                .set_millis(started.elapsed().as_millis() as i64);
            if reset_backoff {
                backoff.reset();
                reset_backoff = false;
            }

            if response.period > 0 {
                let next = response.period.clamp(1, 3600) as u64;
                if next != period {
                    info!(seconds = next, "heartbeat period changed");
                    period = next;
                    self.period.store(period, Ordering::Relaxed);
                }
            }

            for command in response.commands {
                self.executor.spawn(command, self.reply_fn());
            }

            self.flush_events(&self.cancel).await;
            self.check_upgrade().await;

            if let Some(new_server) = response
                .new_server
                .as_deref()
                .map(str::trim)
                .filter(|value| !value.is_empty())
            {
                if new_server.trim_end_matches('/') != self.client.base_url() {
                    return self.migrate(new_server).await;
                }
            }

            tokio::select! {
                _ = self.cancel.cancelled() => return Ok(()),
                _ = tokio::time::sleep(Duration::from_secs(period)) => {}
            }
        }
    }

    /// Executes one login, heartbeat and command pass, then logs out.
    pub async fn once(self: Arc<Self>) -> Result<()> {
        self.login().await?;
        let response = self.ping().await?;
        info!(
            period = response.period,
            commands = response.commands.len(),
            "once ping completed"
        );

        let config = self.store.snapshot();
        let wait = Duration::from_secs(config.command_timeout.clamp(1, 300) as u64 + 10);
        for command in response.commands {
            let delayed = command.start_time.is_after(Utc::now());
            let reply = self.reply_fn();
            if delayed {
                self.executor.spawn(command, reply);
            } else {
                let _ = tokio::time::timeout(wait, self.executor.handle(command, reply)).await;
            }
        }

        let shutdown = CancellationToken::new();
        let _ = tokio::time::timeout(Duration::from_secs(10), self.flush_events(&shutdown)).await;
        if !self.client.token().is_empty() {
            match tokio::time::timeout(
                Duration::from_secs(10),
                self.client.logout("once mode completed"),
            )
            .await
            {
                Ok(Err(error)) => warn!(error = %error, "logout failed"),
                Err(_) => warn!("logout timed out"),
                _ => {}
            }
        }

        Ok(())
    }

    async fn login(&self) -> Result<()> {
        let _guard = self.login_lock.lock().await;
        let config = self.store.snapshot();
        let node = self.build_node_info().await;
        let request = LoginInfo {
            code: config.code.clone(),
            secret: config.secret.clone(),
            product_code: config.product_code.clone(),
            client_id: format!("{}@{}", self.local_ip(), std::process::id()),
            project: config.project.clone(),
            node,
        };

        let response = self.client.login(&request).await?;
        if response.server_time > 0 {
            self.offset_ms.store(
                response.server_time - Utc::now().timestamp_millis(),
                Ordering::Relaxed,
            );
        }

        let code = response.code.clone().unwrap_or_default();
        let secret = response.secret.clone().unwrap_or_default();
        let changed = (!code.is_empty() && code != config.code)
            || (!secret.is_empty() && secret != config.secret);
        if changed {
            if let Err(error) = self.store.update(|current| {
                if !code.is_empty() {
                    current.code = code.clone();
                }
                if !secret.is_empty() {
                    current.secret = secret.clone();
                }
            }) {
                error!(error = %error, "persist node identity failed");
            }
        }

        info!(
            name = response.name.as_deref().unwrap_or_default(),
            expire = response.expire,
            server_time = response.server_time,
            "login succeeded"
        );

        Ok(())
    }

    async fn ping(&self) -> Result<PingResponse> {
        let request = self.build_ping_info().await;
        match self.client.ping(&request).await {
            Ok(response) => {
                self.update_offset(&response);
                Ok(response)
            }
            Err(error) if status_of(&error) == Some(401) => {
                warn!("heartbeat unauthorized, re-login and retry once");
                self.client.set_token("", 0);
                self.login().await?;
                let request = self.build_ping_info().await;
                let response = self.client.ping(&request).await?;
                self.update_offset(&response);
                Ok(response)
            }
            Err(error) => Err(error),
        }
    }

    fn update_offset(&self, response: &PingResponse) {
        if response.server_time > 0 {
            self.offset_ms.store(
                response.server_time - Utc::now().timestamp_millis(),
                Ordering::Relaxed,
            );
        }
    }

    async fn notify_loop(self: Arc<Self>, cancel: CancellationToken) {
        let mut backoff = Backoff::new();
        loop {
            if cancel.is_cancelled() {
                return;
            }
            if self.client.token_expired() {
                if let Err(error) = self.login().await {
                    warn!(error = %error, "notify re-login failed");
                }
            }

            let agent = self.clone();
            let on_command: Arc<dyn Fn(CommandModel) + Send + Sync> = Arc::new(move |command| {
                agent.executor.spawn(command, agent.reply_fn());
            });

            match notify::run_once(
                self.client.clone(),
                self.period.clone(),
                cancel.clone(),
                on_command,
            )
            .await
            {
                Ok(()) => {}
                Err(NotifyError::Cancelled) => return,
                Err(NotifyError::Unauthorized) => {
                    warn!("notify unauthorized, re-login");
                    self.client.set_token("", 0);
                    if let Err(error) = self.login().await {
                        warn!(error = %error, "notify re-login failed");
                    }
                }
                Err(NotifyError::Closed(error)) => {
                    warn!(error = %error, "notify connection closed, retrying");
                }
            }

            if cancel.is_cancelled() {
                return;
            }
            if !backoff.wait(&cancel).await {
                return;
            }
        }
    }

    async fn send_reply(&self, reply: CommandReplyModel) {
        for attempt in 1..=3u32 {
            let result =
                tokio::time::timeout(Duration::from_secs(10), self.client.command_reply(&reply))
                    .await;
            match result {
                Ok(Ok(())) => {
                    debug!(id = reply.id, status = reply.status, "command reply sent");
                    return;
                }
                Ok(Err(error)) if status_of(&error) == Some(401) => {
                    warn!(id = reply.id, "command reply unauthorized, re-login");
                    self.client.set_token("", 0);
                    if let Err(login_error) = self.login().await {
                        warn!(error = %login_error, "re-login failed");
                    }
                }
                Ok(Err(error)) => {
                    warn!(id = reply.id, attempt, error = %error, "command reply failed");
                }
                Err(_) => {
                    warn!(id = reply.id, attempt, "command reply timed out");
                }
            }

            if self.cancel.is_cancelled() {
                return;
            }
            tokio::select! {
                _ = self.cancel.cancelled() => return,
                _ = tokio::time::sleep(Duration::from_secs(attempt as u64)) => {}
            }
        }

        error!(
            id = reply.id,
            status = reply.status,
            "command reply dropped after retries"
        );
    }

    async fn flush_events(&self, cancel: &CancellationToken) {
        for attempt in 1..=3u32 {
            let batch = self.events.take(MAX_EVENT_BATCH);
            if batch.is_empty() {
                return;
            }

            let result =
                tokio::time::timeout(Duration::from_secs(15), self.client.post_events(&batch))
                    .await;
            match result {
                Ok(Ok(count)) => {
                    debug!(count, "events posted");
                    if count >= 0 && (count as usize) < batch.len() {
                        self.events.put_front(&batch[count as usize..]);
                    }
                    if batch.len() < MAX_EVENT_BATCH {
                        return;
                    }
                }
                Ok(Err(error)) if status_of(&error) == Some(401) => {
                    self.events.put_front(&batch);
                    warn!(
                        count = batch.len(),
                        attempt, "post events unauthorized, re-login"
                    );
                    self.client.set_token("", 0);
                    if let Err(login_error) = self.login().await {
                        warn!(error = %login_error, "re-login failed");
                    }
                }
                Ok(Err(error)) => {
                    self.events.put_front(&batch);
                    warn!(count = batch.len(), attempt, error = %error, "post events failed");
                }
                Err(_) => {
                    self.events.put_front(&batch);
                    warn!(count = batch.len(), attempt, "post events timed out");
                }
            }

            if cancel.is_cancelled() {
                return;
            }
            tokio::select! {
                _ = cancel.cancelled() => return,
                _ = tokio::time::sleep(Duration::from_secs(attempt as u64)) => {}
            }
        }
    }

    async fn check_upgrade(&self) {
        {
            let mut last = self.upgrade_lock.lock().await;
            if let Some(at) = *last {
                if at.elapsed() < UPGRADE_PERIOD {
                    return;
                }
            }
            *last = Some(Instant::now());
        }

        let channel = self.store.snapshot().channel;
        match tokio::time::timeout(Duration::from_secs(15), self.client.upgrade(&channel)).await {
            Ok(Ok(Some(info))) => {
                let version = info.version.clone().unwrap_or_default();
                if version != crate::AGENT_VERSION {
                    let mut remark = format!("available version {version}");
                    if let Some(source) = info.source.as_deref().filter(|value| !value.is_empty()) {
                        remark.push_str(&format!(" source={source}"));
                    }
                    info!(
                        version = %version,
                        source = info.source.as_deref().unwrap_or_default(),
                        "upgrade available, manual update required"
                    );
                    self.enqueue("info", "Upgrade", &remark);
                }
            }
            Ok(Ok(None)) => {}
            Ok(Err(error)) => {
                if !self.cancel.is_cancelled() {
                    debug!(error = %error, "upgrade check failed");
                }
            }
            Err(_) => debug!("upgrade check timed out"),
        }
    }

    async fn migrate(&self, new_server: &str) -> Result<()> {
        let old_server = self.client.base_url();
        let config = self.store.snapshot();
        let new_server = new_server.trim().trim_end_matches('/');
        info!(from = %old_server, to = new_server, "migrating to new server");

        let _ = tokio::time::timeout(
            Duration::from_secs(10),
            self.client.logout("server migration"),
        )
        .await;

        self.client.set_base_url(new_server)?;
        if let Err(error) = self
            .store
            .update(|current| current.server = new_server.to_string())
        {
            let _ = self.client.set_base_url(&old_server);
            return Err(error);
        }
        self.client.set_token("", 0);

        if let Err(error) = self.login().await {
            let _ = self.client.set_base_url(&old_server);
            let _ = self
                .store
                .update(|current| current.server = config.server.clone());
            self.client.set_token("", 0);
            return Err(anyhow!("login to new server {new_server}: {error}"));
        }

        Err(anyhow::Error::new(ServerMigrated))
    }

    fn reply_fn(self: &Arc<Self>) -> ReplyFn {
        let agent = self.clone();
        Arc::new(move |reply| {
            let agent = agent.clone();
            Box::pin(async move {
                agent.send_reply(reply).await;
            })
        })
    }

    async fn build_node_info(&self) -> NodeInfo {
        let (ip, gateway, dns) = self.network_snapshot(true);
        let snapshot = self.collect_metrics().await;

        NodeInfo {
            version: crate::AGENT_VERSION.to_string(),
            compile: ProtoTime::new(crate::build_time()),
            os_name: self.system.os_name.clone(),
            os_version: self.system.os_version.clone(),
            architecture: identity::architecture().to_string(),
            machine_name: self.system.hostname.clone(),
            user_name: identity::user_name(),
            ip,
            gateway,
            dns,
            processor_count: std::thread::available_parallelism()
                .map(|value| value.get() as u32)
                .unwrap_or(1),
            memory: snapshot.memory,
            available_memory: snapshot.available_memory,
            free_memory: snapshot.free_memory,
            total_size: snapshot.total_size,
            available_free_space: snapshot.available_free_space,
            drive_size: snapshot.drive_size,
            drive_info: snapshot.drive_info,
            max_open_files: None,
            c_lib_version: self.hardware.c_lib_version.clone(),
            dpi: self.hardware.dpi.clone(),
            resolution: self.hardware.resolution.clone(),
            product: self.hardware.product.clone(),
            vendor: self.hardware.vendor.clone(),
            processor: self.hardware.processor.clone(),
            gpu: self.hardware.gpu.clone(),
            uuid: self.hardware.uuid.clone(),
            machine_guid: self.hardware.machine_guid.clone(),
            serial_number: self.hardware.serial_number.clone(),
            board: self.hardware.board.clone(),
            disk_id: self.hardware.disk_id.clone(),
            macs: self.hardware.macs.clone(),
            install_path: identity::executable_dir(),
            runtime: format!("rustc {}", env!("CARGO_PKG_RUST_VERSION")),
            framework: format!("rustc {}", env!("CARGO_PKG_RUST_VERSION")),
            time_zone: identity::time_zone_name(),
            time: ProtoTime::now(),
            plugins: self.plugins(),
        }
    }

    async fn build_ping_info(&self) -> PingInfo {
        let (ip, gateway, dns) = self.network_snapshot(false);
        let snapshot = self.collect_metrics().await;
        let delay = self.last_delay.millis();

        PingInfo {
            time: Utc::now().timestamp_millis(),
            ip,
            gateway,
            dns,
            macs: self.hardware.macs.clone(),
            drive_info: snapshot.drive_info,
            framework: "rust".to_string(),
            processes: snapshot.processes,
            process_count: snapshot.process_count,
            memory: snapshot.memory,
            available_memory: snapshot.available_memory,
            free_memory: snapshot.free_memory,
            total_size: snapshot.total_size,
            available_free_space: snapshot.available_free_space,
            cpu_rate: snapshot.cpu_rate,
            temperature: 0.0,
            battery: 0.0,
            signal: 0,
            uplink_speed: snapshot.uplink_speed,
            downlink_speed: snapshot.downlink_speed,
            uptime: snapshot.uptime,
            delay,
            tcp_connections: 0,
            tcp_time_wait: 0,
            tcp_close_wait: 0,
            intranet_score: 0.0,
            internet_score: 0.0,
            system_load: snapshot.system_load,
            disk_iops: 0,
            disk_active_time: 0.0,
            gateway_latency: 0,
            gateway_loss_rate: 0.0,
            dns_latency: 0,
            dns_loss_rate: 0.0,
            server_latency: delay.max(0) as u64,
            server_loss_rate: 0.0,
        }
    }

    async fn collect_metrics(&self) -> Snapshot {
        let mut metrics = self.metrics.lock().await;
        metrics.collect()
    }

    fn plugins(&self) -> Vec<String> {
        let config = self.store.snapshot();
        vec![
            "agent:rust".to_string(),
            format!("cap:exec:{}", config.allow_exec),
            "cap:upgrade=false".to_string(),
            "cap:deploy=false".to_string(),
            format!("os:{}", std::env::consts::OS),
        ]
    }

    fn network_snapshot(&self, force: bool) -> (String, String, String) {
        let mut cache = self
            .network
            .lock()
            .unwrap_or_else(|poison| poison.into_inner());
        let fresh = cache
            .at
            .map(|at| at.elapsed() < NETWORK_TTL)
            .unwrap_or(false);
        if force || !fresh {
            cache.ip = identity::local_ips();
            cache.gateway = identity::gateway();
            cache.dns = identity::dns();
            cache.at = Some(Instant::now());
        }
        (cache.ip.clone(), cache.gateway.clone(), cache.dns.clone())
    }

    fn local_ip(&self) -> String {
        self.network_snapshot(false)
            .0
            .split(',')
            .next()
            .unwrap_or_default()
            .to_string()
    }

    fn enqueue(&self, event_type: &str, name: &str, remark: &str) {
        self.events.add(crate::protocol::EventModel {
            time: Utc::now().timestamp_millis(),
            event_type: event_type.to_string(),
            name: name.to_string(),
            remark: if remark.is_empty() {
                None
            } else {
                Some(remark.to_string())
            },
        });
    }

    /// Returns the current server-adjusted time.
    pub fn server_now(&self) -> DateTime<Utc> {
        Utc::now() + chrono::Duration::milliseconds(self.offset_ms.load(Ordering::Relaxed))
    }

    /// Returns the update channel from config.
    pub fn channel(&self) -> String {
        self.store.snapshot().channel
    }

    /// Returns the current events queue.
    pub fn events(&self) -> &Arc<EventQueue> {
        &self.events
    }
}
