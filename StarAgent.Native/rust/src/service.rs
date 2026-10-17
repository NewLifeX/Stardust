//! Service installation and control.
//!
//! Linux uses systemd or OpenWrt procd, macOS uses launchd and Windows uses the
//! Service Control Manager through the `windows-service` crate.

use std::path::PathBuf;

use anyhow::Result;

/// Options for `install`.
#[derive(Clone, Debug)]
pub struct InstallOptions {
    /// Absolute config path.
    pub config_path: PathBuf,
    /// Agent executable.
    pub exe: PathBuf,
    /// Data directory (used for macOS log paths).
    pub data_dir: PathBuf,
    /// Optional install/working directory.
    pub install_dir: Option<PathBuf>,
    /// Optional service account (systemd `User=`).
    pub service_user: Option<String>,
}

/// Arguments handed to the Windows service entry point.
#[derive(Clone, Debug)]
pub struct ServiceRunArgs {
    /// Config path.
    pub config_path: PathBuf,
    /// CLI overrides.
    pub overrides: crate::app::RunOverrides,
}

/// Installs the platform service definition.
pub fn install(options: InstallOptions) -> Result<()> {
    imp::install(options)
}

/// Removes the platform service definition.
pub fn uninstall() -> Result<()> {
    imp::uninstall()
}

/// Starts the service.
pub fn start() -> Result<()> {
    imp::start()
}

/// Stops the service.
pub fn stop() -> Result<()> {
    imp::stop()
}

/// Returns service status text.
pub fn status() -> Result<String> {
    imp::status()
}

#[cfg(windows)]
pub use windows_impl as imp;

#[cfg(windows)]
pub mod windows_impl {
    use std::ffi::OsString;
    use std::sync::OnceLock;
    use std::time::Duration;

    use anyhow::{bail, Context, Result};
    use tokio_util::sync::CancellationToken;
    use windows_service::service::{
        ServiceAccess, ServiceControl, ServiceControlAccept, ServiceErrorControl, ServiceExitCode,
        ServiceInfo, ServiceStartType, ServiceState, ServiceStatus, ServiceType,
    };
    use windows_service::service_control_handler::{self, ServiceControlHandlerResult};
    use windows_service::service_manager::{ServiceManager, ServiceManagerAccess};
    use windows_service::{define_windows_service, service_dispatcher};

    use super::{InstallOptions, ServiceRunArgs};

    const SERVICE_NAME: &str = "StarAgentNative";
    const DISPLAY_NAME: &str = "StarAgent Native Agent";

    static SERVICE_ARGS: OnceLock<ServiceRunArgs> = OnceLock::new();

    define_windows_service!(ffi_service_main, service_main);

    /// Attempts to run as a Windows service. Returns false when the process was
    /// not started by the service control manager.
    pub fn try_run_service(args: ServiceRunArgs) -> bool {
        let _ = SERVICE_ARGS.set(args);
        match service_dispatcher::start(SERVICE_NAME, ffi_service_main) {
            Ok(()) => true,
            // 1063 = ERROR_FAILED_SERVICE_CONTROLLER_CONNECT (console start).
            Err(_) => false,
        }
    }

    fn service_main(_arguments: Vec<OsString>) {
        if let Err(error) = run_service() {
            eprintln!("staragent service error: {error:#}");
        }
    }

    fn run_service() -> Result<()> {
        let args = SERVICE_ARGS
            .get()
            .cloned()
            .ok_or_else(|| anyhow::anyhow!("service arguments missing"))?;

        let (stop_tx, stop_rx) = std::sync::mpsc::channel::<()>();
        let event_handler = move |control| -> ServiceControlHandlerResult {
            match control {
                ServiceControl::Stop | ServiceControl::Shutdown => {
                    let _ = stop_tx.send(());
                    ServiceControlHandlerResult::NoError
                }
                ServiceControl::Interrogate => ServiceControlHandlerResult::NoError,
                _ => ServiceControlHandlerResult::NotImplemented,
            }
        };

        let status_handle = service_control_handler::register(SERVICE_NAME, event_handler)?;
        status_handle.set_service_status(ServiceStatus {
            service_type: ServiceType::OWN_PROCESS,
            current_state: ServiceState::Running,
            controls_accepted: ServiceControlAccept::STOP,
            exit_code: ServiceExitCode::Win32(0),
            checkpoint: 0,
            wait_hint: Duration::from_secs(10),
            process_id: None,
        })?;

        let runtime = tokio::runtime::Builder::new_multi_thread()
            .enable_all()
            .build()?;
        let result = runtime.block_on(async move {
            let cancel = CancellationToken::new();
            let cancel_thread = cancel.clone();
            std::thread::spawn(move || {
                let _ = stop_rx.recv();
                cancel_thread.cancel();
            });
            crate::init_crypto_provider();
            crate::app::run(args.config_path, args.overrides, false, cancel).await
        });

        let exit_code = if result.is_ok() { 0 } else { 1 };
        let _ = status_handle.set_service_status(ServiceStatus {
            service_type: ServiceType::OWN_PROCESS,
            current_state: ServiceState::Stopped,
            controls_accepted: ServiceControlAccept::STOP,
            exit_code: ServiceExitCode::Win32(exit_code),
            checkpoint: 0,
            wait_hint: Duration::from_secs(0),
            process_id: None,
        });

        result
    }

    fn manager(access: ServiceManagerAccess) -> Result<ServiceManager> {
        ServiceManager::local_computer(None::<&str>, access).context("open service manager")
    }

    pub fn install(options: InstallOptions) -> Result<()> {
        let mut access = ServiceManagerAccess::CONNECT;
        access |= ServiceManagerAccess::CREATE_SERVICE;
        let manager = manager(access)?;

        let info = ServiceInfo {
            name: OsString::from(SERVICE_NAME),
            display_name: OsString::from(DISPLAY_NAME),
            service_type: ServiceType::OWN_PROCESS,
            start_type: ServiceStartType::AutoStart,
            error_control: ServiceErrorControl::Normal,
            executable_path: options.exe.clone(),
            launch_arguments: vec![
                OsString::from("run"),
                OsString::from("--config"),
                options.config_path.clone().into_os_string(),
            ],
            dependencies: Vec::new(),
            account_name: None,
            account_password: None,
        };

        manager
            .create_service(&info, ServiceAccess::CHANGE_CONFIG)
            .context("create Windows service")?;

        println!("Windows service {SERVICE_NAME} installed");
        Ok(())
    }

    pub fn uninstall() -> Result<()> {
        let manager = manager(ServiceManagerAccess::CONNECT)?;
        let service = manager
            .open_service(
                SERVICE_NAME,
                ServiceAccess::QUERY_STATUS | ServiceAccess::STOP | ServiceAccess::DELETE,
            )
            .context("open Windows service")?;
        let status = service.query_status()?;
        if status.current_state != ServiceState::Stopped {
            let _ = service.stop();
        }
        service.delete().context("delete Windows service")?;
        println!("Windows service {SERVICE_NAME} removed");
        Ok(())
    }

    pub fn start() -> Result<()> {
        let manager = manager(ServiceManagerAccess::CONNECT)?;
        let service = manager
            .open_service(SERVICE_NAME, ServiceAccess::START)
            .context("open Windows service")?;
        service
            .start(&[] as &[OsString])
            .context("start Windows service")?;
        println!("Windows service {SERVICE_NAME} started");
        Ok(())
    }

    pub fn stop() -> Result<()> {
        let manager = manager(ServiceManagerAccess::CONNECT)?;
        let service = manager
            .open_service(SERVICE_NAME, ServiceAccess::STOP)
            .context("open Windows service")?;
        service.stop().context("stop Windows service")?;
        println!("Windows service {SERVICE_NAME} stopped");
        Ok(())
    }

    pub fn status() -> Result<String> {
        let manager = manager(ServiceManagerAccess::CONNECT)?;
        let service = manager
            .open_service(SERVICE_NAME, ServiceAccess::QUERY_STATUS)
            .context("open Windows service")?;
        let status = service.query_status()?;
        if status.current_state == ServiceState::Stopped {
            bail!("service {SERVICE_NAME} is stopped");
        }
        Ok(format!(
            "Windows service {SERVICE_NAME}: {:?}\n",
            status.current_state
        ))
    }
}

#[cfg(unix)]
pub use unix_impl as imp;

#[cfg(unix)]
pub mod unix_impl {
    use std::os::unix::fs::PermissionsExt;
    use std::path::{Path, PathBuf};
    use std::process::Output;

    use anyhow::{bail, Context, Result};

    use super::InstallOptions;

    const SERVICE_NAME: &str = "staragent";
    const LAUNCHD_LABEL: &str = "com.newlifex.staragent";

    fn is_root() -> bool {
        unsafe { libc::geteuid() == 0 }
    }

    fn is_openwrt() -> bool {
        Path::new("/etc/openwrt_release").exists()
    }

    fn is_macos() -> bool {
        cfg!(target_os = "macos")
    }

    fn run(program: &str, args: &[&str]) -> Result<Output> {
        std::process::Command::new(program)
            .args(args)
            .output()
            .with_context(|| format!("run {program} {}", args.join(" ")))
    }

    fn require_success(program: &str, args: &[&str]) -> Result<()> {
        let output = run(program, args)?;
        if !output.status.success() {
            let stderr = String::from_utf8_lossy(&output.stderr);
            bail!("{program} failed: {}", stderr.trim());
        }
        Ok(())
    }

    fn write_file(path: &Path, content: &str, mode: u32) -> Result<()> {
        if let Some(parent) = path.parent() {
            std::fs::create_dir_all(parent)?;
        }
        std::fs::write(path, content).with_context(|| format!("write {}", path.display()))?;
        std::fs::set_permissions(path, std::fs::Permissions::from_mode(mode))
            .with_context(|| format!("chmod {}", path.display()))?;
        Ok(())
    }

    fn escape_xml(value: &str) -> String {
        value
            .replace('&', "&amp;")
            .replace('<', "&lt;")
            .replace('>', "&gt;")
            .replace('"', "&quot;")
            .replace('\'', "&apos;")
    }

    fn systemd_unit(exe: &Path, config: &Path, working_dir: &Path, user: Option<&str>) -> String {
        let mut unit = format!(
            "[Unit]\nDescription=StarAgent Native Agent\nAfter=network-online.target\nWants=network-online.target\n\n[Service]\nType=simple\nExecStart={} run --config {}\nWorkingDirectory={}\nRestart=always\nRestartSec=5\nKillSignal=SIGTERM\nEnvironment=STARAGENT_CONFIG={}\n",
            exe.display(),
            config.display(),
            working_dir.display(),
            config.display(),
        );
        if let Some(user) = user.filter(|value| !value.trim().is_empty()) {
            unit.push_str(&format!("User={}\n", user.trim()));
        }
        unit.push_str("\n[Install]\nWantedBy=multi-user.target\n");
        unit
    }

    fn procd_script(exe: &Path, config: &Path) -> String {
        format!(
            r#"#!/bin/sh /etc/rc.common
USE_PROCD=1
START=95
STOP=10

start_service() {{
    procd_open_instance
    procd_set_param command {exe} run --config {config}
    procd_set_param env STARAGENT_CONFIG={config}
    procd_set_param respawn 3600 5 5
    procd_set_param stdout 1
    procd_set_param stderr 1
    procd_close_instance
}}

stop_service() {{
    :
}}
"#,
            exe = exe.display(),
            config = config.display()
        )
    }

    fn launchd_plist(exe: &Path, config: &Path, data_dir: &Path) -> String {
        let log_dir = data_dir.join("logs");
        format!(
            r#"<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>{label}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{exe}</string>
        <string>run</string>
        <string>--config</string>
        <string>{config}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>{stdout}</string>
    <key>StandardErrorPath</key>
    <string>{stderr}</string>
</dict>
</plist>
"#,
            label = LAUNCHD_LABEL,
            exe = escape_xml(&exe.to_string_lossy()),
            config = escape_xml(&config.to_string_lossy()),
            stdout = escape_xml(&log_dir.join("staragent.out.log").to_string_lossy()),
            stderr = escape_xml(&log_dir.join("staragent.err.log").to_string_lossy()),
        )
    }

    fn launchd_path() -> PathBuf {
        if is_root() {
            PathBuf::from("/Library/LaunchDaemons").join(format!("{LAUNCHD_LABEL}.plist"))
        } else {
            let home = std::env::var("HOME").unwrap_or_else(|_| ".".to_string());
            PathBuf::from(home)
                .join("Library")
                .join("LaunchAgents")
                .join(format!("{LAUNCHD_LABEL}.plist"))
        }
    }

    pub fn install(options: InstallOptions) -> Result<()> {
        let working_dir = options
            .install_dir
            .clone()
            .or_else(|| options.exe.parent().map(PathBuf::from))
            .unwrap_or_else(|| PathBuf::from("."));

        if is_macos() {
            let plist = launchd_path();
            let content = launchd_plist(&options.exe, &options.config_path, &options.data_dir);
            if let Some(parent) = plist.parent() {
                std::fs::create_dir_all(parent)?;
            }
            let log_dir = options.data_dir.join("logs");
            std::fs::create_dir_all(&log_dir)?;
            write_file(&plist, &content, 0o644)?;
            println!("launchd plist written to {}", plist.display());
            return Ok(());
        }

        if !is_root() {
            bail!("安装系统服务需要 root 权限（sudo）");
        }

        if is_openwrt() {
            let path = PathBuf::from("/etc/init.d").join(SERVICE_NAME);
            write_file(
                &path,
                &procd_script(&options.exe, &options.config_path),
                0o755,
            )?;
            require_success(&path.to_string_lossy(), &["enable"])?;
            println!("OpenWrt procd service written to {}", path.display());
            return Ok(());
        }

        let path = PathBuf::from("/etc/systemd/system").join(format!("{SERVICE_NAME}.service"));
        write_file(
            &path,
            &systemd_unit(
                &options.exe,
                &options.config_path,
                &working_dir,
                options.service_user.as_deref(),
            ),
            0o644,
        )?;
        require_success("systemctl", &["daemon-reload"])?;
        require_success("systemctl", &["enable", "staragent.service"])?;
        println!("systemd unit written to {}", path.display());
        Ok(())
    }

    pub fn uninstall() -> Result<()> {
        if is_macos() {
            let plist = launchd_path();
            let _ = run("launchctl", &["unload", "-w", &plist.to_string_lossy()]);
            if plist.exists() {
                std::fs::remove_file(&plist)?;
            }
            println!("launchd plist removed");
            return Ok(());
        }

        if !is_root() {
            bail!("卸载系统服务需要 root 权限（sudo）");
        }

        if is_openwrt() {
            let path = "/etc/init.d/staragent";
            let _ = run(path, &["stop"]);
            let _ = run(path, &["disable"]);
            let _ = std::fs::remove_file(path);
            println!("OpenWrt procd service removed");
            return Ok(());
        }

        let _ = run("systemctl", &["stop", "staragent.service"]);
        let _ = run("systemctl", &["disable", "staragent.service"]);
        let path = "/etc/systemd/system/staragent.service";
        if Path::new(path).exists() {
            std::fs::remove_file(path)?;
        }
        let _ = run("systemctl", &["daemon-reload"]);
        println!("systemd unit removed");
        Ok(())
    }

    pub fn start() -> Result<()> {
        if is_macos() {
            let plist = launchd_path();
            require_success("launchctl", &["load", "-w", &plist.to_string_lossy()])?;
            println!("launchd service loaded");
            return Ok(());
        }
        if is_openwrt() {
            require_success("/etc/init.d/staragent", &["start"])?;
            println!("staragent started");
            return Ok(());
        }
        require_success("systemctl", &["start", "staragent.service"])?;
        println!("staragent started");
        Ok(())
    }

    pub fn stop() -> Result<()> {
        if is_macos() {
            let plist = launchd_path();
            require_success("launchctl", &["unload", "-w", &plist.to_string_lossy()])?;
            println!("launchd service unloaded");
            return Ok(());
        }
        if is_openwrt() {
            require_success("/etc/init.d/staragent", &["stop"])?;
            println!("staragent stopped");
            return Ok(());
        }
        require_success("systemctl", &["stop", "staragent.service"])?;
        println!("staragent stopped");
        Ok(())
    }

    pub fn status() -> Result<String> {
        if is_macos() {
            let output = run("launchctl", &["list", LAUNCHD_LABEL])?;
            if output.status.success() {
                return Ok(String::from_utf8_lossy(&output.stdout).into_owned());
            }
            return Ok(format!("launchd service {LAUNCHD_LABEL} is not running\n"));
        }
        if is_openwrt() {
            let output = run("/etc/init.d/staragent", &["status"])?;
            return Ok(format!(
                "{}{}",
                String::from_utf8_lossy(&output.stdout),
                String::from_utf8_lossy(&output.stderr)
            ));
        }
        let output = run("systemctl", &["status", "staragent.service", "--no-pager"])?;
        Ok(format!(
            "{}{}",
            String::from_utf8_lossy(&output.stdout),
            String::from_utf8_lossy(&output.stderr)
        ))
    }

    #[cfg(test)]
    mod tests {
        use super::*;

        #[test]
        fn unit_contains_required_settings() {
            let unit = systemd_unit(
                Path::new("/opt/staragent/staragent"),
                Path::new("/opt/staragent/staragent.json"),
                Path::new("/opt/staragent"),
                Some("staragent"),
            );
            assert!(unit.contains("Restart=always"));
            assert!(unit.contains("KillSignal=SIGTERM"));
            assert!(unit.contains("User=staragent"));
            assert!(unit.contains("ExecStart=/opt/staragent/staragent run --config"));
        }

        #[test]
        fn procd_script_has_lifecycle() {
            let script = procd_script(
                Path::new("/usr/bin/staragent"),
                Path::new("/etc/staragent.json"),
            );
            assert!(script.contains("USE_PROCD=1"));
            assert!(script.contains("start_service()"));
            assert!(script.contains("stop_service()"));
            assert!(script.contains("procd_set_param respawn"));
        }

        #[test]
        fn plist_escapes_xml() {
            let plist = launchd_plist(
                Path::new("/opt/a&b/staragent"),
                Path::new("/opt/a&b/staragent.json"),
                Path::new("/opt/a&b"),
            );
            assert!(plist.contains("a&amp;b"));
            assert!(plist.contains("<key>KeepAlive</key>"));
        }
    }
}

#[cfg(not(any(unix, windows)))]
mod imp {
    use anyhow::{bail, Result};

    use super::InstallOptions;

    pub fn install(_options: InstallOptions) -> Result<()> {
        bail!("当前平台不支持服务安装")
    }

    pub fn uninstall() -> Result<()> {
        bail!("当前平台不支持服务卸载")
    }

    pub fn start() -> Result<()> {
        bail!("当前平台不支持服务启动")
    }

    pub fn stop() -> Result<()> {
        bail!("当前平台不支持服务停止")
    }

    pub fn status() -> Result<String> {
        bail!("当前平台不支持服务状态查询")
    }
}
