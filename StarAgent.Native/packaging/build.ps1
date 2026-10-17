<#
.SYNOPSIS
构建 StarAgent.Native 各语言产物。
.DESCRIPTION
默认构建 Go 的全部目标平台；若本机存在 Rust/C++ 工程与工具链，也一并构建。
产物按“语言/平台-架构”输出到仓库根目录 Bin/Agent/native/，并生成 BUILD-INFO.md 清单。
.PARAMETER OutDir
输出根目录，默认 <repo>/Bin/Agent/native。
.PARAMETER SkipGo
跳过 Go 构建。
.PARAMETER SkipRust
跳过 Rust 构建。
.PARAMETER SkipCpp
跳过 C++ 构建。
.EXAMPLE
pwsh ./build.ps1
.EXAMPLE
pwsh ./build.ps1 -SkipRust -SkipCpp
#>
param(
    [String]$OutDir = '',
    [Switch]$SkipGo,
    [Switch]$SkipRust,
    [Switch]$SkipCpp
)

$ErrorActionPreference = 'Stop'
$nativeRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $nativeRoot
if (-not $OutDir) { $OutDir = Join-Path $repoRoot 'Bin\Agent\native' }
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$artifacts = [System.Collections.Generic.List[object]]::new()
$skipped = [System.Collections.Generic.List[object]]::new()

function Find-Go {
    $candidates = @(
        (Get-Command go -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        'C:\Program Files\Go\bin\go.exe',
        'C:\Go\bin\go.exe'
    ) | Where-Object { $_ -and (Test-Path $_) }
    if ($candidates.Count -eq 0) { throw '未找到 go，可安装 Go 1.24+ 或将其加入 PATH' }
    return @($candidates)[0]
}

function Find-Cargo {
    $candidates = @(
        (Get-Command cargo -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        (Join-Path $env:USERPROFILE '.cargo\bin\cargo.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    if ($candidates.Count -eq 0) { return $null }
    return @($candidates)[0]
}

function Find-Rustup {
    $candidates = @(
        (Get-Command rustup -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        (Join-Path $env:USERPROFILE '.cargo\bin\rustup.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    if ($candidates.Count -eq 0) { return $null }
    return @($candidates)[0]
}

function Find-CMake {
    $candidates = @(
        (Get-Command cmake -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        'C:\vs2026\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
    ) | Where-Object { $_ -and (Test-Path $_) }
    if ($candidates.Count -eq 0) { return $null }
    return @($candidates)[0]
}

function Get-HostTarget {
    $os = if ($IsWindows) { 'windows' } elseif ($IsMacOS) { 'darwin' } else { 'linux' }
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    if ($arch -eq 'x64') { $arch = 'amd64' }
    return "$os-$arch"
}

function Add-Artifact([String]$Language, [String]$Target, [String]$Path) {
    $item = Get-Item $Path
    $relative = $item.FullName.Substring($OutDir.Length).TrimStart('\', '/')
    $hash = (Get-FileHash $item.FullName -Algorithm SHA256).Hash
    $artifacts.Add([pscustomobject]@{
        Language = $Language
        Target   = $Target
        File     = $relative
        Size     = $item.Length
        Sha256   = $hash
    })
    Write-Host "built $relative ($([math]::Round($item.Length / 1MB, 2)) MB)"
}

function Add-Skipped([String]$Language, [String]$Target, [String]$Reason) {
    $skipped.Add([pscustomobject]@{ Language = $Language; Target = $Target; Reason = $Reason })
    Write-Warning "跳过 $Language/$Target：$Reason"
}

function Publish-Artifact([String]$Language, [String]$Target, [String]$Source, [String]$Name) {
    $dir = Join-Path (Join-Path $OutDir $Language) $Target
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $dest = Join-Path $dir $Name
    Copy-Item $Source $dest -Force
    Add-Artifact $Language $Target $dest
}

if (-not $SkipGo) {
    $go = Find-Go
    $src = Join-Path $nativeRoot 'go'
    $out = Join-Path $OutDir 'go'
    Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $out -Force | Out-Null

    Push-Location $src
    try {
        $targets = @(
            @{ GOOS = 'linux'; GOARCH = 'amd64'; Name = 'linux-amd64'; Ext = '' },
            @{ GOOS = 'linux'; GOARCH = 'arm64'; Name = 'linux-arm64'; Ext = '' },
            @{ GOOS = 'linux'; GOARCH = 'arm'; GOARM = '7'; Name = 'linux-armv7'; Ext = '' },
            @{ GOOS = 'windows'; GOARCH = 'amd64'; Name = 'windows-amd64'; Ext = '.exe' },
            @{ GOOS = 'windows'; GOARCH = 'arm64'; Name = 'windows-arm64'; Ext = '.exe' },
            @{ GOOS = 'darwin'; GOARCH = 'amd64'; Name = 'darwin-amd64'; Ext = '' },
            @{ GOOS = 'darwin'; GOARCH = 'arm64'; Name = 'darwin-arm64'; Ext = '' }
        )

        $env:CGO_ENABLED = '0'
        foreach ($target in $targets) {
            $env:GOOS = $target.GOOS
            $env:GOARCH = $target.GOARCH
            if ($target.GOARM) { $env:GOARM = $target.GOARM } else { Remove-Item Env:GOARM -ErrorAction SilentlyContinue }
            $temp = Join-Path ([System.IO.Path]::GetTempPath()) ("staragent-go-" + [Guid]::NewGuid().ToString('N') + $target.Ext)
            & $go build -trimpath -ldflags '-s -w' -o $temp ./cmd/staragent
            if ($LASTEXITCODE -ne 0) { throw "Go 构建失败：$($target.Name)" }
            Publish-Artifact 'go' $target.Name $temp ("staragent" + $target.Ext)
            Remove-Item $temp -Force -ErrorAction SilentlyContinue
        }
    }
    finally {
        Pop-Location
        Remove-Item Env:GOOS,Env:GOARCH,Env:GOARM,Env:CGO_ENABLED -ErrorAction SilentlyContinue
    }
}

if (-not $SkipRust -and (Test-Path (Join-Path $nativeRoot 'rust\Cargo.toml'))) {
    $cargo = Find-Cargo
    if (-not $cargo) {
        Add-Skipped 'rust' 'all' '未找到 cargo'
    }
    else {
        $rust = Join-Path $nativeRoot 'rust'
        $out = Join-Path $OutDir 'rust'
        Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path $out -Force | Out-Null

        $targets = [ordered]@{
            'x86_64-pc-windows-msvc'   = @{ OS = 'windows'; Arch = 'amd64'; Ext = '.exe' }
            'i686-pc-windows-msvc'     = @{ OS = 'windows'; Arch = '386'; Ext = '.exe' }
            'aarch64-pc-windows-msvc'  = @{ OS = 'windows'; Arch = 'arm64'; Ext = '.exe' }
            'x86_64-unknown-linux-musl' = @{ OS = 'linux'; Arch = 'amd64'; Ext = '' }
            'aarch64-unknown-linux-musl' = @{ OS = 'linux'; Arch = 'arm64'; Ext = '' }
            'x86_64-unknown-linux-gnu' = @{ OS = 'linux'; Arch = 'amd64'; Ext = '' }
            'aarch64-unknown-linux-gnu' = @{ OS = 'linux'; Arch = 'arm64'; Ext = '' }
            'x86_64-apple-darwin'      = @{ OS = 'darwin'; Arch = 'amd64'; Ext = '' }
            'aarch64-apple-darwin'     = @{ OS = 'darwin'; Arch = 'arm64'; Ext = '' }
        }

        $rustup = Find-Rustup
        $installed = @()
        if ($rustup) { $installed = @(& $rustup target list --installed 2>$null) }
        if ($installed.Count -eq 0) {
            $hostTriple = ((& $cargo -Vv) | Select-String '^host:') -replace '^host:\s*', ''
            if ($hostTriple) { $installed = @($hostTriple.Trim()) }
        }

        Push-Location $rust
        try {
            foreach ($entry in $targets.GetEnumerator()) {
                $triple = $entry.Key
                $target = $entry.Value
                if ($installed -notcontains $triple) {
                    Add-Skipped 'rust' $triple '未安装 target（rustup target add）'
                    continue
                }
                if ($target.OS -ne (Get-HostTarget).Split('-')[0]) {
                    if ($target.OS -eq 'darwin' -and -not $IsMacOS) {
                        Add-Skipped 'rust' $triple '需要 macOS SDK 与链接器'
                        continue
                    }
                    if ($target.OS -eq 'windows' -and -not $IsWindows) {
                        Add-Skipped 'rust' $triple '需要 Windows MSVC 链接器'
                        continue
                    }
                    if ($target.OS -eq 'linux' -and $IsWindows) {
                        $musl = Get-Command 'x86_64-linux-musl-gcc', 'aarch64-linux-musl-gcc' -ErrorAction SilentlyContinue
                        if (-not $musl) {
                            Add-Skipped 'rust' $triple '缺少 musl 交叉编译器（musl-gcc / cargo-zigbuild）'
                            continue
                        }
                    }
                }

                & $cargo build --release --target $triple
                if ($LASTEXITCODE -ne 0) {
                    Add-Skipped 'rust' $triple 'cargo build 失败'
                    continue
                }
                $exe = Join-Path (Join-Path (Join-Path $rust 'target') $triple) ('release\staragent' + $target.Ext)
                if (Test-Path $exe) {
                    Publish-Artifact 'rust' ("$($target.OS)-$($target.Arch)") $exe ('staragent' + $target.Ext)
                }
                else {
                    Add-Skipped 'rust' $triple '未找到构建产物'
                }
            }
        }
        finally { Pop-Location }
    }
}

if (-not $SkipCpp -and (Test-Path (Join-Path $nativeRoot 'cpp\CMakeLists.txt'))) {
    $cmake = Find-CMake
    if (-not $cmake) {
        Add-Skipped 'cpp' 'all' '未找到 cmake'
    }
    else {
        $cpp = Join-Path $nativeRoot 'cpp'
        $build = Join-Path $cpp 'build'
        $out = Join-Path $OutDir 'cpp'
        Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path $out -Force | Out-Null

        if ($IsWindows) {
            & $cmake -S $cpp -B $build -A x64
            if ($LASTEXITCODE -ne 0) { throw 'C++ configure 失败' }
            & $cmake --build $build --config Release --parallel
            if ($LASTEXITCODE -ne 0) { throw 'C++ 构建失败' }
            $exe = Join-Path $build 'Release\staragent.exe'
            if (Test-Path $exe) { Publish-Artifact 'cpp' 'windows-amd64' $exe 'staragent.exe' } else { Add-Skipped 'cpp' 'windows-amd64' '未找到构建产物' }
        }
        else {
            & $cmake -S $cpp -B $build -DCMAKE_BUILD_TYPE=Release
            if ($LASTEXITCODE -ne 0) { throw 'C++ configure 失败' }
            & $cmake --build $build --parallel
            if ($LASTEXITCODE -ne 0) { throw 'C++ 构建失败' }
            $exe = Join-Path $build 'staragent'
            if (Test-Path $exe) { Publish-Artifact 'cpp' (Get-HostTarget) $exe 'staragent' } else { Add-Skipped 'cpp' (Get-HostTarget) '未找到构建产物' }
        }
    }
}

$manifest = @(
    '# StarAgent Native 构建清单',
    '',
    "- 生成时间：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')",
    "- 主机：$([System.Environment]::OSVersion.VersionString) / $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)",
    "- 输出目录：``$OutDir``",
    '',
    '## 产物',
    '',
    '| 语言 | 目标平台 | 文件 | 大小 | SHA256 |',
    '|------|----------|------|------|--------|'
)
foreach ($item in ($artifacts | Sort-Object Language, Target)) {
    $manifest += "| $($item.Language) | $($item.Target) | ``$($item.File)`` | $([math]::Round($item.Size / 1MB, 2)) MB | ``$($item.Sha256)`` |"
}
$manifest += ''
$manifest += '## 跳过或未构建'
$manifest += ''
$manifest += '| 语言 | 目标 | 原因 |'
$manifest += '|------|------|------|'
foreach ($item in ($skipped | Sort-Object Language, Target)) {
    $manifest += "| $($item.Language) | $($item.Target) | $($item.Reason) |"
}
$manifest += ''
$manifest += '> 其他平台的构建说明见 `StarAgent.Native/packaging/README.md`。'

[System.IO.File]::WriteAllLines((Join-Path $OutDir 'BUILD-INFO.md'), [string[]]$manifest, [System.Text.UTF8Encoding]::new($false))
Write-Host "产物目录：$OutDir"
