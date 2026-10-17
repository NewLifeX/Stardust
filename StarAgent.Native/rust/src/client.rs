//! HTTP client for StarServer Node endpoints with token handling.

use std::sync::atomic::{AtomicI64, Ordering};
use std::sync::RwLock;
use std::time::{Duration, Instant};

use anyhow::{anyhow, bail, Context, Result};
use rand::Rng;
use reqwest::{Method, StatusCode};
use tokio_util::sync::CancellationToken;
use tracing::debug;
use url::Url;

use crate::protocol::{
    self, CommandReplyModel, EventModel, LoginInfo, LoginResponse, PingInfo, PingResponse,
    UpgradeInfo,
};

const REQUEST_TIMEOUT: Duration = Duration::from_secs(15);
const MAX_RESPONSE_BYTES: usize = 1 << 20;

/// HTTP status error with a truncated response body.
#[derive(Debug, thiserror::Error)]
#[error("http {status}: {body}")]
pub struct HttpError {
    /// HTTP status code.
    pub status: u16,
    /// Truncated response body.
    pub body: String,
}

/// Returns the HTTP status carried by an error, if any.
pub fn status_of(error: &anyhow::Error) -> Option<u16> {
    error.downcast_ref::<HttpError>().map(|inner| inner.status)
}

fn truncate_body(data: &[u8]) -> String {
    const MAX: usize = 512;
    let text = String::from_utf8_lossy(data);
    let text = text.trim();
    if text.len() <= MAX {
        text.to_string()
    } else {
        let cut = floor_char_boundary(text, MAX);
        format!("{}...", &text[..cut])
    }
}

fn floor_char_boundary(text: &str, index: usize) -> usize {
    let mut index = index.min(text.len());
    while index > 0 && !text.is_char_boundary(index) {
        index -= 1;
    }
    index
}

#[derive(Debug, Clone)]
struct TokenState {
    token: String,
    expires_at: Option<Instant>,
}

impl Default for TokenState {
    fn default() -> Self {
        Self {
            token: String::new(),
            expires_at: None,
        }
    }
}

/// StarServer Node API client.
pub struct Client {
    base_url: RwLock<String>,
    token: RwLock<TokenState>,
    http: reqwest::Client,
    user_agent: String,
}

impl Client {
    /// Creates a client for `server`.
    pub fn new(server: &str, tls_verify: bool, user_agent: String) -> Result<Self> {
        let http = reqwest::Client::builder()
            .connect_timeout(Duration::from_secs(5))
            .timeout(REQUEST_TIMEOUT)
            .user_agent(user_agent.clone())
            .danger_accept_invalid_certs(!tls_verify)
            .build()
            .context("build http client")?;

        let client = Self {
            base_url: RwLock::new(String::new()),
            token: RwLock::new(TokenState::default()),
            http,
            user_agent,
        };
        client.set_base_url(server)?;
        Ok(client)
    }

    /// Switches the StarServer address.
    pub fn set_base_url(&self, server: &str) -> Result<()> {
        let server = server.trim().trim_end_matches('/');
        let url = Url::parse(server).with_context(|| format!("invalid server url {server:?}"))?;
        if !matches!(url.scheme(), "http" | "https") || url.host_str().is_none() {
            bail!("invalid server url {server:?}");
        }
        *self
            .base_url
            .write()
            .unwrap_or_else(|poison| poison.into_inner()) = server.to_string();
        Ok(())
    }

    /// Returns the current StarServer address.
    pub fn base_url(&self) -> String {
        self.base_url
            .read()
            .unwrap_or_else(|poison| poison.into_inner())
            .clone()
    }

    /// Sets the bearer token. `expire_seconds <= 0` means no known expiry.
    pub fn set_token(&self, token: &str, expire_seconds: i64) {
        let mut guard = self
            .token
            .write()
            .unwrap_or_else(|poison| poison.into_inner());
        guard.token = token.to_string();
        guard.expires_at = if token.is_empty() || expire_seconds <= 0 {
            None
        } else {
            Some(Instant::now() + Duration::from_secs(expire_seconds as u64))
        };
    }

    /// Returns the current bearer token.
    pub fn token(&self) -> String {
        self.token
            .read()
            .unwrap_or_else(|poison| poison.into_inner())
            .token
            .clone()
    }

    /// Returns true when the token is missing or about to expire.
    pub fn token_expired(&self) -> bool {
        let guard = self
            .token
            .read()
            .unwrap_or_else(|poison| poison.into_inner());
        if guard.token.is_empty() {
            return true;
        }
        guard
            .expires_at
            .map(|expires| Instant::now() + Duration::from_secs(60) >= expires)
            .unwrap_or(false)
    }

    /// `POST /node/login`.
    pub async fn login(&self, request: &LoginInfo) -> Result<LoginResponse> {
        let body = serde_json::to_value(request).context("encode login request")?;
        let data = self
            .send(Method::POST, "/node/login", Some(body), false)
            .await
            .context("login")?;
        let response: LoginResponse = protocol::parse_response(&data).context("login")?;
        if let Some(token) = response.token.as_deref() {
            if !token.is_empty() {
                self.set_token(token, response.expire);
            }
        }
        Ok(response)
    }

    /// `POST /node/logout?reason=...`.
    pub async fn logout(&self, reason: &str) -> Result<LoginResponse> {
        let query = url::form_urlencoded::Serializer::new(String::new())
            .append_pair("reason", reason)
            .finish();
        let path = if query.is_empty() {
            "/node/logout".to_string()
        } else {
            format!("/node/logout?{query}")
        };
        let data = self
            .send(Method::POST, &path, None, true)
            .await
            .context("logout")?;
        protocol::parse_response(&data).context("logout")
    }

    /// `POST /node/ping`.
    pub async fn ping(&self, request: &PingInfo) -> Result<PingResponse> {
        let body = serde_json::to_value(request).context("encode ping request")?;
        let data = self
            .send(Method::POST, "/node/ping", Some(body), true)
            .await
            .context("ping")?;
        let response: PingResponse = protocol::parse_response(&data).context("ping")?;
        if let Some(token) = response.token.as_deref() {
            if !token.is_empty() {
                self.set_token(token, 0);
                debug!(period = response.period, "received renewed token");
            }
        }
        Ok(response)
    }

    /// `POST /node/postevents`, returning the number of events accepted.
    pub async fn post_events(&self, events: &[EventModel]) -> Result<i64> {
        let body = serde_json::to_value(events).context("encode events")?;
        let data = self
            .send(Method::POST, "/node/postevents", Some(body), true)
            .await
            .context("post events")?;
        protocol::parse_count(&data).context("post events")
    }

    /// `POST /node/commandreply`.
    pub async fn command_reply(&self, reply: &CommandReplyModel) -> Result<()> {
        let body = serde_json::to_value(reply).context("encode command reply")?;
        self.send(Method::POST, "/node/commandreply", Some(body), true)
            .await
            .with_context(|| format!("command reply {}", reply.id))?;
        Ok(())
    }

    /// `POST /node/upgrade?channel=...`. Returns `None` when no upgrade exists.
    pub async fn upgrade(&self, channel: &str) -> Result<Option<UpgradeInfo>> {
        let channel = if channel.trim().is_empty() {
            "Release"
        } else {
            channel.trim()
        };
        let query = url::form_urlencoded::Serializer::new(String::new())
            .append_pair("channel", channel)
            .finish();
        let path = format!("/node/upgrade?{query}");
        let data = self
            .send(Method::POST, &path, None, true)
            .await
            .context("upgrade")?;
        let text = String::from_utf8_lossy(&data);
        let trimmed = text.trim();
        if trimmed.is_empty() || trimmed == "null" {
            return Ok(None);
        }
        let info: UpgradeInfo = protocol::parse_response(&data).context("upgrade")?;
        if info
            .version
            .as_deref()
            .map(str::trim)
            .unwrap_or_default()
            .is_empty()
        {
            return Ok(None);
        }
        Ok(Some(info))
    }

    /// Builds the WebSocket notify URL from the current base URL.
    pub fn notify_url(&self) -> Result<String> {
        let base = self.base_url();
        let mut url = Url::parse(&base).with_context(|| format!("parse server url {base:?}"))?;
        let scheme = match url.scheme() {
            "http" => "ws",
            "https" => "wss",
            other => bail!("unsupported server scheme {other:?}"),
        };
        url.set_scheme(scheme)
            .map_err(|_| anyhow!("unsupported server scheme"))?;
        let path = format!("{}/node/notify", url.path().trim_end_matches('/'));
        url.set_path(&path);
        Ok(url.to_string())
    }

    /// Returns the user agent string.
    pub fn user_agent(&self) -> &str {
        &self.user_agent
    }

    async fn send(
        &self,
        method: Method,
        path: &str,
        body: Option<serde_json::Value>,
        auth: bool,
    ) -> Result<Vec<u8>> {
        let base = self.base_url();
        let url = format!("{base}{path}");
        let mut request = self
            .http
            .request(method.clone(), &url)
            .header("Accept", "application/json");

        if let Some(value) = &body {
            request = request.json(value);
        }
        if auth {
            let token = self.token();
            if !token.is_empty() {
                request = request.header("Authorization", format!("Bearer {token}"));
            }
        }

        let response = request
            .send()
            .await
            .map_err(|error| anyhow!("{method} {path}: {error}"))?;
        let status = response.status();
        let data = response
            .bytes()
            .await
            .with_context(|| format!("{method} {path}: read response"))?;

        debug!(
            method = %method,
            path,
            status = status.as_u16(),
            bytes = data.len(),
            "http request"
        );

        if data.len() > MAX_RESPONSE_BYTES {
            bail!("response body exceeds {MAX_RESPONSE_BYTES} bytes");
        }
        if status == StatusCode::UNAUTHORIZED && auth {
            return Err(HttpError {
                status: status.as_u16(),
                body: truncate_body(&data),
            }
            .into());
        }
        if !status.is_success() {
            return Err(HttpError {
                status: status.as_u16(),
                body: truncate_body(&data),
            }
            .into());
        }

        Ok(data.to_vec())
    }
}

/// Exponential backoff with jitter: 1s, 2s, 4s, 8s, 16s, 30s cap.
pub struct Backoff {
    min: Duration,
    max: Duration,
    jitter: f64,
    attempt: u32,
}

impl Default for Backoff {
    fn default() -> Self {
        Self::new()
    }
}

impl Backoff {
    /// Creates the default backoff policy.
    pub fn new() -> Self {
        Self {
            min: Duration::from_secs(1),
            max: Duration::from_secs(30),
            jitter: 0.2,
            attempt: 0,
        }
    }

    /// Computes the next delay.
    pub fn next_delay(&mut self) -> Duration {
        let mut delay = self.min.as_secs_f64();
        for _ in 0..self.attempt {
            if delay >= self.max.as_secs_f64() {
                break;
            }
            delay *= 2.0;
        }
        delay = delay.min(self.max.as_secs_f64());
        self.attempt = self.attempt.saturating_add(1);

        if self.jitter > 0.0 {
            let factor = 1.0 + rand::thread_rng().gen_range(-self.jitter..=self.jitter);
            delay = (delay * factor).clamp(self.min.as_secs_f64(), self.max.as_secs_f64());
        }

        Duration::from_secs_f64(delay)
    }

    /// Resets after a successful connection.
    pub fn reset(&mut self) {
        self.attempt = 0;
    }

    /// Waits for the next backoff delay. Returns false when cancelled.
    pub async fn wait(&mut self, cancel: &CancellationToken) -> bool {
        let delay = self.next_delay();
        tokio::select! {
            _ = cancel.cancelled() => false,
            _ = tokio::time::sleep(delay) => true,
        }
    }
}

/// Tracks the delay of the last heartbeat.
#[derive(Default)]
pub struct DelayGauge {
    value: AtomicI64,
}

impl DelayGauge {
    /// Records a delay in milliseconds.
    pub fn set_millis(&self, value: i64) {
        self.value.store(value.max(0), Ordering::Relaxed);
    }

    /// Returns the last delay, never negative.
    pub fn millis(&self) -> i64 {
        self.value.load(Ordering::Relaxed).max(0)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn validates_and_trims_base_url() {
        let client = Client::new("https://star.example.com:6600///", true, "test".into()).unwrap();
        assert_eq!(client.base_url(), "https://star.example.com:6600");
        assert!(client.set_base_url("ftp://example.com").is_err());
        assert!(client.set_base_url("not a url").is_err());
    }

    #[test]
    fn builds_notify_url() {
        let client = Client::new("https://star.example.com:6600/", true, "test".into()).unwrap();
        assert_eq!(
            client.notify_url().unwrap(),
            "wss://star.example.com:6600/node/notify"
        );
        client.set_base_url("http://127.0.0.1:1234").unwrap();
        assert_eq!(
            client.notify_url().unwrap(),
            "ws://127.0.0.1:1234/node/notify"
        );
    }

    #[test]
    fn token_expiry() {
        let client = Client::new("https://example.com", true, "test".into()).unwrap();
        assert!(client.token_expired());
        client.set_token("abc", 0);
        assert!(!client.token_expired());
        client.set_token("abc", 30);
        assert!(client.token_expired());
        client.set_token("abc", 120);
        assert!(!client.token_expired());
        client.set_token("", 0);
        assert!(client.token_expired());
    }

    #[test]
    fn backoff_grows_and_caps() {
        let mut backoff = Backoff::new();
        let mut delays = Vec::new();
        for _ in 0..8 {
            delays.push(backoff.next_delay().as_secs_f64());
        }
        assert!(delays[0] >= 0.8 && delays[0] <= 1.2);
        assert!(delays[5] >= 24.0 && delays[5] <= 30.0);
        assert!(delays[7] <= 30.0);
        backoff.reset();
        assert!(backoff.next_delay().as_secs_f64() <= 1.2);
    }

    #[test]
    fn delay_gauge_never_negative() {
        let gauge = DelayGauge::default();
        gauge.set_millis(-5);
        assert_eq!(gauge.millis(), 0);
        gauge.set_millis(42);
        assert_eq!(gauge.millis(), 42);
    }

    #[test]
    fn truncates_bodies() {
        let body = vec![b'a'; 2000];
        let text = truncate_body(&body);
        assert!(text.len() <= 515);
        assert!(text.ends_with("..."));
    }

    #[test]
    fn maps_status_errors() {
        let error: anyhow::Error = HttpError {
            status: 401,
            body: "invalid".into(),
        }
        .into();
        assert_eq!(status_of(&error), Some(401));
    }
}
