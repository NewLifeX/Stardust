//! Daily rotating file logging with optional stderr output.

use std::path::Path;

use anyhow::{Context, Result};
use tracing_appender::non_blocking::WorkerGuard;
use tracing_appender::rolling::{RollingFileAppender, Rotation};
use tracing_subscriber::filter::LevelFilter;
use tracing_subscriber::prelude::*;

/// Keeps the non-blocking log writer alive.
pub struct LoggingGuard {
    _guard: WorkerGuard,
}

/// Initializes the global tracing subscriber.
pub fn init(data_dir: &Path, level: &str, console: bool) -> Result<LoggingGuard> {
    let level = parse_level(level);
    let dir = data_dir.join("logs");
    std::fs::create_dir_all(&dir)
        .with_context(|| format!("create log directory {}", dir.display()))?;

    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        let _ = std::fs::set_permissions(&dir, std::fs::Permissions::from_mode(0o700));
    }

    let appender: RollingFileAppender = tracing_appender::rolling::Builder::new()
        .rotation(Rotation::DAILY)
        .filename_prefix("staragent")
        .filename_suffix("log")
        .max_log_files(7)
        .build(&dir)
        .with_context(|| format!("open log directory {}", dir.display()))?;
    let (writer, guard) = tracing_appender::non_blocking(appender);

    let file_layer = tracing_subscriber::fmt::layer()
        .with_writer(writer)
        .with_ansi(false)
        .with_timer(tracing_subscriber::fmt::time::UtcTime::rfc_3339());

    let registry = tracing_subscriber::registry().with(level).with(file_layer);

    if console {
        let _ = registry
            .with(
                tracing_subscriber::fmt::layer()
                    .with_writer(std::io::stderr)
                    .with_ansi(false),
            )
            .try_init();
    } else {
        let _ = registry.try_init();
    }

    Ok(LoggingGuard { _guard: guard })
}

/// Initializes a stderr-only logger, used when the log directory is unusable.
pub fn init_console(level: &str) {
    let _ = tracing_subscriber::fmt()
        .with_max_level(parse_level(level))
        .with_writer(std::io::stderr)
        .try_init();
}

fn parse_level(level: &str) -> LevelFilter {
    match level.trim().to_ascii_lowercase().as_str() {
        "debug" => LevelFilter::DEBUG,
        "warn" | "warning" => LevelFilter::WARN,
        "error" => LevelFilter::ERROR,
        "trace" => LevelFilter::TRACE,
        _ => LevelFilter::INFO,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_levels() {
        assert_eq!(parse_level("debug"), LevelFilter::DEBUG);
        assert_eq!(parse_level("WARN"), LevelFilter::WARN);
        assert_eq!(parse_level("error"), LevelFilter::ERROR);
        assert_eq!(parse_level("nonsense"), LevelFilter::INFO);
    }
}
