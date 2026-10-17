//! Cross-platform metric collection with graceful degradation.
//!
//! Every metric is optional: a collection failure leaves the field at 0 so the
//! heartbeat is still sent.

use std::collections::HashSet;
use std::path::Path;
use std::time::Instant;

use sysinfo::{Disks, Networks, ProcessesToUpdate, System};

/// One metric sample.
#[derive(Clone, Debug, Default)]
pub struct Snapshot {
    /// CPU usage 0..100.
    pub cpu_rate: f64,
    /// Total physical memory in bytes.
    pub memory: u64,
    /// Available memory in bytes.
    pub available_memory: u64,
    /// Free memory in bytes.
    pub free_memory: u64,
    /// Total size of the data-dir disk.
    pub total_size: u64,
    /// Available space on the data-dir disk.
    pub available_free_space: u64,
    /// Sum of all disk capacities.
    pub drive_size: u64,
    /// `mount[fs]=free/total` list.
    pub drive_info: String,
    /// Upload rate in bytes per second.
    pub uplink_speed: u64,
    /// Download rate in bytes per second.
    pub downlink_speed: u64,
    /// Number of processes.
    pub process_count: u64,
    /// Unique, filtered process names.
    pub processes: String,
    /// System uptime in seconds.
    pub uptime: u64,
    /// Load average (Linux/macOS) or 0.
    pub system_load: f64,
}

/// Collects system metrics and computes network rates.
pub struct MetricsCollector {
    data_dir: String,
    system: System,
    disks: Disks,
    networks: Networks,
    previous_rx: u64,
    previous_tx: u64,
    previous_at: Option<Instant>,
}

impl MetricsCollector {
    /// Creates a collector rooted at `data_dir`.
    pub fn new(data_dir: impl Into<String>) -> Self {
        Self {
            data_dir: data_dir.into(),
            system: System::new_all(),
            disks: Disks::new_with_refreshed_list(),
            networks: Networks::new_with_refreshed_list(),
            previous_rx: 0,
            previous_tx: 0,
            previous_at: None,
        }
    }

    /// Warms up CPU and network counters so the first heartbeat has rates.
    pub async fn prime(&mut self) {
        self.system.refresh_cpu_usage();
        tokio::time::sleep(sysinfo::MINIMUM_CPU_UPDATE_INTERVAL).await;
        let _ = self.collect();
    }

    /// Takes one sample. Individual failures keep the field at 0.
    pub fn collect(&mut self) -> Snapshot {
        let mut snapshot = Snapshot::default();

        self.system.refresh_cpu_usage();
        self.system.refresh_memory();
        self.system.refresh_processes(ProcessesToUpdate::All, true);
        self.disks.refresh(true);
        self.networks.refresh(true);

        snapshot.cpu_rate = self.system.global_cpu_usage().clamp(0.0, 100.0) as f64;
        snapshot.memory = self.system.total_memory();
        snapshot.available_memory = self.system.available_memory();
        snapshot.free_memory = self.system.free_memory();

        if let Some(disk) = self.data_disk() {
            snapshot.total_size = disk.total_space();
            snapshot.available_free_space = disk.available_space();
        }
        let (drive_info, drive_size) = describe_drives(&self.drive_entries());
        snapshot.drive_info = drive_info;
        snapshot.drive_size = drive_size;

        let (rx, tx) = self.network_totals();
        let (down, up) = self.speeds(rx, tx);
        snapshot.downlink_speed = down;
        snapshot.uplink_speed = up;

        let processes = self.system.processes();
        snapshot.process_count = processes.len() as u64;
        snapshot.processes = join_process_names(
            processes
                .values()
                .map(|process| process.name().to_string_lossy().into_owned()),
        );

        snapshot.uptime = System::uptime();
        snapshot.system_load = sysinfo::System::load_average().one;

        snapshot
    }

    fn data_disk(&self) -> Option<&sysinfo::Disk> {
        let target = normalize_path(Path::new(&self.data_dir));
        self.disks
            .list()
            .iter()
            .filter(|disk| disk.total_space() > 0)
            .filter(|disk| is_under(&target, &normalize_path(disk.mount_point())))
            .max_by_key(|disk| normalize_path(disk.mount_point()).len())
            .or_else(|| self.disks.list().iter().find(|disk| disk.total_space() > 0))
    }

    fn drive_entries(&self) -> Vec<DriveEntry> {
        self.disks
            .list()
            .iter()
            .filter(|disk| disk.total_space() > 0)
            .map(|disk| DriveEntry {
                path: disk.mount_point().to_string_lossy().into_owned(),
                fs: disk.file_system().to_string_lossy().into_owned(),
                total: disk.total_space(),
                free: disk.available_space(),
            })
            .collect()
    }

    fn network_totals(&self) -> (u64, u64) {
        self.networks
            .list()
            .values()
            .fold((0u64, 0u64), |(rx, tx), data| {
                (
                    rx.saturating_add(data.received()),
                    tx.saturating_add(data.transmitted()),
                )
            })
    }

    fn speeds(&mut self, rx: u64, tx: u64) -> (u64, u64) {
        let now = Instant::now();
        let mut down = 0;
        let mut up = 0;
        if let Some(previous) = self.previous_at {
            let elapsed = now.duration_since(previous).as_secs_f64();
            if elapsed > 0.0 && rx >= self.previous_rx && tx >= self.previous_tx {
                down = ((rx - self.previous_rx) as f64 / elapsed) as u64;
                up = ((tx - self.previous_tx) as f64 / elapsed) as u64;
            }
        }
        self.previous_rx = rx;
        self.previous_tx = tx;
        self.previous_at = Some(now);
        (down, up)
    }
}

/// One disk entry used for `DriveInfo`.
pub struct DriveEntry {
    /// Mount point.
    pub path: String,
    /// File system name.
    pub fs: String,
    /// Total bytes.
    pub total: u64,
    /// Free bytes.
    pub free: u64,
}

fn describe_drives(drives: &[DriveEntry]) -> (String, u64) {
    let mut items = Vec::new();
    let mut total = 0u64;
    for drive in drives {
        if drive.total == 0
            || drive.fs.eq_ignore_ascii_case("cdfs")
            || drive.fs.eq_ignore_ascii_case("iso9660")
            || drive.fs.eq_ignore_ascii_case("udf")
        {
            continue;
        }
        total = total.saturating_add(drive.total);
        let mut path = drive.path.clone();
        if std::env::consts::OS == "windows" && path.len() == 2 && path.ends_with(':') {
            path.push('\\');
        }
        items.push(format!(
            "{}[{}]={}/{}",
            path,
            drive.fs,
            format_size(drive.free),
            format_size(drive.total)
        ));
    }
    items.sort();

    (items.join(","), total)
}

const IGNORED_PROCESS_NAMES: &[&str] = &[
    "System",
    "System Idle Process",
    "Idle",
    "Registry",
    "Memory Compression",
];

fn join_process_names(names: impl Iterator<Item = String>) -> String {
    let mut seen = HashSet::new();
    let mut result = Vec::new();
    for name in names {
        let name = name.trim().to_string();
        if name.is_empty() || IGNORED_PROCESS_NAMES.contains(&name.as_str()) || name.ends_with('$')
        {
            continue;
        }
        if !seen.insert(name.to_ascii_lowercase()) {
            continue;
        }
        result.push(name);
        if result.len() >= 200 {
            break;
        }
    }
    result.sort();

    result.join(",")
}

/// Formats a byte count using 1024-based units.
pub fn format_size(value: u64) -> String {
    const K: u64 = 1024;
    const M: u64 = K * 1024;
    const G: u64 = M * 1024;
    const T: u64 = G * 1024;

    if value >= T {
        format_unit(value, T, "T")
    } else if value >= G {
        format_unit(value, G, "G")
    } else if value >= M {
        format_unit(value, M, "M")
    } else if value >= K {
        format_unit(value, K, "K")
    } else {
        format!("{value}B")
    }
}

fn format_unit(value: u64, unit: u64, suffix: &str) -> String {
    if value % unit == 0 {
        format!("{}{suffix}", value / unit)
    } else {
        format!("{:.1}{suffix}", value as f64 / unit as f64)
    }
}

fn normalize_path(path: &Path) -> String {
    path.to_string_lossy()
        .replace('\\', "/")
        .to_ascii_lowercase()
}

fn is_under(target: &str, mount: &str) -> bool {
    if mount.is_empty() {
        return false;
    }
    if target == mount {
        return true;
    }
    let prefix = mount.trim_end_matches('/');
    target.starts_with(&format!("{prefix}/"))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn formats_sizes() {
        assert_eq!(format_size(0), "0B");
        assert_eq!(format_size(512), "512B");
        assert_eq!(format_size(1024), "1K");
        assert_eq!(format_size(1536), "1.5K");
        assert_eq!(format_size(1024 * 1024), "1M");
        assert_eq!(format_size(3 * 1024 * 1024 * 1024), "3G");
        assert_eq!(format_size(1024u64.pow(4)), "1T");
    }

    #[test]
    fn filters_process_names() {
        let names = vec![
            "nginx".to_string(),
            "NGINX".to_string(),
            "System".to_string(),
            "weird$".to_string(),
            "dotnet".to_string(),
        ];
        let joined = join_process_names(names.into_iter());
        assert_eq!(joined, "dotnet,nginx");
    }

    #[test]
    fn describes_drives() {
        let drives = vec![
            DriveEntry {
                path: "/".into(),
                fs: "ext4".into(),
                total: 100 * 1024 * 1024,
                free: 50 * 1024 * 1024,
            },
            DriveEntry {
                path: "/data".into(),
                fs: "xfs".into(),
                total: 10 * 1024 * 1024,
                free: 2 * 1024 * 1024,
            },
            DriveEntry {
                path: "/zero".into(),
                fs: "tmpfs".into(),
                total: 0,
                free: 0,
            },
        ];
        let (info, total) = describe_drives(&drives);
        assert_eq!(info, "/[ext4]=50M/100M,/data[xfs]=2M/10M");
        assert_eq!(total, 110 * 1024 * 1024);
    }

    #[test]
    fn matches_paths() {
        assert!(is_under("c:/programdata/staragent/data", "c:/"));
        assert!(is_under("/var/lib/staragent", "/var"));
        assert!(!is_under("/various", "/var"));
        assert!(is_under("/", "/"));
    }

    #[test]
    fn collects_without_panicking() {
        let dir = tempfile::tempdir().unwrap();
        let mut collector = MetricsCollector::new(dir.path().to_string_lossy().into_owned());
        let snapshot = collector.collect();
        assert!(snapshot.cpu_rate >= 0.0 && snapshot.cpu_rate <= 100.0);
        assert!(snapshot.memory > 0);
        assert!(snapshot.process_count > 0);
    }
}
