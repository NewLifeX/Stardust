//! Shared startup helpers used by the CLI and the Windows service entry point.

use std::path::{Path, PathBuf};

use anyhow::Result;
use tokio_util::sync::CancellationToken;

use crate::agent::Agent;
use crate::config::{self, Config};
use crate::logging;

/// Command-line overrides applied before starting the agent.
#[derive(Clone, Debug, Default)]
pub struct RunOverrides {
    /// Overrides `Server`.
    pub server: Option<String>,
    /// Overrides `DataDir`.
    pub data_dir: Option<String>,
    /// Overrides `LogLevel`.
    pub log_level: Option<String>,
    /// Forces `AllowExec=true`.
    pub allow_exec: bool,
    /// Mirrors logs to stderr.
    pub console: bool,
}

/// Loads and validates the configuration with CLI overrides applied.
pub fn load_config(path: &Path, overrides: &RunOverrides) -> Result<Config> {
    let mut config = config::load(path)?;
    apply_overrides(&mut config, overrides);
    config.normalize();
    config.validate()?;
    Ok(config)
}

fn apply_overrides(config: &mut Config, overrides: &RunOverrides) {
    if let Some(server) = &overrides.server {
        config.server = server.clone();
    }
    if let Some(data_dir) = &overrides.data_dir {
        config.data_dir = data_dir.clone();
    }
    if let Some(level) = &overrides.log_level {
        config.log_level = level.clone();
    }
    if overrides.allow_exec {
        config.allow_exec = true;
    }
}

/// Runs the agent (`once` or long-running).
pub async fn run(
    config_path: PathBuf,
    overrides: RunOverrides,
    once: bool,
    cancel: CancellationToken,
) -> Result<()> {
    let config = load_config(&config_path, &overrides)?;
    let _guard = match logging::init(
        Path::new(&config.data_dir),
        &config.log_level,
        overrides.console,
    ) {
        Ok(guard) => Some(guard),
        Err(error) => {
            eprintln!("staragent: log setup failed, falling back to stderr: {error}");
            logging::init_console(&config.log_level);
            None
        }
    };

    let agent = Agent::new(config_path, config, cancel).await?;
    if once {
        agent.once().await
    } else {
        agent.run().await
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn applies_command_line_overrides() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("staragent.json");
        std::fs::write(
            &path,
            br#"{"Server":"https://old.example.com","MetricsInterval":5}"#,
        )
        .unwrap();

        let overrides = RunOverrides {
            server: Some("https://new.example.com".into()),
            data_dir: Some(dir.path().join("data").to_string_lossy().into_owned()),
            log_level: Some("debug".into()),
            allow_exec: true,
            console: false,
        };
        let config = load_config(&path, &overrides).unwrap();
        assert_eq!(config.server, "https://new.example.com");
        assert_eq!(config.log_level, "debug");
        assert!(config.allow_exec);
    }

    #[test]
    fn missing_config_requires_server() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("staragent.json");
        let overrides = RunOverrides::default();
        assert!(load_config(&path, &overrides).is_err());

        let overrides = RunOverrides {
            server: Some("https://star.example.com".into()),
            ..Default::default()
        };
        let config = load_config(&path, &overrides).unwrap();
        assert_eq!(config.server, "https://star.example.com");
    }
}
