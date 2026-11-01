//! StarAgent native CLI.

use std::path::PathBuf;
use std::process::ExitCode;

use anyhow::{Context, Result};
use clap::{ArgAction, Args, Parser, Subcommand};
use tokio_util::sync::CancellationToken;

use staragent_native::{app, config, AGENT_VERSION, BUILD_TIME_TEXT};

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
    /// Run the agent in the foreground
    Run(RunArgs),
    /// Login, send one heartbeat, process commands and exit
    Once(RunArgs),
    /// Install the .NET runtime and the full .NET StarAgent (requires root)
    Bootstrap(RunArgs),
    /// Show version information
    Version,
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

fn main() -> ExitCode {
    let cli = Cli::parse();
    let result = match cli.command {
        Commands::Version => {
            print_version();
            Ok(())
        }
        Commands::Run(args) => run_agent(args, app::RunMode::Run),
        Commands::Once(args) => run_agent(args, app::RunMode::Once),
        Commands::Bootstrap(args) => run_agent(args, app::RunMode::Bootstrap),
    };

    match result {
        Ok(()) => ExitCode::SUCCESS,
        Err(error) => {
            eprintln!("staragent: {error:#}");
            ExitCode::from(1)
        }
    }
}

fn run_agent(args: RunArgs, mode: app::RunMode) -> Result<()> {
    let config_path = args.config_path();
    let overrides = args.overrides();

    let runtime = tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .build()
        .context("create async runtime")?;
    staragent_native::init_crypto_provider();

    runtime.block_on(async move {
        let cancel = CancellationToken::new();
        spawn_signal_handler(cancel.clone());
        app::run(config_path, overrides, mode, cancel).await
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
