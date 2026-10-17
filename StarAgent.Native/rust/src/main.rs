//! StarAgent native CLI.

use std::path::PathBuf;
use std::process::ExitCode;

use anyhow::{bail, Context, Result};
use clap::{ArgAction, Args, Parser, Subcommand};
use tokio_util::sync::CancellationToken;

use staragent_native::{app, config, service, AGENT_VERSION, BUILD_TIME_TEXT};

#[derive(Parser)]
#[command(
    name = "staragent",
    version = env!("CARGO_PKG_VERSION"),
    about = "StarAgent native agent for StarServer"
)]
struct Cli {
    #[command(subcommand)]
    command: Commands,
}

#[derive(Subcommand)]
enum Commands {
    /// Run the agent (foreground or as a system service)
    Run(RunArgs),
    /// Login, send one heartbeat, process commands and exit
    Once(RunArgs),
    /// Show version information
    Version,
    /// Install as a system service
    Install(InstallArgs),
    /// Remove the system service
    Uninstall,
    /// Start the system service
    Start,
    /// Stop the system service
    Stop,
    /// Show system service status
    Status,
}

#[derive(Args, Clone, Debug)]
struct RunArgs {
    /// Config file path (default: staragent.json)
    #[arg(long)]
    config: Option<PathBuf>,
    /// Override Server
    #[arg(long)]
    server: Option<String>,
    /// Override DataDir
    #[arg(long)]
    data: Option<String>,
    /// Override LogLevel
    #[arg(long = "log-level")]
    log_level: Option<String>,
    /// Enable shell command execution (off by default)
    #[arg(long = "allow-exec")]
    allow_exec: bool,
    /// Mirror logs to stderr
    #[arg(long = "console", default_value_t = true, action = ArgAction::Set)]
    console: bool,
}

impl RunArgs {
    fn config_path(&self) -> PathBuf {
        self.config.clone().unwrap_or_else(config::default_path)
    }

    fn overrides(&self) -> app::RunOverrides {
        app::RunOverrides {
            server: self.server.clone(),
            data_dir: self.data.clone(),
            log_level: self.log_level.clone(),
            allow_exec: self.allow_exec,
            console: self.console,
        }
    }
}

#[derive(Args, Clone, Debug)]
struct InstallArgs {
    /// Config file path
    #[arg(long)]
    config: Option<PathBuf>,
    /// Installation/working directory
    #[arg(long)]
    dir: Option<PathBuf>,
    /// Data directory
    #[arg(long)]
    data: Option<String>,
    /// Service account (systemd User=)
    #[arg(long)]
    user: Option<String>,
    /// Server URL, required when the config file does not exist
    #[arg(long)]
    server: Option<String>,
    /// Log level
    #[arg(long = "log-level")]
    log_level: Option<String>,
}

fn main() -> ExitCode {
    let cli = Cli::parse();
    let result = match cli.command {
        Commands::Version => {
            print_version();
            Ok(())
        }
        Commands::Run(args) => run_agent(args, false),
        Commands::Once(args) => run_agent(args, true),
        Commands::Install(args) => install(args),
        Commands::Uninstall => service::uninstall(),
        Commands::Start => service::start(),
        Commands::Stop => service::stop(),
        Commands::Status => match service::status() {
            Ok(text) => {
                print!("{text}");
                if !text.ends_with('\n') {
                    println!();
                }
                Ok(())
            }
            Err(error) => Err(error),
        },
    };

    match result {
        Ok(()) => ExitCode::SUCCESS,
        Err(error) => {
            eprintln!("staragent: {error:#}");
            ExitCode::from(1)
        }
    }
}

fn run_agent(args: RunArgs, once: bool) -> Result<()> {
    let config_path = args.config_path();
    let overrides = args.overrides();

    #[cfg(windows)]
    if !once {
        let service_args = service::ServiceRunArgs {
            config_path: config_path.clone(),
            overrides: overrides.clone(),
        };
        if service::windows_impl::try_run_service(service_args) {
            return Ok(());
        }
    }

    let runtime = tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .build()
        .context("create async runtime")?;
    staragent_native::init_crypto_provider();

    runtime.block_on(async move {
        let cancel = CancellationToken::new();
        spawn_signal_handler(cancel.clone());
        app::run(config_path, overrides, once, cancel).await
    })
}

fn spawn_signal_handler(cancel: CancellationToken) {
    tokio::spawn(async move {
        #[cfg(unix)]
        {
            use tokio::signal::unix::{signal, SignalKind};
            if let Ok(mut terminate) = signal(SignalKind::terminate()) {
                tokio::select! {
                    _ = tokio::signal::ctrl_c() => {}
                    _ = terminate.recv() => {}
                }
                cancel.cancel();
                return;
            }
        }
        let _ = tokio::signal::ctrl_c().await;
        cancel.cancel();
    });
}

fn install(args: InstallArgs) -> Result<()> {
    let path = args.config.clone().unwrap_or_else(config::default_path);
    let path = if path.is_absolute() {
        path
    } else {
        std::env::current_dir()?.join(path)
    };

    let exists = path.exists();
    if !exists && args.server.is_none() {
        bail!("--server is required when the config file does not exist");
    }

    let mut config = if exists {
        config::load(&path)?
    } else {
        config::Config::default()
    };
    if let Some(server) = &args.server {
        config.server = server.clone();
    }
    if let Some(data) = &args.data {
        config.data_dir = data.clone();
    }
    if let Some(level) = &args.log_level {
        config.log_level = level.clone();
    }
    config.normalize();
    config.validate()?;
    config::save(&path, &config)?;

    let exe = std::env::current_exe().context("resolve executable path")?;
    service::install(service::InstallOptions {
        config_path: path.clone(),
        exe,
        data_dir: PathBuf::from(&config.data_dir),
        install_dir: args.dir,
        service_user: args.user,
    })?;

    println!("StarAgent installed. config={}", path.display());
    Ok(())
}

fn print_version() {
    println!("StarAgent Native Rust");
    println!("Version: {AGENT_VERSION}");
    println!("BuildTime: {BUILD_TIME_TEXT}");
    println!(
        "Runtime: rustc {} {}/{}",
        env!("CARGO_PKG_RUST_VERSION"),
        std::env::consts::OS,
        std::env::consts::ARCH
    );
}
