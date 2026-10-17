//! Stable hardware identifiers and system/network information.
//!
//! Values that cannot be read stay empty. Random values are never generated,
//! so a node keeps a stable identity across restarts.

use std::io::Read;
use std::net::UdpSocket;
use std::path::{Path, PathBuf};
use std::process::{Command, Stdio};
use std::time::{Duration, Instant};

use get_if_addrs::IfAddr;

/// Stable hardware identifiers.
#[derive(Clone, Debug, Default)]
pub struct Info {
    /// DMI product UUID.
    pub uuid: String,
    /// OS installation identifier.
    pub machine_guid: String,
    /// Device serial number.
    pub serial_number: String,
    /// Mainboard identifier.
    pub board: String,
    /// System disk serial.
    pub disk_id: String,
    /// Uppercase MAC list separated by commas.
    pub macs: String,
    /// Product/model name.
    pub product: String,
    /// Manufacturer.
    pub vendor: String,
    /// CPU model.
    pub processor: String,
    /// GPU list.
    pub gpu: String,
    /// Screen DPI.
    pub dpi: String,
    /// Screen resolution.
    pub resolution: String,
    /// Installed C runtime versions.
    pub c_lib_version: String,
}

/// Operating system information.
#[derive(Clone, Debug, Default)]
pub struct SystemInfo {
    /// OS name.
    pub os_name: String,
    /// OS version.
    pub os_version: String,
    /// Hostname.
    pub hostname: String,
}

/// Collects stable hardware identifiers, tolerating every failure.
pub fn gather() -> Info {
    let mut info = Info::default();

    #[cfg(target_os = "linux")]
    {
        info.uuid = clean_id(&read_trim("/sys/class/dmi/id/product_uuid"));
        info.machine_guid = {
            let value = clean_id(&read_trim("/etc/machine-id"));
            if value.is_empty() {
                clean_id(&read_trim("/var/lib/dbus/machine-id"))
            } else {
                value
            }
        };
        info.serial_number = clean_id(&read_trim("/sys/class/dmi/id/product_serial"));
        let board = clean_id(&read_trim("/sys/class/dmi/id/board_serial"));
        info.board = if board.is_empty() {
            clean_id(&read_trim("/sys/class/dmi/id/board_name"))
        } else {
            board
        };
        info.disk_id = clean_id(&linux_disk_serial());
    }

    #[cfg(target_os = "windows")]
    {
        for (key, value) in windows_hardware() {
            match key.as_str() {
                "UUID" => info.uuid = clean_id(&value),
                "SERIAL" => info.serial_number = clean_id(&value),
                "BOARD" => info.board = clean_id(&value),
                "DISK" => info.disk_id = clean_id(&value),
                "PRODUCT" => info.product = clean_id(&value),
                "VENDOR" => info.vendor = clean_id(&value),
                "PROCESSOR" => info.processor = clean_id(&value),
                "GPU" => info.gpu = clean_id(&value),
                "DPI" => info.dpi = clean_id(&value),
                "RESOLUTION" => info.resolution = clean_id(&value),
                "CRUNTIME" => info.c_lib_version = clean_id(&value),
                _ => {}
            }
        }
        info.machine_guid = clean_id(&windows_machine_guid());
    }

    #[cfg(target_os = "macos")]
    {
        let values = darwin_hardware();
        for (key, value) in values {
            match key.as_str() {
                "UUID" => info.uuid = clean_id(&value),
                "SERIAL" => info.serial_number = clean_id(&value),
                "BOARD" => info.board = clean_id(&value),
                _ => {}
            }
        }
    }

    info.macs = collect_macs();
    info
}

/// Collects operating system information.
pub fn system_info() -> SystemInfo {
    let details = os_info::get();
    #[allow(unused_mut)]
    let mut info = SystemInfo {
        os_name: details.os_type().to_string(),
        os_version: details.version().to_string(),
        hostname: host_name(),
    };

    #[cfg(target_os = "linux")]
    if let Some(name) = linux_os_name() {
        info.os_name = name;
    }

    #[cfg(target_os = "windows")]
    {
        let (name, version) = windows_os_info();
        if !name.is_empty() {
            info.os_name = name;
        }
        if !version.is_empty() {
            info.os_version = version;
        }
    }

    info
}

#[cfg(target_os = "windows")]
fn windows_os_info() -> (String, String) {
    let script = r#"$ErrorActionPreference='SilentlyContinue'
$os = Get-CimInstance -ClassName Win32_OperatingSystem
"OSNAME=" + $os.Caption
"OSVERSION=" + $os.Version"#;
    let output = run_output(
        "powershell",
        &["-NoProfile", "-NonInteractive", "-Command", script],
        Duration::from_secs(8),
    );
    let values = parse_key_values(&output);
    let name = values
        .get("OSNAME")
        .map(|value| value.trim().trim_start_matches("Microsoft ").to_string())
        .unwrap_or_default();
    let version = values
        .get("OSVERSION")
        .map(|value| value.trim().to_string())
        .unwrap_or_default();
    (name, version)
}

/// Returns the timezone in the format expected by StarServer.
pub fn time_zone_name() -> String {
    let offset = chrono::Local::now().offset().to_string();
    if offset.starts_with('+') || offset.starts_with('-') {
        format!("GMT{offset}")
    } else {
        offset
    }
}

#[allow(dead_code)]
fn read_trim(path: impl AsRef<Path>) -> String {
    std::fs::read_to_string(path)
        .map(|value| value.trim().to_string())
        .unwrap_or_default()
}

const INVALID_IDS: &[&str] = &[
    "",
    "none",
    "unknown",
    "not specified",
    "not settable",
    "to be filled by o.e.m.",
    "to be filled by o.e.m",
    "default string",
    "system serial number",
    "0",
    "00000000-0000-0000-0000-000000000000",
    "ffffffff-ffff-ffff-ffff-ffffffffffff",
];

fn clean_id(value: &str) -> String {
    let value = value.trim().trim_matches(['"', '\'']).trim();
    if INVALID_IDS
        .iter()
        .any(|invalid| invalid.eq_ignore_ascii_case(value))
    {
        return String::new();
    }
    // The server compares UUID/MachineGuid/Serial case-sensitively.
    value.to_string()
}

#[cfg(target_os = "linux")]
fn linux_disk_serial() -> String {
    let Ok(entries) = std::fs::read_dir("/sys/block") else {
        return String::new();
    };
    for entry in entries.flatten() {
        let name = entry.file_name().to_string_lossy().into_owned();
        if name.starts_with("loop")
            || name.starts_with("ram")
            || name.starts_with("dm-")
            || name.starts_with("md")
        {
            continue;
        }
        for relative in ["device/serial", "serial"] {
            let value = read_trim(format!("/sys/block/{name}/{relative}"));
            if !value.is_empty() {
                return value;
            }
        }
    }
    String::new()
}

fn run_output(program: &str, args: &[&str], timeout: Duration) -> String {
    let mut child = match Command::new(program)
        .args(args)
        .stdin(Stdio::null())
        .stdout(Stdio::piped())
        .stderr(Stdio::null())
        .spawn()
    {
        Ok(child) => child,
        Err(_) => return String::new(),
    };

    let mut stdout = match child.stdout.take() {
        Some(value) => value,
        None => {
            let _ = child.kill();
            let _ = child.wait();
            return String::new();
        }
    };
    let reader = std::thread::spawn(move || {
        let mut text = String::new();
        let _ = stdout.read_to_string(&mut text);
        text
    });

    let started = Instant::now();
    loop {
        match child.try_wait() {
            Ok(Some(_)) => break,
            Ok(None) if started.elapsed() < timeout => {
                std::thread::sleep(Duration::from_millis(50));
            }
            _ => {
                let _ = child.kill();
                let _ = child.wait();
                break;
            }
        }
    }
    let _ = child.wait();

    reader.join().unwrap_or_default()
}

#[cfg(target_os = "windows")]
fn windows_machine_guid() -> String {
    let output = run_output(
        "reg",
        &[
            "query",
            r"HKLM\SOFTWARE\Microsoft\Cryptography",
            "/v",
            "MachineGuid",
        ],
        Duration::from_secs(8),
    );
    for line in output.lines() {
        let fields: Vec<&str> = line.split_whitespace().collect();
        if fields.len() >= 3 && fields[0].eq_ignore_ascii_case("MachineGuid") {
            return fields[fields.len() - 1].to_string();
        }
    }
    String::new()
}

#[cfg(target_os = "windows")]
fn windows_hardware() -> std::collections::HashMap<String, String> {
    let script = r#"$ErrorActionPreference='SilentlyContinue'
$cs = Get-CimInstance -ClassName Win32_ComputerSystem
$bb = Get-CimInstance -ClassName Win32_BaseBoard
"UUID=" + (Get-CimInstance -ClassName Win32_ComputerSystemProduct).UUID
"SERIAL=" + (Get-CimInstance -ClassName Win32_BIOS).SerialNumber
"BOARD=" + $bb.SerialNumber
"PRODUCT=" + $bb.Product
"VENDOR=" + $cs.Manufacturer
"PROCESSOR=" + ((Get-CimInstance -ClassName Win32_Processor | Select-Object -First 1).Name)
"GPU=" + (((Get-CimInstance -ClassName Win32_VideoController | Where-Object { $_.Name -notmatch 'Microsoft Basic' } | ForEach-Object { $n=$_.Name.Trim(); $bytes=0; Get-ChildItem -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}' -ErrorAction SilentlyContinue | ForEach-Object { $p=Get-ItemProperty -LiteralPath $_.PSPath -ErrorAction SilentlyContinue; if ($p.DriverDesc -eq $n -and $p.'HardwareInformation.qwMemorySize' -gt $bytes) { $bytes=$p.'HardwareInformation.qwMemorySize' } }; if ($bytes -le 0) { $bytes=$_.AdapterRAM }; $m=[math]::Round($bytes/1GB); if ($m -gt 0) { "$n($($m)G)" } else { $n } }) -join ','))
"DISK=" + ((Get-CimInstance -ClassName Win32_DiskDrive | Where-Object { $_.MediaType -notmatch 'Removable' } | ForEach-Object { $_.SerialNumber.Trim() } | Where-Object { $_ } | Sort-Object -Unique) -join ',')
Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public static class DpiHelper { [DllImport("user32.dll")] public static extern uint GetDpiForSystem(); } public static class MetricsHelper { [DllImport("user32.dll")] public static extern int GetSystemMetrics(int nIndex); }' -ErrorAction SilentlyContinue
$dpi = [DpiHelper]::GetDpiForSystem(); if ($dpi -le 0) { $dpi = 96 }
"DPI=$dpi*$dpi"
"RESOLUTION=" + [MetricsHelper]::GetSystemMetrics(0) + '*' + [MetricsHelper]::GetSystemMetrics(1)
$versions=@()
foreach($base in @('HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes','HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes')){
  if(Test-Path $base){ Get-ChildItem $base | ForEach-Object { $p=Get-ItemProperty $_.PSPath; if($p.Major){ $y=if([int]$p.Major -ge 14){'VC2015-2022'}elseif([int]$p.Major -eq 12){'VC2013'}elseif([int]$p.Major -eq 11){'VC2012'}elseif([int]$p.Major -eq 10){'VC2010'}elseif([int]$p.Major -eq 9){'VC2008'}else{"VC$($p.Major)"}; $v="$($p.Major)"; if($p.Minor){$v+=".$($p.Minor)"}; $s="$y $v"; if($versions -notcontains $s){$versions+=$s} } } }
}
foreach($path in @('HKLM:\SOFTWARE\Microsoft\VisualStudio\10.0\VC\VCRedist\x64','HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\10.0\VC\VCRedist\x64')){ if(Test-Path $path){ $s='VC2010 10.0'; if($versions -notcontains $s){$versions+=$s} } }
"CRUNTIME=" + (($versions | Sort-Object -Descending) -join ';')"#;
    let output = run_output(
        "powershell",
        &["-NoProfile", "-NonInteractive", "-Command", script],
        Duration::from_secs(15),
    );
    parse_key_values(&output)
}

fn parse_key_values(output: &str) -> std::collections::HashMap<String, String> {
    let mut values = std::collections::HashMap::new();
    for line in output.lines() {
        if let Some((key, value)) = line.split_once('=') {
            values.insert(key.trim().to_ascii_uppercase(), value.trim().to_string());
        }
    }
    values
}

#[cfg(target_os = "macos")]
fn darwin_hardware() -> std::collections::HashMap<String, String> {
    let mut values = std::collections::HashMap::new();
    let ioreg = run_output(
        "ioreg",
        &["-rd1", "-c", "IOPlatformExpertDevice"],
        Duration::from_secs(8),
    );
    for line in ioreg.lines() {
        if line.contains("IOPlatformUUID") {
            if let Some((_, value)) = line.split_once('=') {
                values.insert(
                    "UUID".to_string(),
                    value.trim().trim_matches('"').trim().to_string(),
                );
            }
        }
    }

    let profiler = run_output(
        "system_profiler",
        &["SPHardwareDataType"],
        Duration::from_secs(12),
    );
    for line in profiler.lines() {
        let Some((key, value)) = line.split_once(':') else {
            continue;
        };
        let key = key.trim();
        let value = value.trim().to_string();
        if key.eq_ignore_ascii_case("Serial Number (system)") {
            values.insert("SERIAL".to_string(), value);
        } else if key.eq_ignore_ascii_case("Hardware UUID") {
            values.entry("UUID".to_string()).or_insert(value);
        } else if key.eq_ignore_ascii_case("Model Identifier") {
            values.insert("BOARD".to_string(), value);
        }
    }

    values
}

fn is_tunnel_interface(name: &str) -> bool {
    let name = name.to_ascii_lowercase();
    ["tun", "tap", "utun", "awdl", "llw"]
        .iter()
        .any(|prefix| name.starts_with(prefix))
}

fn is_virtual_interface(name: &str) -> bool {
    let name = name.to_ascii_lowercase();
    [
        "lo",
        "docker",
        "veth",
        "vethernet",
        "wsl",
        "br-",
        "virbr",
        "vmnet",
        "tun",
        "tap",
        "utun",
        "awdl",
        "llw",
        "bridge",
        "vboxnet",
        "zt",
        "tailscale",
    ]
    .iter()
    .any(|prefix| name.starts_with(prefix))
}

fn collect_macs() -> String {
    let active: std::collections::HashSet<String> = get_if_addrs::get_if_addrs()
        .unwrap_or_default()
        .into_iter()
        .filter_map(|interface| match interface.addr {
            IfAddr::V4(address) if !address.ip.is_loopback() && !address.ip.is_link_local() => {
                Some(interface.name)
            }
            _ => None,
        })
        .collect();

    let networks = sysinfo::Networks::new_with_refreshed_list();
    let mut macs = Vec::new();
    for (name, data) in networks.list() {
        if is_virtual_interface(name) || !active.contains(name) {
            continue;
        }
        let mac = data.mac_address().to_string();
        if mac.is_empty() || mac == "00:00:00:00:00:00" {
            continue;
        }
        macs.push(mac);
    }

    macs.sort();
    macs.dedup();
    macs.iter()
        .map(|mac| mac.replace(':', "-").to_ascii_uppercase())
        .collect::<Vec<_>>()
        .join(",")
}

/// Returns all non-loopback IPv4 addresses, primary address first.
pub fn local_ips() -> String {
    let primary = local_ip();
    let mut addresses: Vec<String> = if cfg!(target_os = "windows") {
        // get_if_addrs 不暴露网卡状态，会包含已断开局域网口的静态 IP。
        let script = r#"$ErrorActionPreference='SilentlyContinue'
$up = @(Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | ForEach-Object { $_.ifIndex })
Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and $up -contains $_.InterfaceIndex } | ForEach-Object { $_.IPAddress }"#;
        run_output(
            "powershell",
            &["-NoProfile", "-NonInteractive", "-Command", script],
            Duration::from_secs(8),
        )
        .lines()
        .map(str::trim)
        .filter(|value| !value.is_empty())
        .map(str::to_string)
        .collect()
    } else {
        get_if_addrs::get_if_addrs()
            .unwrap_or_default()
            .into_iter()
            .filter(|interface| !is_tunnel_interface(&interface.name))
            .filter_map(|interface| match interface.addr {
                IfAddr::V4(address) if !address.ip.is_loopback() && !address.ip.is_link_local() => {
                    Some(address.ip.to_string())
                }
                _ => None,
            })
            .collect()
    };
    addresses.sort();
    addresses.dedup();
    if !primary.is_empty() {
        addresses.retain(|address| address != &primary);
        addresses.insert(0, primary);
    }

    addresses.join(",")
}

/// Returns the outbound IPv4 address.
pub fn local_ip() -> String {
    if let Ok(socket) = UdpSocket::bind("0.0.0.0:0") {
        if socket.connect("8.8.8.8:80").is_ok() {
            if let Ok(address) = socket.local_addr() {
                let ip = address.ip();
                if !ip.is_loopback() && ip.is_ipv4() {
                    return ip.to_string();
                }
            }
        }
    }

    for interface in get_if_addrs::get_if_addrs().unwrap_or_default() {
        if let IfAddr::V4(address) = interface.addr {
            if !address.ip.is_loopback() {
                return address.ip.to_string();
            }
        }
    }

    String::new()
}

/// Returns the default gateway.
pub fn gateway() -> String {
    #[cfg(target_os = "linux")]
    {
        let data = read_trim("/proc/net/route");
        for line in data.lines().skip(1) {
            let fields: Vec<&str> = line.split_whitespace().collect();
            if fields.len() < 3 || fields[1] != "00000000" {
                continue;
            }
            let Ok(value) = u32::from_str_radix(fields[2], 16) else {
                continue;
            };
            let bytes = [
                (value & 0xFF) as u8,
                ((value >> 8) & 0xFF) as u8,
                ((value >> 16) & 0xFF) as u8,
                ((value >> 24) & 0xFF) as u8,
            ];
            return std::net::Ipv4Addr::from(bytes).to_string();
        }
        return String::new();
    }

    #[cfg(target_os = "windows")]
    {
        let script = r#"$ErrorActionPreference='SilentlyContinue'
$route = Get-NetRoute -DestinationPrefix '0.0.0.0/0' | Sort-Object RouteMetric | Select-Object -First 1
if ($route) {
    $adapter = Get-NetAdapter -InterfaceIndex $route.InterfaceIndex
    if ($adapter -and $adapter.MacAddress) { "$($route.NextHop)/$($adapter.MacAddress)" } else { $route.NextHop }
}"#;
        let output = run_output(
            "powershell",
            &["-NoProfile", "-NonInteractive", "-Command", script],
            Duration::from_secs(8),
        );
        let gateway = output.trim();
        if !gateway.is_empty() {
            return gateway.to_string();
        }

        let output = run_output("route", &["print", "0.0.0.0"], Duration::from_secs(8));
        for line in output.lines() {
            let fields: Vec<&str> = line.split_whitespace().collect();
            if fields.len() >= 3
                && fields[0] == "0.0.0.0"
                && fields[1] == "0.0.0.0"
                && fields[2].parse::<std::net::Ipv4Addr>().is_ok()
            {
                return fields[2].to_string();
            }
        }
        return String::new();
    }

    #[cfg(target_os = "macos")]
    {
        let output = run_output("route", &["-n", "get", "default"], Duration::from_secs(8));
        for line in output.lines() {
            if let Some((key, value)) = line.split_once(':') {
                if key.trim().eq_ignore_ascii_case("gateway") {
                    return value.trim().to_string();
                }
            }
        }
        return String::new();
    }

    #[allow(unreachable_code)]
    String::new()
}

/// Returns the DNS server list.
/// StarServer limits `Node.Dns` to 50 characters; longer values fail node validation.
pub fn dns() -> String {
    #[cfg(target_os = "windows")]
    {
        let output = run_output(
            "powershell",
            &[
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                "(Get-DnsClientServerAddress -AddressFamily IPv4 | ForEach-Object { $_.ServerAddresses } | Sort-Object -Unique) -join ','",
            ],
            Duration::from_secs(8),
        );
        return truncate_dns(output.trim());
    }

    #[cfg(not(target_os = "windows"))]
    {
        let data = read_trim("/etc/resolv.conf");
        let mut servers = Vec::new();
        for line in data.lines() {
            let fields: Vec<&str> = line.split_whitespace().collect();
            if fields.len() >= 2 && fields[0] == "nameserver" && !servers.contains(&fields[1]) {
                servers.push(fields[1]);
            }
        }
        truncate_dns(&servers.join(","))
    }
}

fn truncate_dns(value: &str) -> String {
    const MAX_LEN: usize = 50;
    value.chars().take(MAX_LEN).collect()
}

/// Returns the protocol architecture name.
pub fn architecture() -> &'static str {
    match std::env::consts::ARCH {
        "x86_64" => "X64",
        "x86" => "X86",
        "aarch64" => "Arm64",
        "arm" => "Arm",
        "mips64" | "mips64el" => "Mips64",
        "riscv64" => "RiscV64",
        "loongarch64" => "LA64",
        other => other,
    }
}

/// Returns the hostname.
pub fn host_name() -> String {
    #[cfg(target_os = "linux")]
    {
        let name = read_trim("/etc/hostname");
        if !name.is_empty() {
            return name;
        }
    }

    hostname::get()
        .map(|value| value.to_string_lossy().into_owned())
        .unwrap_or_default()
}

/// Returns the current user name.
pub fn user_name() -> String {
    std::env::var("USER")
        .or_else(|_| std::env::var("USERNAME"))
        .unwrap_or_default()
}

/// Returns the executable directory.
pub fn executable_dir() -> String {
    std::env::current_exe()
        .ok()
        .and_then(|path| path.parent().map(PathBuf::from))
        .map(|path| path.to_string_lossy().into_owned())
        .unwrap_or_default()
}

#[cfg(target_os = "linux")]
fn linux_os_name() -> Option<String> {
    let data = read_trim("/etc/os-release");
    for line in data.lines() {
        if let Some(value) = line.strip_prefix("PRETTY_NAME=") {
            return Some(value.trim().trim_matches('"').to_string());
        }
    }
    for line in data.lines() {
        if let Some(value) = line.strip_prefix("NAME=") {
            return Some(value.trim().trim_matches('"').to_string());
        }
    }
    None
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn cleans_invalid_ids() {
        assert_eq!(clean_id("  1234  "), "1234");
        assert_eq!(clean_id("\"abc\""), "abc");
        assert_eq!(clean_id("None"), "");
        assert_eq!(clean_id("00000000-0000-0000-0000-000000000000"), "");
    }

    #[test]
    fn detects_virtual_interfaces() {
        assert!(is_virtual_interface("docker0"));
        assert!(is_virtual_interface("veth1234"));
        assert!(is_virtual_interface("tailscale0"));
        assert!(!is_virtual_interface("eth0"));
        assert!(!is_virtual_interface("enp3s0"));
    }

    #[test]
    fn architecture_is_known() {
        let arch = architecture();
        assert!(!arch.is_empty());
    }

    #[test]
    fn parses_key_value_output() {
        let values = parse_key_values("UUID=abc\nSERIAL=xyz\n");
        assert_eq!(values.get("UUID").map(String::as_str), Some("abc"));
        assert_eq!(values.get("SERIAL").map(String::as_str), Some("xyz"));
    }

    #[test]
    fn truncates_long_dns_values() {
        assert_eq!(truncate_dns("223.5.5.5"), "223.5.5.5");
        assert_eq!(truncate_dns(&"1".repeat(60)).len(), 50);
    }

    #[test]
    fn time_zone_is_not_empty() {
        let zone = time_zone_name();
        assert!(!zone.is_empty());
        assert_ne!(zone, "Local");
    }

    #[test]
    fn gathers_without_panicking() {
        let info = gather();
        let _ = info.macs;
        assert!(!local_ips().is_empty());
        let system = system_info();
        assert!(!system.os_name.is_empty());
    }
}
