//! 引导安装：把原生 Agent 升级为 .NET 版 StarAgent。
//!
//! 原生 Agent 只做采集上报，需要完整能力时由运维在节点上执行 `staragent bootstrap`：
//! 下载 .NET 运行时 → 解压安装 → 下载完整版 StarAgent → 解压 → 调用其 `-install`。
//! 解压统一交给系统命令（tar / PowerShell Expand-Archive），不引入额外 crate。

use std::path::{Path, PathBuf};
use std::process::Command;

use anyhow::{bail, Context, Result};
use md5::{Digest, Md5};
use tracing::{info, warn};

use crate::protocol::{BootstrapInfo, BootstrapPackage};

/// 完整版 StarAgent 的安装目录，与官方安装脚本保持一致。
fn agent_dir() -> &'static str {
    if cfg!(windows) {
        r"C:\Program Files\StarAgent"
    } else {
        "/opt/staragent"
    }
}

/// .NET 运行时的安装目录。
#[cfg(unix)]
fn runtime_dir() -> &'static str {
    if cfg!(windows) {
        r"C:\Program Files\dotnet"
    } else if cfg!(target_os = "macos") {
        "/usr/local/share/dotnet"
    } else {
        "/usr/share/dotnet"
    }
}

/// 执行引导安装。成功返回安装的完整版版本号。
pub async fn run(info: &BootstrapInfo, data_dir: &str, default_server: &str) -> Result<String> {
    #[cfg(unix)]
    {
        // 安装到系统目录，必须有 root 权限
        if unsafe { libc::geteuid() } != 0 {
            bail!("bootstrap requires root privileges");
        }
    }

    let dir = Path::new(data_dir).join("bootstrap");
    std::fs::create_dir_all(&dir).with_context(|| format!("create {}", dir.display()))?;

    if !info.runtime.is_empty() {
        let path = download(&info.runtime, &dir).await?;
        install_runtime(&path, file_name(&info.runtime, &path)).await?;
        info!(version = ?info.runtime.version, "bootstrap runtime installed");
    }

    let path = download(&info.agent, &dir).await?;
    let server = info
        .server
        .as_deref()
        .filter(|value| !value.trim().is_empty())
        .unwrap_or(default_server);
    install_agent(&path, file_name(&info.agent, &path), server).await?;

    let version = info.version.clone().unwrap_or_default();
    info!(version, "bootstrap completed");

    Ok(version)
}

/// 安装包文件名：优先用服务端给的 FileName，其次取下载地址最后一段。
fn file_name(package: &BootstrapPackage, path: &Path) -> String {
    package
        .file_name
        .clone()
        .filter(|value| !value.trim().is_empty())
        .unwrap_or_else(|| {
            path.file_name()
                .map(|value| value.to_string_lossy().to_string())
                .unwrap_or_default()
        })
}

/// 下载安装包。服务端目前只保证 MD5，缺少可用哈希时跳过校验并告警。
async fn download(package: &BootstrapPackage, dir: &Path) -> Result<PathBuf> {
    let source = package
        .source
        .as_deref()
        .filter(|value| !value.trim().is_empty())
        .context("package source is empty")?;

    let name = package
        .file_name
        .clone()
        .filter(|value| !value.trim().is_empty())
        .unwrap_or_else(|| {
            source
                .split('?')
                .next()
                .and_then(|value| value.rsplit('/').next())
                .unwrap_or("package.bin")
                .to_string()
        });
    let path = dir.join(name);

    info!(%source, target = %path.display(), "bootstrap download");

    let response = reqwest::Client::builder()
        .timeout(std::time::Duration::from_secs(30 * 60))
        .build()?
        .get(source)
        .send()
        .await
        .context("download")?;
    if !response.status().is_success() {
        bail!("download failed: {}", response.status());
    }

    let bytes = response.bytes().await.context("read package")?;
    std::fs::write(&path, &bytes).with_context(|| format!("write {}", path.display()))?;

    if let Some(hash) = package
        .file_hash
        .as_deref()
        .map(str::trim)
        .filter(|v| !v.is_empty())
    {
        // 服务端历史数据以 MD5（32 位十六进制）为主，其他长度暂不校验
        if hash.len() == 32 {
            let actual = md5_hex(&bytes);
            if !actual.eq_ignore_ascii_case(hash) {
                bail!("hash mismatch: expected {hash} actual {actual}");
            }
        } else {
            warn!(hash, "unsupported hash length, verification skipped");
        }
    }

    Ok(path)
}

/// 计算字节流的 MD5（小写十六进制），用于校验安装包完整性。
fn md5_hex(bytes: &[u8]) -> String {
    let digest = Md5::digest(bytes);
    let mut text = String::with_capacity(32);
    for byte in digest {
        text.push_str(&format!("{byte:02x}"));
    }
    text
}

/// 运行一条 shell 命令，非零退出码视为失败。
fn run_command(name: &str, args: &[&str], dir: Option<&Path>) -> Result<()> {
    let mut command = Command::new(name);
    command.args(args);
    if let Some(dir) = dir {
        command.current_dir(dir);
    }

    let output = command.output().with_context(|| format!("run {name}"))?;
    if !output.status.success() {
        bail!(
            "{name} failed: {:?} {}",
            output.status.code(),
            String::from_utf8_lossy(&output.stderr).trim()
        );
    }

    Ok(())
}

/// .NET 运行时：Windows 走官方 exe 静默安装，其余平台解压 tar.gz 并建软链。
#[cfg(windows)]
async fn install_runtime(path: &Path, _file_name: String) -> Result<()> {
    let status = Command::new(path)
        .args(["/install", "/quiet", "/norestart"])
        .status()
        .context("run runtime installer")?;
    // 3010 表示安装成功但需要重启
    if status.success() || status.code() == Some(3010) {
        return Ok(());
    }
    bail!("runtime installer failed: {:?}", status.code());
}

/// .NET 运行时：解压官方 tar.gz 到安装目录，并建立 /usr/bin/dotnet 软链。
#[cfg(unix)]
async fn install_runtime(path: &Path, file_name: String) -> Result<()> {
    let dir = runtime_dir();
    std::fs::create_dir_all(dir).with_context(|| format!("create {dir}"))?;

    let path_text = path.to_string_lossy().to_string();
    if file_name.to_lowercase().ends_with(".zip") {
        run_command("unzip", &["-o", &path_text, "-d", dir], None)?;
    } else {
        run_command("tar", &["-xzf", &path_text, "-C", dir], None)?;
    }

    let link = "/usr/bin/dotnet";
    let _ = std::fs::remove_file(link);
    std::os::unix::fs::symlink(Path::new(dir).join("dotnet"), link).context("link dotnet")?;

    Ok(())
}

/// 完整版 StarAgent：解压到安装目录，然后调用其自身的 `-install` 注册服务。
async fn install_agent(path: &Path, file_name: String, server: &str) -> Result<()> {
    let dir = agent_dir();
    std::fs::create_dir_all(dir).with_context(|| format!("create {dir}"))?;

    let path_text = path.to_string_lossy().to_string();
    if cfg!(windows) {
        let script =
            format!("Expand-Archive -Force -LiteralPath '{path_text}' -DestinationPath '{dir}'");
        run_command("powershell", &["-NoProfile", "-Command", &script], None)?;
    } else if file_name.to_lowercase().ends_with(".zip") {
        run_command("unzip", &["-o", &path_text, "-d", dir], None)?;
    } else {
        run_command("tar", &["-xzf", &path_text, "-C", dir], None)?;
    }

    run_command(
        "dotnet",
        &["StarAgent.dll", "-install", "-server", server],
        Some(Path::new(dir)),
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn md5_matches_known_vectors() {
        // RFC 1321 测试向量
        assert_eq!(md5_hex(b""), "d41d8cd98f00b204e9800998ecf8427e");
        assert_eq!(md5_hex(b"abc"), "900150983cd24fb0d6963f7d28e17f72");
    }

    #[test]
    fn file_name_prefers_server_value() {
        let package = BootstrapPackage {
            file_name: Some("staragent100.tar.gz".into()),
            ..Default::default()
        };
        assert_eq!(
            file_name(&package, Path::new("/tmp/ignored.bin")),
            "staragent100.tar.gz"
        );
    }

    #[test]
    fn file_name_falls_back_to_downloaded_path() {
        let package = BootstrapPackage::default();
        assert_eq!(
            file_name(&package, Path::new("/tmp/dotnet-runtime.tar.gz")),
            "dotnet-runtime.tar.gz"
        );
    }

    #[test]
    fn package_without_source_is_empty() {
        assert!(BootstrapPackage::default().is_empty());

        let package = BootstrapPackage {
            source: Some("https://example.com/pkg.zip".into()),
            ..Default::default()
        };
        assert!(!package.is_empty());
    }
}
