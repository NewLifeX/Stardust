//! Protocol JSON models and time handling.
//!
//! Field names are PascalCase on the wire. Unknown fields are ignored and
//! known field names are matched case-insensitively to stay compatible with
//! future server-side changes.

use anyhow::{bail, Result};
use chrono::{DateTime, NaiveDateTime, TimeZone, Utc};
use serde::{Deserialize, Serialize};
use serde_json::Value;

/// Wire format for `yyyy-MM-dd HH:mm:ss` UTC timestamps.
pub const TIME_LAYOUT: &str = "%Y-%m-%d %H:%M:%S";

/// Protocol value used when a timestamp is not set.
pub const ZERO_TIME_TEXT: &str = "0001-01-01 00:00:00";

/// Maximum command output size carried in a reply.
pub const MAX_OUTPUT_BYTES: usize = 64 * 1024;

/// Command status values.
pub const STATUS_READY: i32 = 0;
/// Delay accepted and scheduled.
pub const STATUS_PROCESSING: i32 = 1;
/// Command completed successfully.
pub const STATUS_COMPLETED: i32 = 2;
/// Command cancelled or rejected.
pub const STATUS_CANCELLED: i32 = 3;
/// Command execution failed.
pub const STATUS_ERROR: i32 = 4;

/// Parses a protocol timestamp. Empty values and `0001-01-01` mean "not set".
pub fn parse_time_text(text: &str) -> Result<Option<DateTime<Utc>>> {
    let text = text.trim();
    if text.is_empty() || text == ZERO_TIME_TEXT || text.starts_with("0001-01-01T00:00:00") {
        return Ok(None);
    }

    for layout in ["%Y-%m-%d %H:%M:%S", "%Y-%m-%d %H:%M:%S%.f"] {
        if let Ok(value) = NaiveDateTime::parse_from_str(text, layout) {
            return Ok(Some(Utc.from_utc_datetime(&value)));
        }
    }
    for layout in ["%Y-%m-%dT%H:%M:%S", "%Y-%m-%dT%H:%M:%S%.f"] {
        if let Ok(value) = NaiveDateTime::parse_from_str(text, layout) {
            return Ok(Some(Utc.from_utc_datetime(&value)));
        }
    }
    if let Ok(value) = DateTime::parse_from_rfc3339(text) {
        return Ok(Some(value.with_timezone(&Utc)));
    }

    bail!("unsupported time format: {text:?}")
}

/// A protocol timestamp that serializes as `yyyy-MM-dd HH:mm:ss` UTC.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct ProtoTime(pub Option<DateTime<Utc>>);

impl ProtoTime {
    /// Creates a timestamp from a UTC value.
    pub fn new(value: DateTime<Utc>) -> Self {
        Self(Some(value))
    }

    /// Returns the current UTC time.
    pub fn now() -> Self {
        Self(Some(Utc::now()))
    }

    /// Returns Unix milliseconds, or 0 when unset.
    pub fn unix_millis(&self) -> i64 {
        self.0.map(|value| value.timestamp_millis()).unwrap_or(0)
    }

    /// Returns true when the timestamp is set (year > 2000).
    pub fn is_set(&self) -> bool {
        use chrono::Datelike;
        self.0.map(|value| value.year() > 2000).unwrap_or(false)
    }

    /// Returns the UTC timestamp when set.
    pub fn value(&self) -> Option<DateTime<Utc>> {
        self.0
    }

    /// Returns true when the timestamp is set and strictly before `other`.
    pub fn is_before(&self, other: DateTime<Utc>) -> bool {
        self.0.map(|value| value < other).unwrap_or(false)
    }

    /// Returns true when the timestamp is set and strictly after `other`.
    pub fn is_after(&self, other: DateTime<Utc>) -> bool {
        self.0.map(|value| value > other).unwrap_or(false)
    }

    /// Returns the protocol text form.
    pub fn text(&self) -> String {
        match self.0 {
            Some(value) => value.with_timezone(&Utc).format(TIME_LAYOUT).to_string(),
            None => ZERO_TIME_TEXT.to_string(),
        }
    }
}

impl Serialize for ProtoTime {
    fn serialize<S>(&self, serializer: S) -> std::result::Result<S::Ok, S::Error>
    where
        S: serde::Serializer,
    {
        serializer.serialize_str(&self.text())
    }
}

impl<'de> Deserialize<'de> for ProtoTime {
    fn deserialize<D>(deserializer: D) -> std::result::Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        let value = Option::<String>::deserialize(deserializer)?;
        match value {
            None => Ok(Self(None)),
            Some(text) => match parse_time_text(&text) {
                Ok(value) => Ok(Self(value)),
                Err(error) => Err(serde::de::Error::custom(error.to_string())),
            },
        }
    }
}

impl From<DateTime<Utc>> for ProtoTime {
    fn from(value: DateTime<Utc>) -> Self {
        Self::new(value)
    }
}

/// Node hardware and system information.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase", default)]
pub struct NodeInfo {
    #[serde(skip_serializing_if = "String::is_empty")]
    pub version: String,
    pub compile: ProtoTime,
    #[serde(rename = "OSName", skip_serializing_if = "String::is_empty")]
    pub os_name: String,
    #[serde(rename = "OSVersion", skip_serializing_if = "String::is_empty")]
    pub os_version: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub architecture: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub machine_name: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub user_name: String,
    #[serde(rename = "IP", skip_serializing_if = "String::is_empty")]
    pub ip: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub gateway: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub dns: String,
    pub processor_count: u32,
    pub memory: u64,
    pub available_memory: u64,
    pub free_memory: u64,
    pub total_size: u64,
    pub available_free_space: u64,
    pub drive_size: u64,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub drive_info: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub max_open_files: Option<u64>,
    #[serde(rename = "CLibVersion", skip_serializing_if = "String::is_empty")]
    pub c_lib_version: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub dpi: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub resolution: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub product: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub vendor: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub processor: String,
    #[serde(rename = "GPU", skip_serializing_if = "String::is_empty")]
    pub gpu: String,
    #[serde(rename = "UUID", skip_serializing_if = "String::is_empty")]
    pub uuid: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub machine_guid: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub serial_number: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub board: String,
    #[serde(rename = "DiskID", skip_serializing_if = "String::is_empty")]
    pub disk_id: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub macs: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub install_path: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub runtime: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub framework: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub time_zone: String,
    pub time: ProtoTime,
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub plugins: Vec<String>,
}

/// Login request body (`POST /node/login`).
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase", default)]
pub struct LoginInfo {
    pub code: String,
    pub secret: String,
    pub product_code: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub client_id: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub project: String,
    pub node: NodeInfo,
}

/// Login/logout response body.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase", default)]
pub struct LoginResponse {
    pub code: Option<String>,
    pub secret: Option<String>,
    pub name: Option<String>,
    pub token: Option<String>,
    pub expire: i64,
    pub time: i64,
    pub server_time: i64,
}

/// Heartbeat request body (`POST /node/ping`).
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase", default)]
pub struct PingInfo {
    pub time: i64,
    #[serde(rename = "IP", skip_serializing_if = "String::is_empty")]
    pub ip: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub gateway: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub dns: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub macs: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub drive_info: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub framework: String,
    #[serde(skip_serializing_if = "String::is_empty")]
    pub processes: String,
    pub process_count: u64,
    pub memory: u64,
    pub available_memory: u64,
    pub free_memory: u64,
    pub total_size: u64,
    pub available_free_space: u64,
    pub cpu_rate: f64,
    pub temperature: f64,
    pub battery: f64,
    pub signal: i32,
    pub uplink_speed: u64,
    pub downlink_speed: u64,
    pub uptime: u64,
    pub delay: i64,
    pub tcp_connections: u64,
    pub tcp_time_wait: u64,
    pub tcp_close_wait: u64,
    pub intranet_score: f64,
    pub internet_score: f64,
    pub system_load: f64,
    pub disk_iops: u64,
    pub disk_active_time: f64,
    pub gateway_latency: u64,
    pub gateway_loss_rate: f64,
    pub dns_latency: u64,
    pub dns_loss_rate: f64,
    pub server_latency: u64,
    pub server_loss_rate: f64,
}

/// Heartbeat response body.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase", default)]
pub struct PingResponse {
    pub time: i64,
    pub server_time: i64,
    pub period: i64,
    pub token: Option<String>,
    pub new_server: Option<String>,
    pub sync_time: i64,
    #[serde(default, deserialize_with = "null_as_default")]
    pub commands: Vec<CommandModel>,
}

/// A command pushed over WebSocket or in a ping response.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase", default)]
pub struct CommandModel {
    pub id: i64,
    pub command: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub argument: Option<String>,
    pub start_time: ProtoTime,
    pub expire: ProtoTime,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub trace_id: Option<String>,
    pub status: i32,
}

/// Command reply body (`POST /node/commandreply`).
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase", default)]
pub struct CommandReplyModel {
    pub id: i64,
    pub status: i32,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub data: Option<String>,
}

/// One event (`POST /node/postevents` array element).
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase", default)]
pub struct EventModel {
    pub time: i64,
    #[serde(rename = "Type")]
    pub event_type: String,
    pub name: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub remark: Option<String>,
}

/// Upgrade check response.
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase", default)]
pub struct UpgradeInfo {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub version: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub source: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub file_hash: Option<String>,
    pub file_size: i64,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub preinstall: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub executor: Option<String>,
    pub force: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,
}

/// Business error returned inside a 200 response envelope.
#[derive(Debug, thiserror::Error)]
#[error("server returned code {code}: {msg}")]
pub struct ApiError {
    /// Business code.
    pub code: i64,
    /// Business message.
    pub msg: String,
}

/// Deserializes `null` as the type's default value.
fn null_as_default<'de, D, T>(deserializer: D) -> std::result::Result<T, D::Error>
where
    D: serde::Deserializer<'de>,
    T: serde::Deserialize<'de> + Default,
{
    Ok(Option::<T>::deserialize(deserializer)?.unwrap_or_default())
}

/// Known protocol field names, used for case-insensitive key matching.
const KNOWN_FIELDS: &[&str] = &[
    "Code",
    "Secret",
    "Name",
    "Token",
    "Expire",
    "Time",
    "ServerTime",
    "Period",
    "NewServer",
    "SyncTime",
    "Commands",
    "Id",
    "Command",
    "Argument",
    "StartTime",
    "TraceId",
    "Status",
    "Data",
    "Msg",
    "Version",
    "Compile",
    "OSName",
    "OSVersion",
    "Architecture",
    "MachineName",
    "UserName",
    "IP",
    "Gateway",
    "Dns",
    "ProcessorCount",
    "Memory",
    "AvailableMemory",
    "FreeMemory",
    "TotalSize",
    "AvailableFreeSpace",
    "DriveSize",
    "DriveInfo",
    "MaxOpenFiles",
    "CLibVersion",
    "Dpi",
    "Resolution",
    "Product",
    "Vendor",
    "Processor",
    "GPU",
    "UUID",
    "MachineGuid",
    "SerialNumber",
    "Board",
    "DiskID",
    "Macs",
    "InstallPath",
    "Runtime",
    "Framework",
    "TimeZone",
    "Plugins",
    "ProductCode",
    "ClientId",
    "Project",
    "Temperature",
    "Battery",
    "Signal",
    "UplinkSpeed",
    "DownlinkSpeed",
    "Uptime",
    "Delay",
    "TcpConnections",
    "TcpTimeWait",
    "TcpCloseWait",
    "IntranetScore",
    "InternetScore",
    "SystemLoad",
    "DiskIOPS",
    "DiskActiveTime",
    "GatewayLatency",
    "GatewayLossRate",
    "DnsLatency",
    "DnsLossRate",
    "ServerLatency",
    "ServerLossRate",
    "Source",
    "FileHash",
    "FileSize",
    "Preinstall",
    "Executor",
    "Force",
    "Description",
    "Type",
    "Remark",
];

fn canonical_field(key: &str) -> Option<&'static str> {
    KNOWN_FIELDS
        .iter()
        .copied()
        .find(|field| field.eq_ignore_ascii_case(key))
}

fn normalize_keys(value: &mut Value) {
    match value {
        Value::Object(map) => {
            let keys: Vec<String> = map.keys().cloned().collect();
            for key in keys {
                if let Some(canonical) = canonical_field(&key) {
                    if canonical != key {
                        if let Some(entry) = map.remove(&key) {
                            map.insert(canonical.to_string(), entry);
                        }
                    }
                }
            }
            for entry in map.values_mut() {
                normalize_keys(entry);
            }
        }
        Value::Array(items) => {
            for item in items {
                normalize_keys(item);
            }
        }
        _ => {}
    }
}

/// Parses JSON with case-insensitive known field names.
pub fn parse_json_ci<T: serde::de::DeserializeOwned>(data: &[u8]) -> Result<T> {
    let mut value: Value = serde_json::from_slice(data)?;
    normalize_keys(&mut value);
    Ok(serde_json::from_value(value)?)
}

/// Parses a bare response or `{ Code, Data, Msg }` envelope.
pub fn parse_response<T: serde::de::DeserializeOwned>(data: &[u8]) -> Result<T> {
    if data.iter().all(|byte| byte.is_ascii_whitespace()) {
        bail!("empty response body");
    }

    let mut value: Value = serde_json::from_slice(data)?;
    normalize_keys(&mut value);
    if let Value::Object(map) = &value {
        if let Some(code) = map.get("Code").and_then(Value::as_i64) {
            if code != 0 {
                let msg = map
                    .get("Msg")
                    .and_then(Value::as_str)
                    .unwrap_or_default()
                    .to_string();
                return Err(ApiError { code, msg }.into());
            }
            if let Some(inner) = map.get("Data") {
                if !inner.is_null() {
                    return Ok(serde_json::from_value(inner.clone())?);
                }
            }
        }
    }

    Ok(serde_json::from_value(value)?)
}

/// Parses the `POST /node/postevents` count from a bare integer, numeric string
/// or an envelope.
pub fn parse_count(data: &[u8]) -> Result<i64> {
    if data.iter().all(|byte| byte.is_ascii_whitespace()) {
        bail!("empty response body");
    }

    let mut value: Value = serde_json::from_slice(data)?;
    normalize_keys(&mut value);

    parse_count_value(&value)
}

fn parse_count_value(value: &Value) -> Result<i64> {
    match value {
        Value::Number(number) => number
            .as_i64()
            .ok_or_else(|| anyhow::anyhow!("event count is not an integer")),
        Value::String(text) => Ok(text.trim().parse::<i64>()?),
        Value::Object(map) => {
            if let Some(code) = map.get("Code").and_then(Value::as_i64) {
                if code != 0 {
                    let msg = map
                        .get("Msg")
                        .and_then(Value::as_str)
                        .unwrap_or_default()
                        .to_string();
                    return Err(ApiError { code, msg }.into());
                }
            }
            match map.get("Data") {
                None | Some(Value::Null) => Ok(0),
                Some(inner) => parse_count_value(inner),
            }
        }
        other => bail!("unsupported event count payload: {other}"),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_zero_time() {
        assert_eq!(ProtoTime::default().text(), ZERO_TIME_TEXT);
        assert!(parse_time_text(ZERO_TIME_TEXT).unwrap().is_none());
        assert!(parse_time_text("").unwrap().is_none());
    }

    #[test]
    fn parses_and_formats_time() {
        let time = parse_time_text("2026-10-17 08:00:00").unwrap().unwrap();
        assert_eq!(ProtoTime::new(time).text(), "2026-10-17 08:00:00");
        let rfc = parse_time_text("2026-10-17T08:00:00Z").unwrap().unwrap();
        assert_eq!(ProtoTime::new(rfc).text(), "2026-10-17 08:00:00");
    }

    #[test]
    fn serializes_command_model() {
        let command = CommandModel {
            id: 7,
            command: "bash".into(),
            argument: Some("{}".into()),
            status: 0,
            ..Default::default()
        };
        let text = serde_json::to_string(&command).unwrap();
        assert!(text.contains("\"Id\":7"));
        assert!(text.contains("\"Command\":\"bash\""));
        assert!(text.contains("\"StartTime\":\"0001-01-01 00:00:00\""));
    }

    #[test]
    fn parses_ping_response_with_commands() {
        let text = br#"{
            "ServerTime": 1,
            "Period": 5,
            "Commands": [
                {"Id": 9, "Command": "node/synctime", "StartTime": "0001-01-01 00:00:00"}
            ]
        }"#;
        let response: PingResponse = parse_response(text).unwrap();
        assert_eq!(response.period, 5);
        assert_eq!(response.commands.len(), 1);
        assert_eq!(response.commands[0].id, 9);
        assert!(!response.commands[0].start_time.is_set());
    }

    #[test]
    fn parses_case_insensitive_fields() {
        let text =
            br#"{"code": "c1", "secret": "s1", "token": "t", "expire": 60, "servertime": 123}"#;
        let response: LoginResponse = parse_response(text).unwrap();
        assert_eq!(response.code.as_deref(), Some("c1"));
        assert_eq!(response.secret.as_deref(), Some("s1"));
        assert_eq!(response.token.as_deref(), Some("t"));
        assert_eq!(response.expire, 60);
        assert_eq!(response.server_time, 123);
    }

    #[test]
    fn parses_null_optional_fields() {
        let text = br#"{"ServerTime": 1, "Commands": null, "Token": null, "NewServer": null}"#;
        let response: PingResponse = parse_response(text).unwrap();
        assert!(response.commands.is_empty());
        assert!(response.token.is_none());
        assert!(response.new_server.is_none());

        let upgrade: UpgradeInfo =
            parse_response(br#"{"Version": null, "Source": null, "FileSize": 0, "Force": false}"#)
                .unwrap();
        assert!(upgrade.version.is_none());
        assert!(upgrade.source.is_none());
    }

    #[test]
    fn parses_wrapped_response() {
        let text = br#"{"code":0,"data":{"Version":"2.0"},"msg":""}"#;
        let info: UpgradeInfo = parse_response(text).unwrap();
        assert_eq!(info.version.as_deref(), Some("2.0"));
    }

    #[test]
    fn reports_business_error() {
        let text = br#"{"code":3,"msg":"denied"}"#;
        let error = parse_response::<LoginResponse>(text).unwrap_err();
        assert!(error.to_string().contains("code 3"));
    }

    #[test]
    fn parses_event_count_forms() {
        assert_eq!(parse_count(b"2").unwrap(), 2);
        assert_eq!(parse_count(b"\"3\"").unwrap(), 3);
        assert_eq!(parse_count(br#"{"code":0,"data":4}"#).unwrap(), 4);
        assert_eq!(parse_count(br#"{"code":0,"data":null}"#).unwrap(), 0);
        assert!(parse_count(br#"{"code":7,"msg":"bad"}"#).is_err());
    }
}
