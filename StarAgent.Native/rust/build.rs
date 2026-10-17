use chrono::Utc;

fn main() {
    println!("cargo:rerun-if-changed=build.rs");
    println!("cargo:rerun-if-env-changed=STARAGENT_VERSION");

    let version =
        std::env::var("STARAGENT_VERSION").unwrap_or_else(|_| "1.0.2026.1017".to_string());
    let build_time = Utc::now().format("%Y-%m-%d %H:%M:%S").to_string();

    println!("cargo:rustc-env=STARAGENT_VERSION={version}");
    println!("cargo:rustc-env=STARAGENT_BUILD_TIME={build_time}");
}
