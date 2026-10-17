//! WebSocket `/node/notify` client with application-level Ping/Pong.

use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::Arc;
use std::time::{Duration, Instant};

use anyhow::anyhow;
use futures_util::{SinkExt, StreamExt};
use tokio::sync::mpsc;
use tokio_tungstenite::tungstenite::client::IntoClientRequest;
use tokio_tungstenite::tungstenite::Message;
use tokio_util::sync::CancellationToken;
use tracing::{debug, info, warn};

use crate::client::Client;
use crate::protocol::{self, CommandModel};

/// Notify loop failure kind.
#[derive(Debug, thiserror::Error)]
pub enum NotifyError {
    /// The bearer token was missing or rejected.
    #[error("notify unauthorized")]
    Unauthorized,
    /// The agent is shutting down.
    #[error("notify cancelled")]
    Cancelled,
    /// The connection closed or failed.
    #[error("notify connection failed: {0}")]
    Closed(#[source] anyhow::Error),
}

/// Establishes one WebSocket connection and serves it until it closes.
pub async fn run_once(
    client: Arc<Client>,
    interval_seconds: Arc<AtomicU64>,
    cancel: CancellationToken,
    on_command: Arc<dyn Fn(CommandModel) + Send + Sync>,
) -> Result<(), NotifyError> {
    let token = client.token();
    if token.is_empty() {
        return Err(NotifyError::Unauthorized);
    }
    let url = client.notify_url().map_err(NotifyError::Closed)?;
    let mut request = url
        .as_str()
        .into_client_request()
        .map_err(|error| NotifyError::Closed(anyhow!(error)))?;
    request.headers_mut().insert(
        "Authorization",
        http::HeaderValue::from_str(&format!("Bearer {token}"))
            .map_err(|error| NotifyError::Closed(anyhow!(error)))?,
    );
    request.headers_mut().insert(
        "User-Agent",
        http::HeaderValue::from_str(client.user_agent())
            .map_err(|error| NotifyError::Closed(anyhow!(error)))?,
    );

    let (socket, _response) = match tokio::time::timeout(
        Duration::from_secs(10),
        tokio_tungstenite::connect_async(request),
    )
    .await
    {
        Err(_) => {
            return Err(NotifyError::Closed(anyhow!("notify dial timeout")));
        }
        Ok(Err(error)) => return Err(classify(error)),
        Ok(Ok(value)) => value,
    };

    info!(url = %url, "notify websocket connected");

    let (sink, stream) = socket.split();
    let (out_tx, mut out_rx) = mpsc::unbounded_channel::<Message>();
    let mut writer = tokio::spawn(async move {
        let mut sink = sink;
        while let Some(message) = out_rx.recv().await {
            if sink.send(message).await.is_err() {
                break;
            }
        }
        let _ = sink.close().await;
    });

    let result = serve(stream, &out_tx, interval_seconds, cancel, on_command).await;

    drop(out_tx);
    writer.abort();
    let _ = (&mut writer).await;

    result
}

fn classify(error: tokio_tungstenite::tungstenite::Error) -> NotifyError {
    if let tokio_tungstenite::tungstenite::Error::Http(response) = &error {
        if response.status().as_u16() == 401 {
            return NotifyError::Unauthorized;
        }
    }
    NotifyError::Closed(anyhow!(error))
}

async fn serve(
    mut stream: futures_util::stream::SplitStream<
        tokio_tungstenite::WebSocketStream<
            tokio_tungstenite::MaybeTlsStream<tokio::net::TcpStream>,
        >,
    >,
    out_tx: &mpsc::UnboundedSender<Message>,
    interval_seconds: Arc<AtomicU64>,
    cancel: CancellationToken,
    on_command: Arc<dyn Fn(CommandModel) + Send + Sync>,
) -> Result<(), NotifyError> {
    let seconds = interval_seconds.load(Ordering::Relaxed).max(1);
    let keepalive = Duration::from_secs(seconds);
    let mut ticker = tokio::time::interval(keepalive);
    ticker.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
    let mut last_pong = Instant::now();

    loop {
        tokio::select! {
            _ = cancel.cancelled() => {
                return Err(NotifyError::Cancelled);
            }
            _ = ticker.tick() => {
                if last_pong.elapsed() > keepalive * 3 {
                    warn!("notify pong timeout, closing connection");
                    return Err(NotifyError::Closed(anyhow!("pong timeout")));
                }
                if out_tx.send(Message::Text("Ping".into())).is_err() {
                    return Err(NotifyError::Closed(anyhow!("notify writer closed")));
                }
            }
            message = stream.next() => {
                let message = match message {
                    None => return Err(NotifyError::Closed(anyhow!("notify stream ended"))),
                    Some(Err(error)) => return Err(NotifyError::Closed(anyhow!(error))),
                    Some(Ok(message)) => message,
                };

                match message {
                    Message::Text(text) => {
                        let text = text.trim();
                        match text {
                            "Ping" => {
                                if out_tx.send(Message::Text("Pong".into())).is_err() {
                                    return Err(NotifyError::Closed(anyhow!("notify writer closed")));
                                }
                            }
                            "Pong" => {
                                last_pong = Instant::now();
                            }
                            _ => {
                                match protocol::parse_json_ci::<CommandModel>(text.as_bytes()) {
                                    Ok(command) if !command.command.trim().is_empty() => on_command(command),
                                    Ok(_) => warn!("notify command ignored: empty command"),
                                    Err(error) => warn!(error = %error, "notify json ignored"),
                                }
                            }
                        }
                    }
                    Message::Binary(data) => {
                        debug!(bytes = data.len(), "notify binary frame ignored");
                    }
                    Message::Ping(payload) => {
                        if out_tx.send(Message::Pong(payload)).is_err() {
                            return Err(NotifyError::Closed(anyhow!("notify writer closed")));
                        }
                    }
                    Message::Pong(_) => {
                        last_pong = Instant::now();
                    }
                    Message::Close(_) => {
                        return Err(NotifyError::Closed(anyhow!("notify closed by server")));
                    }
                    Message::Frame(_) => {}
                }
            }
        }
    }
}
