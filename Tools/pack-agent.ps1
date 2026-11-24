<#
.SYNOPSIS
发布 StarAgent 安装包：三大 RID 使用 AOT，其余目标使用 any 框架依赖包。

.DESCRIPTION
1. any：按 StarAgent.csproj 的多目标框架逐个发布框架依赖包（staragent10/90/80/...）。
2. AOT：为 win-x64、linux-x64、linux-arm64 发布 NativeAOT 单文件包；Linux 在 WSL 中交叉编译。
3. 统一输出到 Bin/Agent（可用 -OutDir 覆盖），生成 zip/tar.gz 与 BUILD-INFO.json。

.PARAMETER OutDir
输出目录，默认 <repo>/Bin/Agent。

.PARAMETER Version
包版本号，默认读取 StarAgent.csproj 的 Version。

.PARAMETER AotRids
AOT 目标 RID，默认 win-x64,linux-x64,linux-arm64。

.PARAMETER SlimAot
AOT 使用 Slim 档：语言白名单（中英）+ StackTraceSupport=false，体积再降 3~4 MB。

.PARAMETER SkipAny
跳过 any 包。

.PARAMETER SkipAot
跳过 AOT 包。

.PARAMETER LocalSource
AOT 使用本地 NewLife 源码（-p:UseLocalNewLifeSource=true），仅联调时使用。

.EXAMPLE
pwsh Tools/pack-agent.ps1

.EXAMPLE
pwsh Tools/pack-agent.ps1 -SlimAot -AotRids win-x64
#>
[CmdletBinding()]
param(
    [String]$OutDir = '',
    [String]$Version = '',
    [String[]]$AotRids = @('win-x64', 'linux-x64', 'linux-arm64'),
    [Switch]$SlimAot,
    [Switch]$SkipAny,
    [Switch]$SkipAot,
    [Switch]$LocalSource
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $RepoRoot 'StarAgent/StarAgent.csproj'
if (-not (Test-Path $Project)) { throw "找不到项目：$Project" }
if (-not $OutDir) { $OutDir = Join-Path $RepoRoot 'Bin/Agent' }
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$WorkRoot = Join-Path $RepoRoot '.tmp/pack'
New-Item -ItemType Directory -Path $WorkRoot -Force | Out-Null

function Write-TextFile([String]$Path, [String]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function ConvertTo-WslPath([String]$Path) {
    if ($Path.StartsWith('/')) { return $Path }
    $full = [System.IO.Path]::GetFullPath($Path)
    $drive = $full.Substring(0, 1).ToLowerInvariant()
    return "/mnt/$drive" + $full.Substring(2).Replace('\', '/')
}

function Get-Version {
    if ($Version) { return $Version }
    $v = & dotnet msbuild $Project -getProperty:Version -p:TargetFrameworks=net10.0 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $v) { throw '读取项目版本失败，请用 -Version 指定' }
    return ($v | Select-Object -Last 1).Trim()
}

$PackageVersion = Get-Version
Write-Host "StarAgent 发布：$PackageVersion -> $OutDir"

$packages = [System.Collections.Generic.List[Object]]::new()

function Add-Package([String]$Form, [String]$Rid, [String]$Tfm, [String]$OS, [String]$Arch, [String]$TargetRuntime, [String]$Path, [String]$TarPath = '') {
    $file = Get-Item $Path
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash
    $tarFile = ''
    $tarSize = 0
    $tarHash = ''
    if ($TarPath -and (Test-Path $TarPath)) {
        $tar = Get-Item $TarPath
        $tarFile = $tar.Name
        $tarSize = $tar.Length
        $tarHash = (Get-FileHash $tar.FullName -Algorithm SHA256).Hash
    }
    $packages.Add([pscustomobject]@{
        form = $Form; rid = $Rid; tfm = $Tfm; os = $OS; arch = $Arch; targetRuntime = $TargetRuntime
        file = $file.Name; size = $file.Length; sha256 = $hash
        tarFile = $tarFile; tarSize = $tarSize; tarSha256 = $tarHash
    })
    Write-Host ("  {0,-34} {1,8:N2} MB  {2}" -f $file.Name, ($file.Length / 1MB), $hash.Substring(0, 12))
    if ($tarFile) { Write-Host ("  {0,-34} {1,8:N2} MB  {2}" -f $tarFile, ($tarSize / 1MB), $tarHash.Substring(0, 12)) }
}

function New-Zip([String]$ZipPath, [String]$SourceDir, [String[]]$Includes) {
    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
    $files = @(Get-ChildItem $SourceDir -File | Where-Object { $name = $_.Name; $Includes | Where-Object { $name -like $_ } })
    if ($files.Count -eq 0) { throw "没有可打包文件：$SourceDir" }
    Compress-Archive -Path ($files | Select-Object -ExpandProperty FullName) -DestinationPath $ZipPath -CompressionLevel Optimal
}

function New-TarGz([String]$TarPath, [String]$SourceDir, [String[]]$Names) {
    if (Test-Path $TarPath) { Remove-Item $TarPath -Force }

    # Windows tar 不支持 --mode，Linux shim 会丢执行位；优先用 WSL tar 固定 755
    if (Get-Command wsl -ErrorAction SilentlyContinue) {
        $tarWsl = ConvertTo-WslPath $TarPath
        $dirWsl = ConvertTo-WslPath $SourceDir
        $quoted = ($Names | ForEach-Object { "'$_'" }) -join ' '
        & wsl -e bash -c "tar --mode=755 -czf '$tarWsl' -C '$dirWsl' $quoted"
    }
    else {
        & tar.exe -czf $TarPath -C $SourceDir @Names
    }
    if ($LASTEXITCODE -ne 0) { throw "打包 tar.gz 失败：$TarPath" }
}

# ===== any（框架依赖）=====
if (-not $SkipAny) {
    Write-Host '== any（框架依赖）'
    $tfms = @(
        @{ Tfm = 'net10.0'; Name = 'staragent10'; OS = 'Any'; Arch = 'Any'; Runtime = '10' },
        @{ Tfm = 'net9.0'; Name = 'staragent90'; OS = 'Any'; Arch = 'Any'; Runtime = '9' },
        @{ Tfm = 'net8.0'; Name = 'staragent80'; OS = 'Any'; Arch = 'Any'; Runtime = '8' },
        @{ Tfm = 'net7.0'; Name = 'staragent70'; OS = 'Any'; Arch = 'Any'; Runtime = '7' },
        @{ Tfm = 'net6.0'; Name = 'staragent60'; OS = 'Any'; Arch = 'Any'; Runtime = '6' },
        @{ Tfm = 'net461'; Name = 'staragent461'; OS = 'Any'; Arch = 'Any'; Runtime = '4' },
        @{ Tfm = 'net45'; Name = 'staragent45'; OS = 'Any'; Arch = 'Any'; Runtime = '4' }
    )

    foreach ($item in $tfms) {
        $dir = Join-Path $WorkRoot "any/$($item.Tfm)"
        Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  publish $($item.Tfm)"
        $publishArgs = @('publish', $Project, '-c', 'Release', '-f', $item.Tfm, "-p:TargetFrameworks=$($item.Tfm)", '-o', $dir)
        if ($LocalSource) { $publishArgs += '-p:UseLocalNewLifeSource=true' }
        & dotnet @publishArgs 2>&1 | Select-String -Pattern ': error|-> ' | Select-Object -Last 1
        if ($LASTEXITCODE -ne 0) { throw "发布失败：$($item.Tfm)" }

        # Linux 下 any 包没有 apphost，补一个 shim，便于 AOT→any 后 systemd 仍能从 ./StarAgent 启动
        $shim = Join-Path $dir 'StarAgent'
        $shimText = "#!/bin/sh`n# Generated by pack-agent.ps1: framework-dependent entry for Linux.`nDIR=`$(cd `"`$(dirname `"`$0`")`" && pwd)`nexec dotnet `"`$DIR/StarAgent.dll`" `"`$@`"`n"
        Write-TextFile $shim $shimText

        $include = @('*.exe', '*.dll', '*.runtimeconfig.json', '*.exe.config', 'StarAgent')
        $zip = Join-Path $OutDir "$($item.Name).zip"
        New-Zip $zip $dir $include

        $tar = Join-Path $OutDir "$($item.Name).tar.gz"
        $names = @(Get-ChildItem $dir -File | Where-Object { $n = $_.Name; $include | Where-Object { $n -like $_ } } | Select-Object -ExpandProperty Name)
        New-TarGz $tar $dir $names

        Add-Package 'any' 'any' $item.Tfm $item.OS $item.Arch $item.Runtime $zip $tar
    }
}

# ===== AOT =====
if (-not $SkipAot) {
    Write-Host "== AOT（$($AotRids -join ', ')）"

    if ($SlimAot) { $env:NewLifeLocalizationLanguages = 'zh-Hans;en' }

    foreach ($rid in $AotRids) {
        if ($rid -notin @('win-x64', 'linux-x64', 'linux-arm64')) { throw "不支持的 AOT RID：$rid" }
        $dir = Join-Path $WorkRoot "aot/$rid"
        Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue

        $slimArgs = ''
        if ($SlimAot) { $slimArgs = '-p:StackTraceSupport=false' }
        $localArgs = ''
        if ($LocalSource) { $localArgs = '-p:UseLocalNewLifeSource=true' }

        if ($rid -eq 'win-x64') {
            $args = @('publish', $Project, '-c', 'Release', '-f', 'net10.0', '-r', $rid, '--self-contained', 'true',
                '-p:TargetFrameworks=net10.0', '-p:StarAgentAot=true', '-p:StripSymbols=true', '-o', $dir)
            if ($SlimArgs) { $args += $slimArgs }
            if ($LocalArgs) { $args += $LocalArgs }
            Write-Host "  publish $rid"
            & dotnet @args 2>&1 | Select-String -Pattern ': error|-> ' | Select-Object -Last 1
            if ($LASTEXITCODE -ne 0) { throw "AOT 发布失败：$rid" }
        }
        else {
            $repoWsl = ConvertTo-WslPath $RepoRoot
            $outWsl = ConvertTo-WslPath $dir
            $objcopy = if ($rid -eq 'linux-arm64') { '-p:ObjCopyName=aarch64-linux-gnu-objcopy' } else { '' }
            $envPrefix = if ($SlimAot) { 'NewLifeLocalizationLanguages="zh-Hans;en" ' } else { '' }
            $cmd = "${envPrefix}cd '$repoWsl' && dotnet publish StarAgent/StarAgent.csproj -c Release -f net10.0 -r $rid --self-contained true -p:TargetFrameworks=net10.0 -p:StarAgentAot=true -p:StripSymbols=true -p:CppCompilerAndLinker=clang -p:LinkerFlavor=lld $objcopy $slimArgs $localArgs -o '$outWsl'"
            Write-Host "  publish $rid (WSL)"
            & wsl -e bash -c $cmd 2>&1 | Select-String -Pattern ': error|-> ' | Select-Object -Last 1
            if ($LASTEXITCODE -ne 0) { throw "AOT 发布失败：$rid" }
        }

        $exeName = if ($rid -eq 'win-x64') { 'StarAgent.exe' } else { 'StarAgent' }
        $exe = Join-Path $dir $exeName
        if (-not (Test-Path $exe)) { throw "找不到 AOT 产物：$exe" }

        # 只保留可执行文件，调试符号不随包发布
        Get-ChildItem $dir -File | Where-Object { $_.Name -ne $exeName } | Remove-Item -Force

        $zip = Join-Path $OutDir "staragent10-$rid-aot.zip"
        New-Zip $zip $dir @($exeName)

        $tar = ''
        if ($rid -ne 'win-x64') {
            $tar = Join-Path $OutDir "staragent10-$rid-aot.tar.gz"
            $tarWsl = ConvertTo-WslPath $tar
            $dirWsl = ConvertTo-WslPath $dir
            & wsl -e bash -c "tar --mode=755 -czf '$tarWsl' -C '$dirWsl' '$exeName'"
            if ($LASTEXITCODE -ne 0) { throw "打包 tar.gz 失败：$tar" }
        }

        $os = if ($rid -like 'win*') { 'Windows' } else { 'Linux' }
        $arch = if ($rid -like '*arm64') { 'Arm64' } else { 'X64' }
        Add-Package 'aot' $rid 'net10.0' $os $arch '10' $zip $tar
    }
}

if ($SlimAot) { Remove-Item Env:NewLifeLocalizationLanguages -ErrorAction SilentlyContinue }

# ===== 清单 =====
$info = [pscustomobject]@{
    productCode = 'StarAgent'
    version = $PackageVersion
    generatedAt = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')
    slimAot = [Boolean]$SlimAot
    packages = $packages
}
Write-TextFile (Join-Path $OutDir 'BUILD-INFO.json') ($info | ConvertTo-Json -Depth 5)

Write-Host "完成：$($packages.Count) 个包，清单 $(Join-Path $OutDir 'BUILD-INFO.json')"
