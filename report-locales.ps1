# 多语言覆盖率报告：统计各组件各语言的已翻译条目
# 已翻译 = 译文与英文源文不同且非空；英文为源文与兜底，不计入覆盖率
#
# 用法：
#   pwsh -File report-locales.ps1                       # 输出到 Doc/多语言覆盖.md
#   pwsh -File report-locales.ps1 -OutFile tmp.md
param(
    [String]$OutFile = "Doc/多语言覆盖.md"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$projects = [ordered]@{
    "Stardust"           = "Stardust/Locales"
    "Stardust.Extensions" = "Stardust.Extensions/Locales"
    "Stardust.Server"    = "Stardust.Server/Locales"
    "Stardust.Web"       = "Stardust.Web/Locales"
    "Stardust.WebApi"    = "Stardust.WebApi/Locales"
    "Stardust.Data"      = "Stardust.Data/Locales"
    "StarAgent"          = "StarAgent/Locales"
    "StarAgent.Mobile"   = "StarAgent.Mobile/Locales"
    "StarGateway"        = "StarGateway/Locales"
    "DeployAgent"        = "DeployAgent/Locales"
    "AgentExpansion"     = "Plugins/AgentExpansion/Locales"
    "NetworkDetect"      = "Plugins/NetworkDetect/Locales"
}

$pattern = '^\s*"((?:[^"\\]|\\.)*)"\s*:\s*"((?:[^"\\]|\\.)*)"\s*$'

function Read-Locale([string]$path) {
    $entries = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $path -Encoding UTF8) {
        $m = [regex]::Match($line, $pattern)
        if (!$m.Success) { continue }
        $entries[$m.Groups[1].Value] = $m.Groups[2].Value
    }
    return $entries
}

$baseline = "zh-Hans.yml"
$locales = @()
$stats = @{}

foreach ($name in $projects.Keys) {
    $dir = Join-Path $root $projects[$name]
    if (!(Test-Path $dir)) { continue }

    $reference = Read-Locale (Join-Path $dir $baseline)
    $project = @{ Name = $name; Total = $reference.Count; Languages = @{} }

    foreach ($file in Get-ChildItem $dir -Filter *.yml | Sort-Object Name) {
        $locale = $file.BaseName
        if ($locales -notcontains $locale) { $locales += $locale }

        $entries = Read-Locale $file.FullName
        $translated = 0
        foreach ($key in $entries.Keys) {
            # 元数据键（菜单名、字段描述等中文源文）不计入运行时消息覆盖率
            if (!$reference.Contains($key)) { continue }
            $value = $entries[$key]
            if (![string]::IsNullOrWhiteSpace($value) -and $value -ne $key) { $translated++ }
        }
        $project.Languages[$locale] = $translated

        if (!$stats.ContainsKey($locale)) { $stats[$locale] = @{ Translated = 0; Keys = @{} } }
        foreach ($key in $entries.Keys) {
            if (!$reference.Contains($key)) { continue }
            $value = $entries[$key]
            if (![string]::IsNullOrWhiteSpace($value) -and $value -ne $key) { $stats[$locale].Keys[$key] = $true }
        }
    }

    $stats[$name] = $project
}

# 全局唯一英文源文总数
$allKeys = @{}
foreach ($name in $projects.Keys) {
    $dir = Join-Path $root $projects[$name]
    if (!(Test-Path $dir)) { continue }
    foreach ($key in (Read-Locale (Join-Path $dir $baseline)).Keys) { $allKeys[$key] = $true }
}

$lines = @()
$lines += "# 多语言覆盖"
$lines += ""
$lines += "> 自动生成：``pwsh -File report-locales.ps1``。已翻译指译文与英文源文不同，英文为源文与兜底不计入。"
$lines += ""
$lines += "## 1. 按语言汇总"
$lines += ""
$lines += "| 语言 | 已翻译 | 唯一源文 | 覆盖率 |"
$lines += "|------|--------|----------|--------|"
foreach ($locale in $locales) {
    $count = $stats[$locale].Keys.Count
    $ratio = if ($allKeys.Count -gt 0) { [math]::Round($count * 100.0 / $allKeys.Count, 1) } else { 0 }
    $lines += "| $locale | $count | $($allKeys.Count) | $ratio% |"
}
$lines += ""
$lines += "## 2. 按组件统计"
$lines += ""
$header = "| 组件 | 条目 |"
$divider = "|------|------|"
foreach ($locale in $locales) {
    $header += " $locale |"
    $divider += "---|"
}
$lines += $header
$lines += $divider
foreach ($name in $projects.Keys) {
    $project = $stats[$name]
    if ($project -isnot [hashtable] -or !$project.ContainsKey("Total")) { continue }
    $row = "| $name | $($project.Total) |"
    foreach ($locale in $locales) {
        $value = $project.Languages[$locale]
        if ($null -eq $value) { $row += " - |"; continue }
        $ratio = if ($project.Total -gt 0) { [math]::Round($value * 100.0 / $project.Total, 0) } else { 0 }
        $row += " $ratio% |"
    }
    $lines += $row
}
$lines += ""

# 界面元数据词条：菜单名、实体字段描述等中文源文，按 Cube 约定允许部分语言缺失并回退源文
$metaRows = @()
foreach ($name in $projects.Keys) {
    $dir = Join-Path $root $projects[$name]
    $metaPath = Join-Path $dir "metadata-keys.json"
    if (!(Test-Path $metaPath)) { continue }

    # 只统计中文源文键（菜单名、字段描述），程序集标题/描述等英文单值元数据不在界面内渲染
    $metaKeys = @(Get-Content -LiteralPath $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json | ForEach-Object { [String]$_ } | Where-Object { $_ -match '[\u4e00-\u9fff]' })
    if ($metaKeys.Count -eq 0) { continue }

    # 统计口径为“该语言提供了该键的译文”：是否与源文同形（如繁体中文的“使用版本”）不影响计入
    $perLang = [ordered]@{}
    $translatable = @{}
    foreach ($file in Get-ChildItem $dir -Filter *.yml | Sort-Object Name) {
        $locale = $file.BaseName
        if ($locale -eq [IO.Path]::GetFileNameWithoutExtension($baseline)) { continue }
        $entries = Read-Locale $file.FullName
        $hits = @{}
        foreach ($key in $metaKeys) {
            if (!$entries.Contains($key)) { continue }
            $value = $entries[$key]
            if (![string]::IsNullOrWhiteSpace($value)) { $hits[$key] = $true; $translatable[$key] = $true }
        }
        $perLang[$locale] = $hits
    }
    $total = $translatable.Count
    if ($total -eq 0) { continue }

    $full = 0; $partial = 0; $sum = 0; $langCount = 0
    foreach ($locale in $perLang.Keys) {
        $count = $perLang[$locale].Count
        $langCount++
        $sum += $count
        if ($count -ge $total) { $full++ } elseif ($count -gt 0) { $partial++ }
    }
    $avg = if ($langCount -gt 0) { [math]::Round($sum * 100.0 / ($total * $langCount), 1) } else { 0 }
    $metaRows += "| $name | $total | $full | $partial | $avg% |"
}
if ($metaRows.Count -gt 0) {
    $lines += "## 3. 界面元数据词条"
    $lines += ""
    $lines += "> 键为中文源文（菜单名、实体字段描述），由 Cube `DataField.Localize` / `MenuHelper.GetDisplayName` 在请求期翻译；缺失回退中文源文。"
    $lines += ""
    $lines += "| 组件 | 键数 | 完整翻译语言 | 部分翻译语言 | 平均覆盖率 |"
    $lines += "|------|------|--------------|--------------|------------|"
    $lines += $metaRows
    $lines += ""
}

$path = if ([IO.Path]::IsPathRooted($OutFile)) { $OutFile } else { Join-Path $root $OutFile }
$dir = Split-Path $path -Parent
if ($dir -and !(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
[IO.File]::WriteAllText($path, ($lines -join "`n") + "`n", (New-Object System.Text.UTF8Encoding($false)))

Write-Host "已生成多语言覆盖率报告：$path" -ForegroundColor Green
