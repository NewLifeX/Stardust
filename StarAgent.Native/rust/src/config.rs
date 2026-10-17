//! Configuration loading, environment overrides and atomic persistence.

use std::io::Write;
use std::path::{Path, PathBuf};
use std::sync::RwLock;

use anyhow::{bail, Context, Result};
use serde::{Deserialize, Serialize};

/// Persistent agent configuration. Field names match the protocol docs.
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase", default)]
pub struct Config {
    /// StarServer base URL.
    pub server: String,
    /// Product identifier used by the server for filtering.
    pub product_code: String,
    /// Registered node code.
    pub code: String,
    /// Registered node secret.
    pub secret: String,
    /// Optional project name.
    pub project: String,
    /// Update channel.
    pub channel: String,
    /// Data directory for identity, events and logs.
    pub data_dir: String,
    /// Log level (`debug`, `info`, `warn`, `error`).
    pub log_level: String,
    /// Heartbeat period in seconds.
    pub metrics_interval: i64,
    /// Enables `bash` / `cmd` execution. Off by default.
    pub allow_exec: bool,
    /// Command whitelist. Empty means all built-in commands.
    pub allowed_commands: Vec<String>,
    /// Default command timeout in seconds (1..=300).
    pub command_timeout: i64,
    /// Verify TLS certificates. `false` is for testing only.
    pub tls_verify: bool,
}

impl Default for Config {
    fn default() -> Self {
        Self {
            server: String::new(),
            product_code: "StarAgent-Rust".to_string(),
            code: String::new(),
            secret: String::new(),
            project: String::new(),
            channel: "Release".to_string(),
            data_dir: default_data_dir(),
            log_level: "info".to_string(),
            metrics_interval: 60,
            allow_exec: false,
            allowed_commands: Vec::new(),
            command_timeout: 30,
            tls_verify: true,
        }
    }
}

impl Config {
    /// Applies defaults and trims user supplied values.
    pub fn normalize(&mut self) {
        self.server = self.server.trim().trim_end_matches('/').to_string();
        self.product_code = self.product_code.trim().to_string();
        if self.product_code.is_empty() {
            self.product_code = "StarAgent-Rust".to_string();
        }
        self.channel = self.channel.trim().to_string();
        if self.channel.is_empty() {
            self.channel = "Release".to_string();
        }
        self.log_level = self.log_level.trim().to_string();
        if self.log_level.is_empty() {
            self.log_level = "info".to_string();
        }
        self.data_dir = self.data_dir.trim().to_string();
        if self.data_dir.is_empty() {
            self.data_dir = default_data_dir();
        }
        if self.metrics_interval <= 0 {
            self.metrics_interval = 60;
        }
        if self.command_timeout <= 0 {
            self.command_timeout = 30;
        }
        self.allowed_commands = self
            .allowed_commands
            .iter()
            .map(|item| item.trim().to_string())
            .filter(|item| !item.is_empty())
            .collect();
    }

    /// Validates values required to run the agent.
    pub fn validate(&self) -> Result<()> {
        if self.server.is_empty() {
            bail!("Server is required (configure staragent.json or STARAGENT_SERVER)");
        }
        let url = url::Url::parse(&self.server)
            .with_context(|| format!("invalid Server {:?}", self.server))?;
        if !matches!(url.scheme(), "http" | "https") {
            bail!("unsupported Server scheme {:?}", url.scheme());
        }
        if url.host_str().is_none() {
            bail!("invalid Server {:?}", self.server);
        }
        if self.product_code.is_empty() {
            bail!("ProductCode is required");
        }
        if !(5..=3600).contains(&self.metrics_interval) {
            bail!(
                "MetricsInterval must be between 5 and 3600 seconds, got {}",
                self.metrics_interval
            );
        }
        if !(1..=300).contains(&self.command_timeout) {
            bail!(
                "CommandTimeout must be between 1 and 300 seconds, got {}",
                self.command_timeout
            );
        }
        Ok(())
    }

    /// Applies `STARAGENT_*` environment variables. Supports both the compact
    /// and the underscore forms used by the conformance runner.
    pub fn apply_env<F>(&mut self, lookup: F) -> Result<()>
    where
        F: Fn(&str) -> Option<String>,
    {
        let get = |names: &[&str]| names.iter().find_map(|name| lookup(name));

        if let Some(value) = get(&["STARAGENT_SERVER", "STARAGENT_SERVER_URL"]) {
            self.server = value;
        }
        if let Some(value) = get(&["STARAGENT_PRODUCTCODE", "STARAGENT_PRODUCT_CODE"]) {
            self.product_code = value;
        }
        if let Some(value) = get(&["STARAGENT_CODE"]) {
            self.code = value;
        }
        if let Some(value) = get(&["STARAGENT_SECRET"]) {
            self.secret = value;
        }
        if let Some(value) = get(&["STARAGENT_PROJECT"]) {
            self.project = value;
        }
        if let Some(value) = get(&["STARAGENT_CHANNEL"]) {
            self.channel = value;
        }
        if let Some(value) = get(&["STARAGENT_DATADIR", "STARAGENT_DATA_DIR"]) {
            self.data_dir = value;
        }
        if let Some(value) = get(&["STARAGENT_LOGLEVEL", "STARAGENT_LOG_LEVEL"]) {
            self.log_level = value;
        }
        if let Some(value) = get(&["STARAGENT_METRICSINTERVAL", "STARAGENT_METRICS_INTERVAL"]) {
            let parsed = value
                .trim()
                .parse::<i64>()
                .with_context(|| format!("invalid STARAGENT_METRICSINTERVAL={value:?}"))?;
            self.metrics_interval = parsed;
        }
        if let Some(value) = get(&["STARAGENT_ALLOWEXEC", "STARAGENT_ALLOW_EXEC"]) {
            self.allow_exec = parse_bool(&value)
                .with_context(|| format!("invalid STARAGENT_ALLOWEXEC={value:?}"))?;
        }
        if let Some(value) = get(&["STARAGENT_ALLOWEDCOMMANDS", "STARAGENT_ALLOWED_COMMANDS"]) {
            self.allowed_commands = split_list(&value);
        }
        if let Some(value) = get(&["STARAGENT_COMMANDTIMEOUT", "STARAGENT_COMMAND_TIMEOUT"]) {
            let parsed = value
                .trim()
                .parse::<i64>()
                .with_context(|| format!("invalid STARAGENT_COMMANDTIMEOUT={value:?}"))?;
            self.command_timeout = parsed;
        }
        if let Some(value) = get(&["STARAGENT_TLSVERIFY", "STARAGENT_TLS_VERIFY"]) {
            self.tls_verify = parse_bool(&value)
                .with_context(|| format!("invalid STARAGENT_TLSVERIFY={value:?}"))?;
        }

        Ok(())
    }
}

fn parse_bool(value: &str) -> Result<bool> {
    match value.trim().to_ascii_lowercase().as_str() {
        "1" | "true" | "yes" | "on" => Ok(true),
        "0" | "false" | "no" | "off" => Ok(false),
        other => bail!("invalid boolean {other:?}"),
    }
}

fn split_list(value: &str) -> Vec<String> {
    value
        .split([',', ';'])
        .map(|item| item.trim().to_string())
        .filter(|item| !item.is_empty())
        .collect()
}

/// Returns the default config path, honoring `STARAGENT_CONFIG`.
pub fn default_path() -> PathBuf {
    for name in ["STARAGENT_CONFIG", "STARAGENT_CONFIG_FILE"] {
        if let Ok(value) = std::env::var(name) {
            let value = value.trim();
            if !value.is_empty() {
                return PathBuf::from(value);
            }
        }
    }

    PathBuf::from("staragent.json")
}

/// Loads the config file if present, applies environment overrides and
/// normalizes the result.
pub fn load(path: &Path) -> Result<Config> {
    let mut config = Config::default();
    match std::fs::read(path) {
        Ok(data) => {
            config = serde_json::from_slice(&data)
                .with_context(|| format!("decode config {}", path.display()))?;
        }
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => {}
        Err(error) => {
            return Err(error).with_context(|| format!("read config {}", path.display()));
        }
    }

    config.apply_env(|name| std::env::var(name).ok())?;
    config.normalize();
    Ok(config)
}

/// Saves the config atomically with mode 0600.
pub fn save(path: &Path, config: &Config) -> Result<()> {
    let mut config = config.clone();
    config.normalize();
    let mut data = serde_json::to_vec_pretty(&config).context("encode config")?;
    data.push(b'\n');
    write_atomic(path, &data, 0o600)
}

/// Writes a file through a temporary file in the same directory and renames
/// it, avoiding partially written files.
pub fn write_atomic(path: &Path, data: &[u8], mode: u32) -> Result<()> {
    let _ = mode;
    let dir = path
        .parent()
        .filter(|value| !value.as_os_str().is_empty())
        .unwrap_or_else(|| Path::new("."));
    std::fs::create_dir_all(dir).with_context(|| format!("create directory {}", dir.display()))?;
    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        let _ = std::fs::set_permissions(dir, std::fs::Permissions::from_mode(0o700));
    }

    let mut temp = tempfile::Builder::new()
        .prefix(".staragent-")
        .suffix(".tmp")
        .tempfile_in(dir)
        .context("create temporary file")?;

    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        temp.as_file()
            .set_permissions(std::fs::Permissions::from_mode(mode))
            .context("chmod temporary file")?;
    }

    temp.write_all(data).context("write temporary file")?;
    temp.as_file().sync_all().context("sync temporary file")?;
    temp.persist(path)
        .map_err(|error| error.error)
        .with_context(|| format!("replace {}", path.display()))?;

    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        std::fs::set_permissions(path, std::fs::Permissions::from_mode(mode))
            .with_context(|| format!("chmod {}", path.display()))?;
    }

    Ok(())
}

/// Thread-safe config store with atomic updates.
pub struct Store {
    path: PathBuf,
    config: RwLock<Config>,
}

impl Store {
    /// Creates a store backed by `path`.
    pub fn new(path: PathBuf, mut config: Config) -> Self {
        config.normalize();
        Self {
            path,
            config: RwLock::new(config),
        }
    }

    /// Returns the config path.
    pub fn path(&self) -> &Path {
        &self.path
    }

    /// Returns a snapshot of the current config.
    pub fn snapshot(&self) -> Config {
        self.config
            .read()
            .unwrap_or_else(|poison| poison.into_inner())
            .clone()
    }

    /// Applies a change and persists it. The in-memory state is only updated
    /// after a successful write.
    pub fn update<F>(&self, apply: F) -> Result<Config>
    where
        F: FnOnce(&mut Config),
    {
        let mut guard = self
            .config
            .write()
            .unwrap_or_else(|poison| poison.into_inner());
        let mut next = guard.clone();
        apply(&mut next);
        next.normalize();
        save(&self.path, &next)?;
        *guard = next.clone();
        Ok(next)
    }
}

/// Platform-specific data directory.
pub fn default_data_dir() -> String {
    if cfg!(windows) {
        if let Ok(program_data) = std::env::var("ProgramData") {
            if !program_data.trim().is_empty() {
                return Path::new(&program_data)
                    .join("StarAgent")
                    .join("data")
                    .to_string_lossy()
                    .into_owned();
            }
        }
        return home_dir()
            .map(|home| home.join(".staragent"))
            .unwrap_or_else(|| PathBuf::from("data"))
            .to_string_lossy()
            .into_owned();
    }

    #[cfg(unix)]
    let is_root = unsafe { libc::geteuid() == 0 };
    #[cfg(not(unix))]
    let is_root = false;

    if cfg!(target_os = "macos") {
        let base = if is_root {
            PathBuf::from("/Library/Application Support/StarAgent")
        } else {
            home_dir()
                .map(|home| {
                    home.join("Library")
                        .join("Application Support")
                        .join("StarAgent")
                })
                .unwrap_or_else(|| PathBuf::from(".staragent"))
        };
        return base.to_string_lossy().into_owned();
    }

    if is_root {
        return "/var/lib/staragent".to_string();
    }

    home_dir()
        .map(|home| home.join(".staragent"))
        .unwrap_or_else(|| PathBuf::from("data"))
        .to_string_lossy()
        .into_owned()
}

fn home_dir() -> Option<PathBuf> {
    std::env::var_os("HOME")
        .map(PathBuf::from)
        .or_else(|| std::env::var_os("USERPROFILE").map(PathBuf::from))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::HashMap;

    fn env_map(values: &[(&str, &str)]) -> HashMap<String, String> {
        values
            .iter()
            .map(|(key, value)| (key.to_string(), value.to_string()))
            .collect()
    }

    #[test]
    fn applies_environment_overrides() {
        let mut config = Config::default();
        let values = env_map(&[
            ("STARAGENT_SERVER", "https://star.example.com:6600/"),
            ("STARAGENT_PRODUCT_CODE", "StarAgent-Test"),
            ("STARAGENT_DATA_DIR", "/tmp/data"),
            ("STARAGENT_METRICS_INTERVAL", "12"),
            ("STARAGENT_ALLOW_EXEC", "true"),
            ("STARAGENT_ALLOWED_COMMANDS", "node/*, bash;cmd"),
            ("STARAGENT_COMMAND_TIMEOUT", "45"),
            ("STARAGENT_TLS_VERIFY", "false"),
        ]);
        config.apply_env(|name| values.get(name).cloned()).unwrap();
        config.normalize();

        assert_eq!(config.server, "https://star.example.com:6600");
        assert_eq!(config.product_code, "StarAgent-Test");
        assert_eq!(config.metrics_interval, 12);
        assert!(config.allow_exec);
        assert_eq!(config.allowed_commands, vec!["node/*", "bash", "cmd"]);
        assert_eq!(config.command_timeout, 45);
        assert!(!config.tls_verify);
        config.validate().unwrap();
    }

    #[test]
    fn rejects_invalid_values() {
        let mut config = Config::default();
        config.server = "ftp://example.com".into();
        assert!(config.validate().is_err());

        config.server = "https://example.com".into();
        config.metrics_interval = 2;
        assert!(config.validate().is_err());

        config.metrics_interval = 60;
        config.command_timeout = 301;
        assert!(config.validate().is_err());
    }

    #[test]
    fn store_persists_and_reloads() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("staragent.json");
        let mut config = Config::default();
        config.server = "https://star.example.com".into();
        config.metrics_interval = 5;
        let store = Store::new(path.clone(), config);

        store
            .update(|current| {
                current.code = "CODE1".into();
                current.secret = "SECRET1".into();
            })
            .unwrap();

        let loaded = load(&path).unwrap();
        assert_eq!(loaded.code, "CODE1");
        assert_eq!(loaded.secret, "SECRET1");
        assert_eq!(loaded.server, "https://star.example.com");

        #[cfg(unix)]
        {
            use std::os::unix::fs::PermissionsExt;
            let mode = std::fs::metadata(&path).unwrap().permissions().mode() & 0o777;
            assert_eq!(mode, 0o600);
        }
    }

    #[test]
    fn atomic_write_creates_parent() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("nested").join("events.json");
        write_atomic(&path, b"[]", 0o600).unwrap();
        assert_eq!(std::fs::read(&path).unwrap(), b"[]");
    }
}
