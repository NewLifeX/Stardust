//! StarAgent native agent for StarServer.
//!
//! Implements the protocol described in `StarAgent.Native/spec/protocol.md`.

pub mod agent;
pub mod app;
pub mod client;
pub mod command;
pub mod config;
pub mod events;
pub mod identity;
pub mod logging;
pub mod metrics;
pub mod notify;
pub mod protocol;
pub mod service;

use chrono::{DateTime, Utc};

/// Agent version reported to the server.
pub const AGENT_VERSION: &str = env!("STARAGENT_VERSION");

/// Build time, formatted as `yyyy-MM-dd HH:mm:ss` in UTC.
pub const BUILD_TIME_TEXT: &str = env!("STARAGENT_BUILD_TIME");

/// Returns the build time as UTC.
pub fn build_time() -> DateTime<Utc> {
    DateTime::parse_from_str(&format!("{BUILD_TIME_TEXT} +0000"), "%Y-%m-%d %H:%M:%S %z")
        .map(|value| value.with_timezone(&Utc))
        .unwrap_or_else(|_| Utc::now())
}

/// Installs the process-wide rustls crypto provider (ring) once.
pub fn init_crypto_provider() {
    // Multiple rustls users share the process-level provider. Ignoring the
    // error keeps this idempotent when a dependency already installed one.
    let _ = rustls::crypto::ring::default_provider().install_default();
}
