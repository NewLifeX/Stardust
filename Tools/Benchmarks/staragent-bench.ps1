<#
.SYNOPSIS
StarAgent 启动性能基准：同一台机器上对比 .NET 通用/JIT/R2R/AOT 与 Go/Rust/C++ 原生实现。

.DESCRIPTION
1. 构建（可选）：发布 .NET 四种形态，交叉编译 Go，构建 Windows 上的 Rust/C++，并可在 Linux 目标机上构建 Rust/C++。
2. 部署：把每个变体推到目标机独立目录（Windows 本机、WSL、SSH Linux）。
3. 采样：每个变体预热一次，再运行 N 次，记录墙钟、CPU、峰值 RSS。
4. 汇总：输出原始 CSV 与 Markdown 汇总表。

.PARAMETER Iterations
每个变体的空载/完整初始化采样次数，默认取 machines.json 的 iterations（15）。

.PARAMETER RunIterations
完整生命周期（登录 → 首次心跳）每个变体的采样次数，默认 3。

.PARAMETER Modes
测量模式：empty（空载 --version）、full（完整初始化 -status）、run（真实服务端登录 + 首次心跳）。

.PARAMETER AgentServer
完整生命周期使用的星尘服务端，默认 http://star.newlifex.com:6600。

.PARAMETER Machines
只测指定机器（名称匹配 machines.json），默认全部。

.PARAMETER SkipBuild
跳过构建，直接使用 .tmp/bench/artifacts 下已有产物。

.PARAMETER SkipNativeLinuxBuild
跳过在 Linux 目标机上构建 Rust/C++，使用已有原生二进制。

.PARAMETER SkipDeploy
跳过部署（目标机目录内容保持不变）。

.PARAMETER SkipRun
只构建与部署，不运行采样。

.PARAMETER ResultDir
结果输出目录，默认 .tmp/bench/results。

.EXAMPLE
pwsh Tools/Benchmarks/staragent-bench.ps1

.EXAMPLE
pwsh Tools/Benchmarks/staragent-bench.ps1 -Machines A2,sh14.newlifex.com -Iterations 10

.EXAMPLE
pwsh Tools/Benchmarks/staragent-bench.ps1 -Modes run -RunIterations 3 -SkipBuild -SkipDeploy
#>
[CmdletBinding()]
param(
    [String]$RepoRoot = '',
    [Int]$Iterations = 0,
    [Int]$RunIterations = 3,
    [String[]]$Machines = @(),
    [ValidateSet('empty', 'full', 'run')]
    [String[]]$Modes = @('empty', 'full'),
    [String]$AgentServer = 'http://star.newlifex.com:6600',
    [Switch]$SkipBuild,
    [Switch]$SkipNativeLinuxBuild,
    [Switch]$SkipDeploy,
    [Switch]$SkipRun,
    [String]$ResultDir = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $RepoRoot) { $RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)

$BenchRoot = Join-Path $RepoRoot '.tmp/bench'
$ArtifactRoot = Join-Path $BenchRoot 'artifacts'
$PublishRoot = Join-Path $BenchRoot 'publish'
$PackRoot = Join-Path $BenchRoot 'pack'
$SourceRoot = Join-Path $BenchRoot 'src'
$RemoteRoot = Join-Path $BenchRoot 'remote'
if (-not $ResultDir) { $ResultDir = Join-Path $BenchRoot 'results' }
$ResultDir = [System.IO.Path]::GetFullPath($ResultDir)

foreach ($dir in $ArtifactRoot, $PublishRoot, $PackRoot, $SourceRoot, $RemoteRoot, $ResultDir) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

$Config = Get-Content (Join-Path $PSScriptRoot 'machines.json') -Raw | ConvertFrom-Json
if ($Iterations -le 0) { $Iterations = [Int]$Config.iterations }

$AllMachines = @($Config.machines)
if ($Machines.Count -gt 0) {
    $AllMachines = @($AllMachines | Where-Object { $Machines -contains $_.name })
    if ($AllMachines.Count -eq 0) { throw "没有匹配的机器：$($Machines -join ', ')" }
}

$Timestamps = Get-Date -Format 'yyyyMMdd-HHmmss'
$RawCsv = Join-Path $ResultDir "raw-$Timestamps.csv"
$SummaryMd = Join-Path $ResultDir "summary-$Timestamps.md"

# variant 定义：Dir 中的 {rid} 会替换为机器 RID。
$VariantDefs = @(
    [pscustomobject]@{ Id = 'dotnet-universal'; Label = '.NET 通用(JIT)'; Kind = 'dotnet'; Dir = 'dotnet-universal'; Rids = @('win-x64', 'linux-x64', 'linux-arm64'); Args = @('--version', '-status') },
    [pscustomobject]@{ Id = 'dotnet-jit'; Label = '.NET JIT 自包含'; Kind = 'dotnet'; Dir = 'dotnet-jit-{rid}'; Rids = @('win-x64', 'linux-x64', 'linux-arm64'); Args = @('--version', '-status') },
    [pscustomobject]@{ Id = 'dotnet-r2r'; Label = '.NET R2R 自包含'; Kind = 'dotnet'; Dir = 'dotnet-r2r-{rid}'; Rids = @('win-x64', 'linux-x64', 'linux-arm64'); Args = @('--version', '-status') },
    [pscustomobject]@{ Id = 'dotnet-aot'; Label = '.NET AOT'; Kind = 'dotnet'; Dir = 'dotnet-aot-{rid}'; Rids = @('win-x64', 'linux-x64', 'linux-arm64'); Args = @('--version', '-status') },
    [pscustomobject]@{ Id = 'go'; Label = 'Go 原生'; Kind = 'native'; Dir = 'go-{rid}'; Rids = @('win-x64', 'linux-x64', 'linux-arm64'); Args = @('version') },
    [pscustomobject]@{ Id = 'rust'; Label = 'Rust 原生'; Kind = 'native'; Dir = 'rust-{rid}'; Rids = @('win-x64', 'linux-x64', 'linux-arm64'); Args = @('version') },
    [pscustomobject]@{ Id = 'cpp'; Label = 'C++ 原生'; Kind = 'native'; Dir = 'cpp-{rid}'; Rids = @('win-x64', 'linux-x64', 'linux-arm64'); Args = @('version') }
)

function Get-VariantDir([Object]$Variant, [String]$Rid) {
    $dir = $Variant.Dir.Replace('{rid}', $Rid)
    return Join-Path $ArtifactRoot $dir
}

function Get-ApplicableVariants([Object]$Machine) {
    return @($VariantDefs | Where-Object { $_.Rids -contains $Machine.rid })
}

function Invoke-DotNet([String[]]$Arguments) {
    Write-Host "  dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet 失败：$($Arguments -join ' ')" }
}

function Publish-DotNetVariants {
    Write-Host '== 发布 .NET 变体'
    $proj = Join-Path $RepoRoot 'StarAgent/StarAgent.csproj'
    $common = @('publish', $proj, '-c', 'Release', '-f', 'net10.0', '-p:TargetFrameworks=net10.0')

    Invoke-DotNet ($common + @('-o', (Join-Path $ArtifactRoot 'dotnet-universal')))

    foreach ($rid in 'win-x64', 'linux-x64', 'linux-arm64') {
        Invoke-DotNet ($common + @('-r', $rid, '--self-contained', 'true', '-p:PublishReadyToRun=false', '-o', (Join-Path $ArtifactRoot "dotnet-jit-$rid")))
        Invoke-DotNet ($common + @('-r', $rid, '--self-contained', 'true', '-p:PublishReadyToRun=true', '-o', (Join-Path $ArtifactRoot "dotnet-r2r-$rid")))
    }

    # AOT：Windows 本机构建 win-x64；Linux 在 WSL 中构建，避免跨 OS 交叉编译。
    Invoke-DotNet ($common + @('-r', 'win-x64', '--self-contained', 'true', '-p:StarAgentAot=true', '-p:StripSymbols=true', '-o', (Join-Path $ArtifactRoot 'dotnet-aot-win-x64')))

    $repoWsl = ConvertTo-WslPath $RepoRoot
    $outWsl = ConvertTo-WslPath $ArtifactRoot
    foreach ($rid in 'linux-x64', 'linux-arm64') {
        $objcopy = if ($rid -eq 'linux-arm64') { '-p:ObjCopyName=aarch64-linux-gnu-objcopy' } else { '' }
        $cmd = "cd '$repoWsl' && dotnet publish StarAgent/StarAgent.csproj -c Release -f net10.0 -r $rid --self-contained true -p:TargetFrameworks=net10.0 -p:StarAgentAot=true -p:StripSymbols=true -p:CppCompilerAndLinker=clang -p:LinkerFlavor=lld $objcopy -o '$outWsl/dotnet-aot-$rid'"
        Write-Host "  wsl: $cmd"
        & wsl -e bash -c $cmd
        if ($LASTEXITCODE -ne 0) { throw "WSL AOT 发布失败：$rid" }
    }
}

function Build-GoVariants {
    Write-Host '== 构建 Go 原生'
    $goSrc = Join-Path $RepoRoot 'StarAgent.Native/go'
    $old = @{ GOOS = $env:GOOS; GOARCH = $env:GOARCH; CGO_ENABLED = $env:CGO_ENABLED }
    try {
        $env:CGO_ENABLED = '0'
        foreach ($t in @(@('windows', 'amd64', 'win-x64', '.exe'), @('linux', 'amd64', 'linux-x64', ''), @('linux', 'arm64', 'linux-arm64', ''))) {
            $env:GOOS = $t[0]
            $env:GOARCH = $t[1]
            $outDir = Join-Path $ArtifactRoot "go-$($t[2])"
            New-Item -ItemType Directory -Path $outDir -Force | Out-Null
            Write-Host "  go build $($t[0])/$($t[1])"
            Push-Location $goSrc
            try {
                & go build -trimpath -ldflags '-s -w' -o (Join-Path $outDir "staragent$($t[3])") ./cmd/staragent
                if ($LASTEXITCODE -ne 0) { throw "Go 构建失败：$($t[2])" }
            }
            finally { Pop-Location }
        }
    }
    finally {
        $env:GOOS = $old.GOOS
        $env:GOARCH = $old.GOARCH
        $env:CGO_ENABLED = $old.CGO_ENABLED
    }
}

function Find-CMake {
    $candidates = @(
        (Get-Command cmake -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        'C:\vs2026\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
    ) | Where-Object { $_ -and (Test-Path $_) }
    if ($candidates.Count -eq 0) { return $null }
    return @($candidates)[0]
}

function Build-WindowsNative {
    Write-Host '== 构建 Windows 原生（Rust/C++）'

    $cargo = Get-Command cargo -ErrorAction SilentlyContinue
    if ($cargo) {
        Push-Location (Join-Path $RepoRoot 'StarAgent.Native/rust')
        try {
            & cargo build --release
            if ($LASTEXITCODE -ne 0) { throw 'Rust 构建失败' }
            $outDir = Join-Path $ArtifactRoot 'rust-win-x64'
            New-Item -ItemType Directory -Path $outDir -Force | Out-Null
            Copy-Item 'target/release/staragent.exe' (Join-Path $outDir 'staragent.exe') -Force
        }
        finally { Pop-Location }
    }
    else {
        Write-Warning '未找到 cargo，跳过 Windows Rust 构建'
    }

    $cmake = Find-CMake
    if ($cmake) {
        $cpp = Join-Path $RepoRoot 'StarAgent.Native/cpp'
        $build = Join-Path $BenchRoot 'cpp-win'
        $ix = Join-Path $cpp 'build/_deps/ixwebsocket-src'
        $cmakeArgs = @('-S', $cpp, '-B', $build, '-A', 'x64', '-DSTARAGENT_WITH_WS=ON', '-DSTARAGENT_WITH_TLS=OFF')
        if (Test-Path $ix) { $cmakeArgs += "-DFETCHCONTENT_SOURCE_DIR_IXWEBSOCKET=$($ix.Replace('\', '/'))" }
        & $cmake @cmakeArgs
        if ($LASTEXITCODE -ne 0) { throw 'C++ configure 失败' }
        & $cmake --build $build --config Release --parallel
        if ($LASTEXITCODE -ne 0) { throw 'C++ 构建失败' }
        $outDir = Join-Path $ArtifactRoot 'cpp-win-x64'
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
        Copy-Item (Join-Path $build 'Release/staragent.exe') (Join-Path $outDir 'staragent.exe') -Force
    }
    else {
        Write-Warning '未找到 cmake，跳过 Windows C++ 构建'
    }
}

function Write-TextFile([String]$Path, [String]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function ConvertTo-WslPath([String]$Path) {
    if ($Path.StartsWith('/')) { return $Path }
    $full = [System.IO.Path]::GetFullPath($Path)
    $drive = $full.Substring(0, 1).ToLowerInvariant()
    return "/mnt/$drive" + $full.Substring(2).Replace('\', '/')
}

function Build-NativeLinux {
    Write-Host '== 在 Linux 目标机构建 Rust/C++'

    $rustTar = Join-Path $SourceRoot 'rust.tar'
    $cppTar = Join-Path $SourceRoot 'cpp.tar'
    & git -C $RepoRoot archive --format=tar -o $rustTar HEAD:StarAgent.Native/rust
    & git -C $RepoRoot archive --format=tar -o $cppTar HEAD:StarAgent.Native/cpp
    if ($LASTEXITCODE -ne 0) { throw 'git archive 失败' }

    $ixTgz = Join-Path $SourceRoot 'ixwebsocket.tgz'
    $ixSrc = Join-Path $RepoRoot 'StarAgent.Native/cpp/build/_deps/ixwebsocket-src'
    if (Test-Path $ixSrc) {
        & tar.exe -czf $ixTgz -C (Split-Path -Parent $ixSrc) (Split-Path -Leaf $ixSrc)
        if ($LASTEXITCODE -ne 0) { throw '打包 ixwebsocket 失败' }
    }

    $buildHosts = @(
        [pscustomobject]@{ Host = 'root@sh14.newlifex.com'; Rid = 'linux-x64'; Jobs = 2; Parallel = 3 },
        [pscustomobject]@{ Host = 'root@10.0.0.10'; Rid = 'linux-arm64'; Jobs = 1; Parallel = 1 }
    )

    $remoteScript = @'
set -e
if [ ! -x "$HOME/.cargo/bin/cargo" ]; then
  export RUSTUP_DIST_SERVER=https://rsproxy.cn RUSTUP_UPDATE_ROOT=https://rsproxy.cn/rustup
  curl --proto '=https' --tlsv1.2 -sSf https://rsproxy.cn/rustup-init.sh | sh -s -- -y --profile minimal --default-toolchain stable
fi
mkdir -p "$HOME/.cargo"
if ! grep -q rsproxy "$HOME/.cargo/config.toml" 2>/dev/null; then
  printf '%s\n' '[source.crates-io]' 'replace-with = "rsproxy-sparse"' '' '[source.rsproxy-sparse]' 'registry = "sparse+https://rsproxy.cn/index/"' '' '[net]' 'git-fetch-with-cli = true' > "$HOME/.cargo/config.toml"
fi
. "$HOME/.cargo/env"
if ! command -v cmake >/dev/null 2>&1; then
  if command -v dnf >/dev/null 2>&1; then dnf install -y cmake; else apt-get update -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq cmake; fi
fi
rm -rf /root/bench/build /root/bench/src && mkdir -p /root/bench/build /root/bench/src/rust /root/bench/src/cpp
tar -xf /root/rust.tar -C /root/bench/src/rust
tar -xf /root/cpp.tar -C /root/bench/src/cpp
if [ -f /root/ixwebsocket.tgz ]; then tar -xzf /root/ixwebsocket.tgz -C /root/bench/src; fi

cd /root/bench/src/rust
CARGO_BUILD_JOBS=__JOBS__ cargo build --release --locked
cp target/release/staragent /root/bench/build/rust-staragent

cd /root/bench/src/cpp
IX_ARGS=""
if [ -d /root/bench/src/ixwebsocket-src ]; then IX_ARGS="-DFETCHCONTENT_SOURCE_DIR_IXWEBSOCKET=/root/bench/src/ixwebsocket-src"; fi
cmake -S . -B build -DCMAKE_BUILD_TYPE=Release $IX_ARGS -DSTARAGENT_WITH_WS=ON -DSTARAGENT_WITH_TLS=OFF > /tmp/bench-cpp-cmake.log 2>&1
cmake --build build --parallel __PARALLEL__ > /tmp/bench-cpp-build.log 2>&1
cp build/staragent /root/bench/build/cpp-staragent
ls -l /root/bench/build
'@

    foreach ($bh in $buildHosts) {
        Write-Host "  -> $($bh.Host)"
        & scp -q $rustTar $cppTar "$($bh.Host):/root/"
        if (Test-Path $ixTgz) { & scp -q $ixTgz "$($bh.Host):/root/" }
        $script = $remoteScript.Replace('__JOBS__', $bh.Jobs).Replace('__PARALLEL__', $bh.Parallel) -replace "`r`n", "`n"
        $scriptFile = Join-Path $SourceRoot "build-native-$($bh.Rid).sh"
        Write-TextFile $scriptFile $script
        & scp -q $scriptFile "$($bh.Host):/root/build-native.sh"
        & ssh $bh.Host 'bash /root/build-native.sh'
        if ($LASTEXITCODE -ne 0) { throw "远程构建失败：$($bh.Host)" }

        $outDir = Join-Path $ArtifactRoot "rust-$($bh.Rid)"
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
        & scp -q "$($bh.Host):/root/bench/build/rust-staragent" (Join-Path $outDir 'staragent')
        if ($LASTEXITCODE -ne 0) { throw "下载 Rust 失败：$($bh.Rid)" }

        $outDir = Join-Path $ArtifactRoot "cpp-$($bh.Rid)"
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
        & scp -q "$($bh.Host):/root/bench/build/cpp-staragent" (Join-Path $outDir 'staragent')
        if ($LASTEXITCODE -ne 0) { throw "下载 C++ 失败：$($bh.Rid)" }
    }
}

function Build-BenchRunner {
    Write-Host '== 编译测量小程序 bench-run / bench-heartbeat'
    $src = Join-Path $PSScriptRoot 'bench-run.c'
    $srcHb = Join-Path $PSScriptRoot 'bench-heartbeat.c'
    $outX64 = Join-Path $BenchRoot 'bench-run-linux-x64'
    $outHbX64 = Join-Path $BenchRoot 'bench-heartbeat-linux-x64'
    $srcWsl = ConvertTo-WslPath $src
    $srcHbWsl = ConvertTo-WslPath $srcHb
    $outWsl = ConvertTo-WslPath $outX64
    $outHbWsl = ConvertTo-WslPath $outHbX64
    & wsl -e bash -c "gcc -O2 -static -o '$outWsl' '$srcWsl' && gcc -O2 -static -o '$outHbWsl' '$srcHbWsl'"
    if ($LASTEXITCODE -ne 0) {
        Write-Warning 'WSL 静态编译失败，将在各目标机上分别编译'
        $outX64 = $null
    }

    $armHost = 'root@10.0.0.10'
    & scp -q $src "${armHost}:/root/bench-run.c"
    & scp -q $srcHb "${armHost}:/root/bench-heartbeat.c"
    & ssh $armHost 'gcc -O2 -o /root/bench-run /root/bench-run.c && gcc -O2 -o /root/bench-heartbeat /root/bench-heartbeat.c'
    if ($LASTEXITCODE -ne 0) { throw 'A2 编译测量程序失败' }
    & scp -q "${armHost}:/root/bench-run" (Join-Path $BenchRoot 'bench-run-linux-arm64')
    & scp -q "${armHost}:/root/bench-heartbeat" (Join-Path $BenchRoot 'bench-heartbeat-linux-arm64')

    return $outX64
}

function Deploy-LinuxPackage([Object]$Machine, [String]$Name, [String]$LocalDir) {
    $tgz = Join-Path $PackRoot "$Name-$($Machine.rid).tgz"
    & tar.exe -czf $tgz -C $LocalDir .
    if ($LASTEXITCODE -ne 0) { throw "打包失败：$LocalDir" }

    $target = "$($Machine.workDir)/$Name"
    if ($Machine.kind -eq 'wsl') {
        $wslTgz = ConvertTo-WslPath $tgz
        & wsl -e bash -c "mkdir -p '$target' && tar -xzf '$wslTgz' -C '$target' && chmod +x '$target/StarAgent' '$target/staragent' 2>/dev/null || true"
        if ($LASTEXITCODE -ne 0) { throw "WSL 解包失败：$Name" }
    }
    else {
        & scp -q $tgz "$($Machine.host):/tmp/bench-package.tgz"
        if ($LASTEXITCODE -ne 0) { throw "上传失败：$($Machine.name) $Name" }
        & ssh $Machine.host "mkdir -p '$target' && tar -xzf /tmp/bench-package.tgz -C '$target' && rm -f /tmp/bench-package.tgz; chmod +x '$target/StarAgent' '$target/staragent' 2>/dev/null || true"
        if ($LASTEXITCODE -ne 0) { throw "解包失败：$($Machine.name) $Name" }
    }
}

function Deploy-Variants([Object]$Machine) {
    $variants = Get-ApplicableVariants $Machine
    if ($Machine.kind -eq 'windows') {
        foreach ($v in $variants) {
            $src = Get-VariantDir $v $Machine.rid
            $dst = Join-Path $Machine.workDir $v.Id
            New-Item -ItemType Directory -Path $dst -Force | Out-Null
            Copy-Item (Join-Path $src '*') $dst -Recurse -Force
            Write-Host "  windows $($v.Id) -> $dst"
        }
        return
    }

    foreach ($v in $variants) {
        $src = Get-VariantDir $v $Machine.rid
        if (-not (Test-Path $src)) { Write-Warning "缺少产物 $($v.Id)/$($Machine.rid)：$src"; continue }
        Write-Host "  $($Machine.name) $($v.Id)"
        Deploy-LinuxPackage $Machine $v.Id $src
    }

    # 编译/上传 bench-run、bench-heartbeat 与 runner 脚本
    $runner = Join-Path $PSScriptRoot 'linux-runner.sh'
    $helpers = @(
        @{ Name = 'bench-run'; Source = 'bench-run.c' },
        @{ Name = 'bench-heartbeat'; Source = 'bench-heartbeat.c' }
    )
    if ($Machine.kind -eq 'wsl') {
        $wslRoot = ConvertTo-WslPath $Machine.workDir
        $wslRunner = ConvertTo-WslPath $runner
        & wsl -e bash -c "mkdir -p '$wslRoot' && cp '$wslRunner' '$wslRoot/linux-runner.sh' && chmod +x '$wslRoot/linux-runner.sh'"
        foreach ($h in $helpers) {
            $helper = Join-Path $BenchRoot "$($h.Name)-linux-x64"
            if (Test-Path $helper) {
                $wslHelper = ConvertTo-WslPath $helper
                & wsl -e bash -c "cp '$wslHelper' '$wslRoot/$($h.Name)' && chmod +x '$wslRoot/$($h.Name)'"
            }
            else {
                $wslSrc = ConvertTo-WslPath (Join-Path $PSScriptRoot $h.Source)
                & wsl -e bash -c "gcc -O2 -o '$wslRoot/$($h.Name)' '$wslSrc'"
            }
        }
    }
    else {
        & scp -q $runner "$($Machine.host):$($Machine.workDir)/linux-runner.sh"
        & ssh $Machine.host "chmod +x '$($Machine.workDir)/linux-runner.sh'"
        $rid = if ($Machine.rid -eq 'linux-arm64') { 'linux-arm64' } else { 'linux-x64' }
        foreach ($h in $helpers) {
            $helper = Join-Path $BenchRoot "$($h.Name)-$rid"
            if (Test-Path $helper) {
                & scp -q $helper "$($Machine.host):$($Machine.workDir)/$($h.Name)"
                & ssh $Machine.host "chmod +x '$($Machine.workDir)/$($h.Name)'"
            }
            else {
                & scp -q (Join-Path $PSScriptRoot $h.Source) "$($Machine.host):$($Machine.workDir)/$($h.Name).c"
                & ssh $Machine.host "gcc -O2 -o '$($Machine.workDir)/$($h.Name)' '$($Machine.workDir)/$($h.Name).c'"
            }
        }
    }
}

function New-Manifest([Object]$Machine) {
    $lines = [System.Collections.Generic.List[String]]::new()
    foreach ($v in (Get-ApplicableVariants $Machine)) {
        $dir = Get-VariantDir $v $Machine.rid
        if (-not (Test-Path $dir)) {
            Write-Warning "跳过未构建变体 $($v.Id)/$($Machine.rid)"
            continue
        }
        if ($v.Kind -eq 'dotnet') {
            $exe = if ($v.Id -eq 'dotnet-universal') { '/usr/bin/dotnet' } else { './StarAgent' }
            $dll = if ($v.Id -eq 'dotnet-universal') { 'StarAgent.dll' } else { '' }
            if ($Modes -contains 'empty') { $lines.Add("$($v.Id)`tempty`t$exe`t$dll`t--version`t") }
            if ($Modes -contains 'full') { $lines.Add("$($v.Id)`tfull`t$exe`t$dll`t-status`t") }
            if ($Modes -contains 'run') {
                if ($v.Id -eq 'dotnet-universal') {
                    $lines.Add("$($v.Id)`trun`t$exe`tStarAgent.dll`t-run -server $AgentServer`tFirst heartbeat succeeded")
                }
                else {
                    $lines.Add("$($v.Id)`trun`t$exe`t-run`t-server $AgentServer`tFirst heartbeat succeeded")
                }
            }
        }
        else {
            if ($Modes -contains 'empty') { $lines.Add("$($v.Id)`tempty`t./staragent`tversion`t`t") }
            if ($Modes -contains 'run') { $lines.Add("$($v.Id)`trun`t./staragent`tonce`t--server $AgentServer`tonce ping completed") }
        }
    }
    return ($lines -join "`n") + "`n"
}

function Invoke-LinuxBench([Object]$Machine) {
    $manifest = New-Manifest $Machine
    $local = Join-Path $BenchRoot "manifest-$($Machine.name).tsv"
    Write-TextFile $local $manifest

    if ($Machine.kind -eq 'wsl') {
        $wslRoot = ConvertTo-WslPath $Machine.workDir
        $wslLocal = ConvertTo-WslPath $local
        & wsl -e bash -c "cp '$wslLocal' '$wslRoot/manifest.tsv'"
        $output = & wsl -e bash -c "bash '$wslRoot/linux-runner.sh' '$wslRoot' $Iterations $RunIterations"
        if ($LASTEXITCODE -ne 0) { throw "WSL 运行失败" }
        return $output
    }

    & scp -q $local "$($Machine.host):$($Machine.workDir)/manifest.tsv"
    if ($LASTEXITCODE -ne 0) { throw "上传 manifest 失败：$($Machine.name)" }
    $output = & ssh $Machine.host "bash '$($Machine.workDir)/linux-runner.sh' '$($Machine.workDir)' $Iterations $RunIterations"
    if ($LASTEXITCODE -ne 0) { throw "运行失败：$($Machine.name)" }
    return $output
}

function Invoke-WindowsBench([Object]$Machine) {
    $rows = [System.Collections.Generic.List[Object]]::new()
    $dotnet = (Get-Command dotnet).Source
    foreach ($v in (Get-ApplicableVariants $Machine)) {
        $dir = Join-Path $Machine.workDir $v.Id
        if (-not (Test-Path $dir)) { Write-Warning "缺少目录 $dir"; continue }
        $exe = if ($v.Id -eq 'dotnet-universal') { $dotnet } else { Join-Path $dir 'StarAgent.exe' }
        if ($v.Kind -eq 'native') { $exe = Join-Path $dir 'staragent.exe' }

        $cases = @()
        if ($Modes -contains 'empty') { $cases += [pscustomobject]@{ Mode = 'empty'; Arg = if ($v.Kind -eq 'dotnet') { '--version' } else { 'version' } } }
        if ($Modes -contains 'full' -and $v.Kind -eq 'dotnet') { $cases += [pscustomobject]@{ Mode = 'full'; Arg = '-status' } }

        foreach ($case in $cases) {
            $mode = $case.Mode
            $arg = $case.Arg
            $argLine = if ($v.Id -eq 'dotnet-universal') { "`"$dir\StarAgent.dll`" $arg" } else { $arg }

            # 预热
            $null = Measure-WindowsProcess $exe $argLine $dir 1
            for ($i = 1; $i -le $Iterations; $i++) {
                $m = Measure-WindowsProcess $exe $argLine $dir 1
                $rows.Add([pscustomobject]@{
                    machine = $Machine.name; variant = $v.Id; mode = $mode; run = $i
                    wall_ms = $m.WallMs; cpu_ms = $m.CpuMs; rss_kb = $m.PeakWorkingSetKb; exit = $m.ExitCode; marker = ''
                })
            }
        }

        if ($Modes -contains 'run') {
            $marker = if ($v.Kind -eq 'dotnet') { 'First heartbeat succeeded' } else { '' }
            $runArg = if ($v.Id -eq 'dotnet-universal') {
                "`"$dir\StarAgent.dll`" -run -server $AgentServer"
            }
            elseif ($v.Kind -eq 'native') {
                "once --server $AgentServer"
            }
            else {
                "-run -server $AgentServer"
            }

            $null = Measure-WindowsHeartbeat $exe $runArg $dir $marker 120
            for ($i = 1; $i -le $RunIterations; $i++) {
                $m = Measure-WindowsHeartbeat $exe $runArg $dir $marker 120
                $rows.Add([pscustomobject]@{
                    machine = $Machine.name; variant = $v.Id; mode = 'run'; run = $i
                    wall_ms = $m.WallMs; cpu_ms = $m.CpuMs; rss_kb = $m.PeakWorkingSetKb; exit = $m.ExitCode; marker = $m.Marker
                })
            }
        }
    }
    return $rows
}

function Measure-WindowsHeartbeat([String]$Exe, [String]$Arguments, [String]$WorkDir, [String]$Marker, [Int]$TimeoutSec = 120) {
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $Exe
    $psi.Arguments = $Arguments
    $psi.WorkingDirectory = $WorkDir
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $logDir = Join-Path $WorkDir 'Log'

    # 清理上一次运行留下的日志，避免轮询到旧的心跳标记
    if (Test-Path $logDir) {
        Get-ChildItem $logDir -Filter '*.log' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    }

    $p = [System.Diagnostics.Process]::Start($psi)
    $outTask = $p.StandardOutput.ReadToEndAsync()
    $errTask = $p.StandardError.ReadToEndAsync()
    $peakPrivate = 0L
    $peakWs = 0L
    $found = $false

    while (-not $p.HasExited) {
        try {
            $p.Refresh()
            if ($p.WorkingSet64 -gt $peakWs) { $peakWs = $p.WorkingSet64 }
            $priv = $p.PrivateMemorySize64
            if ($priv -gt $peakPrivate) { $peakPrivate = $priv }
        }
        catch { }

        if (-not [string]::IsNullOrEmpty($Marker)) {
            try {
                $log = Get-ChildItem $logDir -Filter '*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
                if ($log) {
                    $text = [System.IO.File]::ReadAllText($log.FullName)
                    if ($text.Contains($Marker)) { $found = $true; break }
                }
            }
            catch { }
        }

        if ($sw.Elapsed.TotalSeconds -gt $TimeoutSec) { break }

        Start-Sleep -Milliseconds 10
    }

    $wall = $sw.Elapsed.TotalMilliseconds
    if (-not $p.HasExited) {
        try { $p.Kill($true) } catch { try { $p.Kill() } catch { } }
        $p.WaitForExit(5000) | Out-Null
    }
    $p.WaitForExit()
    $sw.Stop()
    try { $null = $outTask.Result } catch { }
    try { $null = $errTask.Result } catch { }
    try { if ($p.PeakWorkingSet64 -gt $peakWs) { $peakWs = $p.PeakWorkingSet64 } } catch { }

    return [pscustomobject]@{
        WallMs = [math]::Round($wall, 3)
        CpuMs = [math]::Round($p.TotalProcessorTime.TotalMilliseconds, 3)
        PeakWorkingSetKb = [int64]($peakWs / 1KB)
        PeakPrivateKb = [int64]($peakPrivate / 1KB)
        ExitCode = if ($p.HasExited) { $p.ExitCode } else { -1 }
        Marker = if ($found) { '1' } elseif ([string]::IsNullOrEmpty($Marker)) { '0' } else { '-1' }
    }
}

function Measure-WindowsProcess([String]$Exe, [String]$Arguments, [String]$WorkDir, [Int]$Count = 1) {
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $Exe
    $psi.Arguments = $Arguments
    $psi.WorkingDirectory = $WorkDir
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $p = [System.Diagnostics.Process]::Start($psi)
    $outTask = $p.StandardOutput.ReadToEndAsync()
    $errTask = $p.StandardError.ReadToEndAsync()
    $peakPrivate = 0L
    $peakWs = 0L
    while (-not $p.HasExited) {
        try {
            $p.Refresh()
            if ($p.WorkingSet64 -gt $peakWs) { $peakWs = $p.WorkingSet64 }
            $priv = $p.PrivateMemorySize64
            if ($priv -gt $peakPrivate) { $peakPrivate = $priv }
        }
        catch { }
        Start-Sleep -Milliseconds 1
    }
    $p.WaitForExit()
    $sw.Stop()
    $null = $outTask.Result
    $null = $errTask.Result
    try { if ($p.PeakWorkingSet64 -gt $peakWs) { $peakWs = $p.PeakWorkingSet64 } } catch { }

    return [pscustomobject]@{
        WallMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 3)
        CpuMs = [math]::Round($p.TotalProcessorTime.TotalMilliseconds, 3)
        PeakWorkingSetKb = [int64]($peakWs / 1KB)
        PeakPrivateKb = [int64]($peakPrivate / 1KB)
        ExitCode = $p.ExitCode
    }
}

function Get-DirSizeMb([String]$Dir) {
    if (-not (Test-Path $Dir)) { return $null }
    $sum = (Get-ChildItem $Dir -Recurse -File | Measure-Object Length -Sum).Sum
    return [math]::Round($sum / 1MB, 1)
}

function Get-BinarySizeMb([String]$Path) {
    if (-not (Test-Path $Path)) { return $null }
    return [math]::Round((Get-Item $Path).Length / 1MB, 1)
}

function Get-PackageSizes {
    $sizes = [ordered]@{}
    $sizes['dotnet-universal'] = Get-DirSizeMb (Join-Path $ArtifactRoot 'dotnet-universal')
    foreach ($rid in 'win-x64', 'linux-x64', 'linux-arm64') {
        foreach ($kind in 'jit', 'r2r') {
            $sizes["dotnet-$kind-$rid"] = Get-DirSizeMb (Join-Path $ArtifactRoot "dotnet-$kind-$rid")
        }
        $exe = if ($rid -eq 'win-x64') { 'StarAgent.exe' } else { 'StarAgent' }
        $sizes["dotnet-aot-$rid"] = Get-BinarySizeMb (Join-Path $ArtifactRoot "dotnet-aot-$rid/$exe")
    }
    foreach ($lang in 'go', 'rust', 'cpp') {
        foreach ($rid in 'win-x64', 'linux-x64', 'linux-arm64') {
            $exe = if ($rid -eq 'win-x64') { 'staragent.exe' } else { 'staragent' }
            $sizes["$lang-$rid"] = Get-BinarySizeMb (Join-Path $ArtifactRoot "$lang-$rid/$exe")
        }
    }
    return $sizes
}

function Write-Summary([Object[]]$Rows) {
    $sizes = Get-PackageSizes
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("# StarAgent 启动与完整生命周期原始汇总")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("> 生成时间：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | 启动每变体 $Iterations 次、生命周期每变体 $RunIterations 次取中位数")
    [void]$sb.AppendLine()

    [void]$sb.AppendLine('## 包大小（MB）')
    [void]$sb.AppendLine()
    [void]$sb.AppendLine('| 变体 | win-x64 | linux-x64 | linux-arm64 |')
    [void]$sb.AppendLine('|------|--------:|----------:|------------:|')
    foreach ($v in 'dotnet-universal', 'dotnet-jit', 'dotnet-r2r', 'dotnet-aot') {
        $cells = foreach ($rid in 'win-x64', 'linux-x64', 'linux-arm64') {
            if ($v -eq 'dotnet-universal') { $sizes[$v] } else { $sizes["$v-$rid"] }
        }
        [void]$sb.AppendLine("| $v | $($cells -join ' | ') |")
    }
    foreach ($v in 'go', 'rust', 'cpp') {
        $cells = foreach ($rid in 'win-x64', 'linux-x64', 'linux-arm64') { $sizes["$v-$rid"] }
        [void]$sb.AppendLine("| $v | $($cells -join ' | ') |")
    }
    [void]$sb.AppendLine()

    foreach ($mode in 'empty', 'full', 'run') {
        $modeRows = @($Rows | Where-Object { $_.mode -eq $mode })
        if ($modeRows.Count -eq 0) { continue }

        $title = switch ($mode) {
            'empty' { '空载启动（--version / version）' }
            'full' { '完整初始化（-status，仅 .NET）' }
            default { '完整生命周期（真实服务端：登录 → 首次心跳，-run / once）' }
        }
        [void]$sb.AppendLine("## $title")
        [void]$sb.AppendLine()

        foreach ($metric in 'wall_ms', 'cpu_ms', 'rss_kb') {
            $metricLabel = switch ($metric) {
                'wall_ms' { '墙钟时间 (ms)' }
                'cpu_ms' { 'CPU 时间 (ms)' }
                default { '峰值 RSS (MB)' }
            }
            [void]$sb.AppendLine("### $metricLabel")
            [void]$sb.AppendLine()
            $variantIds = @($modeRows | Select-Object -ExpandProperty variant -Unique)
            $machineNames = @($modeRows | Select-Object -ExpandProperty machine -Unique)
            [void]$sb.AppendLine('| 机器 | ' + (($variantIds | ForEach-Object { $VariantDefs | Where-Object Id -eq $_ | Select-Object -ExpandProperty Label }) -join ' | ') + ' |')
            [void]$sb.AppendLine('|------|' + (($variantIds | ForEach-Object { '---:' }) -join '|') + '|')
            foreach ($m in $machineNames) {
                $cells = foreach ($vid in $variantIds) {
                    $vals = @($Rows | Where-Object { $_.machine -eq $m -and $_.variant -eq $vid -and $_.mode -eq $mode } | ForEach-Object { [double]$_.$metric })
                    if ($vals.Count -eq 0) { '-' } else {
                        $med = ($vals | Sort-Object)[[int][math]::Floor($vals.Count / 2)]
                        if ($metric -eq 'rss_kb') { '{0:N1}' -f ($med / 1024) } else { '{0:N0}' -f $med }
                    }
                }
                [void]$sb.AppendLine("| $m | $($cells -join ' | ') |")
            }
            [void]$sb.AppendLine()
        }
    }

    Write-TextFile $SummaryMd $sb.ToString()
    Write-Host "汇总：$SummaryMd"}

# ===== 主流程 =====
Write-Host "StarAgent 基准：$($AllMachines.Count) 台机器 × $Iterations 次"

if (-not $SkipBuild) {
    Publish-DotNetVariants
    Build-GoVariants
    Build-WindowsNative
    if (-not $SkipNativeLinuxBuild) { Build-NativeLinux }
    $null = Build-BenchRunner
}

$allRows = [System.Collections.Generic.List[Object]]::new()
foreach ($machine in $AllMachines) {
    Write-Host "== $($machine.name) ($($machine.kind)/$($machine.rid))"
    if (-not $SkipDeploy) { Deploy-Variants $machine }

    if (-not $SkipRun) {
        if ($machine.kind -eq 'windows') {
            $rows = Invoke-WindowsBench $machine
            foreach ($r in $rows) { $allRows.Add($r) }
        }
        else {
            $lines = @(Invoke-LinuxBench $machine)
            foreach ($raw in $lines) {
                foreach ($line in ($raw -split "`r?`n")) {
                    if ($line -match '^variant,') { continue }
                    if ([string]::IsNullOrWhiteSpace($line)) { continue }
                    $p = $line.Trim().Split(',')
                    if ($p.Count -lt 7 -or [string]::IsNullOrWhiteSpace($p[3])) { continue }
                    $marker = if ($p.Count -ge 8) { $p[7] } else { '' }
                    $allRows.Add([pscustomobject]@{
                        machine = $machine.name; variant = $p[0]; mode = $p[1]; run = [int]$p[2]
                        wall_ms = [double]$p[3]; cpu_ms = [double]$p[4]; rss_kb = [double]$p[5]; exit = $p[6]; marker = $marker
                    })
                }
            }
        }
    }
}

if ($allRows.Count -gt 0) {
    $csvText = ($allRows | ConvertTo-Csv -NoTypeInformation) -join "`n"
    Write-TextFile $RawCsv ($csvText + "`n")
    Write-Host "原始数据：$RawCsv"
    Write-Summary $allRows
}
