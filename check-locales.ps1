# 多语言词表一致性校验：键集合、占位符、格式说明符
# 以基准语言文件（默认 zh-Hans.yml）为准，所有语言必须提供完全相同的键集合
#
# 用法：
#   pwsh -File check-locales.ps1                                      # 星尘默认目录（Stardust）
#   pwsh -File check-locales.ps1 -LocaleDir Stardust.Web\Locales -SourceDir Stardust.Web
#   pwsh -File check-locales.ps1 -LocaleDir Stardust\Locales -SkipOrphan   # 下游只想校验词表内部一致性
#   pwsh -File check-locales.ps1 -LocaleDir Stardust.Data\Locales -SourceDir Stardust.Data -MetadataKeys Stardust.Data\Locales\metadata-keys.json  # 元数据键允许部分语言缺失
param(
    [String]$LocaleDir = "Stardust\Locales",
    [String]$Baseline = "zh-Hans.yml",
    [String[]]$SourceDir = @("Stardust"),
    # 元数据键清单，允许部分语言缺失（缺失回退源文），并免于孤儿键检查
    [String]$MetadataKeys,
    # 从生成实体/控制器的 [DisplayName]/[Description] 特性中收集元数据键
    [String[]]$MetadataSourceDir = @(),
    [Switch]$SkipOrphan,
    [Switch]$Strict
)

$ErrorActionPreference = "Stop"

# pwsh -File 会把 "NewLife.Cube,NewLife.CubeNC" 当成单个参数，这里统一拆成多个目录
$SourceDir = @($SourceDir | ForEach-Object { $_ -split '[,;]' } | Where-Object { $_ })

$root = $PSScriptRoot
$localeDir = if ([IO.Path]::IsPathRooted($LocaleDir)) { $LocaleDir } else { Join-Path $root $LocaleDir }
if (!(Test-Path $localeDir)) {
    Write-Host "未找到语言目录：$localeDir" -ForegroundColor Red
    exit 1
}

$pattern = '^\s*"((?:[^"\\]|\\.)*)"\s*:\s*"((?:[^"\\]|\\.)*)"\s*$'
$placeholder = '\{(\d+)(?:,-?\d+)?(:[^}]*)?\}'
$languageCode = '\b[a-z]{2}-[A-Za-z]{2,4}\b'

function Unescape-Yaml([string]$text) {
    # 与源生成器保持一致：还原 \\ \" \n \r \t \uXXXX
    return [regex]::Replace($text, '\\(u[0-9a-fA-F]{4}|.)', {
        param($m)
        $v = $m.Groups[1].Value
        if ($v.Length -eq 5) { return [string][char][Convert]::ToInt32($v.Substring(1), 16) }
        switch -CaseSensitive ($v) {
            'n' { return "`n" }
            'r' { return "`r" }
            't' { return "`t" }
            '\\' { return '\' }
            '"' { return '"' }
            default { return $m.Value }
        }
    })
}

function Get-Placeholders([string]$text) {
    $set = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($m in [regex]::Matches($text, $placeholder)) {
        [void]$set.Add([int]$m.Groups[1].Value)
    }
    return ,$set
}

function Get-PlaceholderSpecs([string]$text) {
    $map = @{}
    foreach ($m in [regex]::Matches($text, $placeholder)) {
        $index = [int]$m.Groups[1].Value
        if (!$map.ContainsKey($index)) { $map[$index] = $m.Groups[2].Value }
    }
    return $map
}

# 语言清单条目：译文中的语言代码集合必须与英文 key 一致，防止译者漏码
function Test-LanguageCodeList([string]$key, [string]$value) {
    if (!$key.StartsWith("UI language. Values:")) { return $true }
    $expected = @([regex]::Matches($key, $languageCode) | ForEach-Object { $_.Value } | Sort-Object -Unique)
    $actual = @([regex]::Matches($value, $languageCode) | ForEach-Object { $_.Value } | Sort-Object -Unique)
    return !(Compare-Object $expected $actual)
}

function Read-Locale([string]$path) {
    $entries = [ordered]@{}
    $errors = @()
    $lineNo = 0
    foreach ($line in Get-Content -LiteralPath $path -Encoding UTF8) {
        $lineNo++
        if ($line.Trim().Length -eq 0 -or $line.TrimStart().StartsWith("#")) { continue }
        $m = [regex]::Match($line, $pattern)
        if (!$m.Success) {
            $errors += "第 $lineNo 行格式非法：$line"
            continue
        }
        $key = Unescape-Yaml $m.Groups[1].Value
        if ($entries.Contains($key)) {
            $errors += "第 $lineNo 行键重复：$key"
            continue
        }
        $entries[$key] = Unescape-Yaml $m.Groups[2].Value
    }
    return @{ Entries = $entries; Errors = $errors }
}

$referencePath = Join-Path $localeDir $Baseline
if (!(Test-Path -LiteralPath $referencePath)) {
    Write-Host "未找到基准语言文件：$referencePath" -ForegroundColor Red
    exit 1
}
$reference = Read-Locale $referencePath
$allErrors = @($reference.Errors | ForEach-Object { "$($Baseline): $_" })
$allWarnings = @()

foreach ($key in $reference.Entries.Keys) {
    if (!(Test-LanguageCodeList $key $reference.Entries[$key])) {
        $allErrors += "$($Baseline): 语言代码清单不一致 → $key"
    }
}

# 元数据键：来自显式清单或实体/控制器特性。允许部分语言缺失，并免于孤儿键检查
$metadataKeySet = [System.Collections.Generic.HashSet[String]]::new([StringComparer]::Ordinal)
if ($MetadataKeys) {
    $metaPath = if ([IO.Path]::IsPathRooted($MetadataKeys)) { $MetadataKeys } else { Join-Path $root $MetadataKeys }
    if (!(Test-Path -LiteralPath $metaPath)) {
        Write-Host "未找到元数据键清单：$metaPath" -ForegroundColor Red
        exit 1
    }
    $metaJson = Get-Content -LiteralPath $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($metaJson -is [array]) { $metaItems = $metaJson }
    elseif ($null -ne $metaJson.keys) { $metaItems = $metaJson.keys }
    else { $metaItems = @() }
    foreach ($item in @($metaItems)) { if ($item) { [void]$metadataKeySet.Add([String]$item) } }
}
foreach ($dir in @($MetadataSourceDir | Where-Object { $_ })) {
    $dirPath = if ([IO.Path]::IsPathRooted($dir)) { $dir } else { Join-Path $root $dir }
    if (!(Test-Path $dirPath)) { continue }
    foreach ($file in Get-ChildItem $dirPath -Recurse -Filter *.cs -File) {
        $text = [IO.File]::ReadAllText($file.FullName)
        foreach ($m in [regex]::Matches($text, '\[(?:DisplayName|Description)\("((?:[^"\\]|\\.)*)"\)\]')) {
            $key = $m.Groups[1].Value.Replace('\"', '"').Replace('\\', '\')
            if ($key) { [void]$metadataKeySet.Add($key) }
        }
    }
}

foreach ($file in Get-ChildItem $localeDir -Filter *.yml | Sort-Object Name) {
    if ($file.Name -eq $Baseline) { continue }
    $lang = [IO.Path]::GetFileNameWithoutExtension($file.Name)
    $locale = Read-Locale $file.FullName
    foreach ($e in $locale.Errors) { $allErrors += "$($file.Name): $e" }

    foreach ($key in $reference.Entries.Keys) {
        # en 是源文语言，允许只提供元数据译文，其余消息回退英文源文
        if (!$locale.Entries.Contains($key) -and !$metadataKeySet.Contains($key) -and $lang -ne "en") {
            $allErrors += "$($file.Name): 缺少键 $key"
        }
    }
    foreach ($key in $locale.Entries.Keys) {
        if (!$reference.Entries.Contains($key)) {
            # 元数据键允许只出现在部分语言；但仍需校验占位符
            if ($metadataKeySet.Contains($key)) {
                $value = $locale.Entries[$key]
                $expected = Get-Placeholders $key
                $actual = Get-Placeholders $value
                if (!$expected.SetEquals($actual)) { $allErrors += "$($file.Name): 占位符不一致 → $key" }
                continue
            }
            $allErrors += "$($file.Name): 多余键 $key"
            continue
        }

        $value = $locale.Entries[$key]
        $expected = Get-Placeholders $key
        $actual = Get-Placeholders $value
        if (!$expected.SetEquals($actual)) {
            $allErrors += "$($file.Name): 占位符不一致 → $key"
            continue
        }

        # 格式说明符只在译文保留该占位符时比对，缺失时仅告警
        $enSpecs = Get-PlaceholderSpecs $key
        $trSpecs = Get-PlaceholderSpecs $value
        foreach ($index in $enSpecs.Keys) {
            if ($trSpecs.ContainsKey($index) -and $trSpecs[$index] -ne $enSpecs[$index]) {
                $allWarnings += "$($file.Name): 格式说明符不一致 → $key"
                break
            }
        }

        if (!(Test-LanguageCodeList $key $value)) {
            $allErrors += "$($file.Name): 语言代码清单不一致 → $key"
        }
    }
}

# 严格模式：译文不得与英文源文完全相同（值等于键即视为未翻译），
# 同形词（如德语 Status、荷兰语 Systeem）在 Locales/identical.allow.json 中按语言登记后放行。
if ($Strict) {
    $allowPath = Join-Path $localeDir "identical.allow.json"
    $allow = @{}
    if (Test-Path -LiteralPath $allowPath) {
        $allowJson = Get-Content -LiteralPath $allowPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($p in $allowJson.PSObject.Properties) {
            $set = [System.Collections.Generic.HashSet[String]]::new([StringComparer]::Ordinal)
            foreach ($v in @($p.Value)) { [void]$set.Add([String]$v) }
            $allow[$p.Name] = $set
        }
    }

    foreach ($file in Get-ChildItem $localeDir -Filter *.yml | Sort-Object Name) {
        $lang = [IO.Path]::GetFileNameWithoutExtension($file.Name)
        if ($lang -eq [IO.Path]::GetFileNameWithoutExtension($Baseline) -or $lang -eq "en") { continue }

        $locale = Read-Locale $file.FullName
        $allowed = $null
        if ($allow.ContainsKey($lang)) { $allowed = $allow[$lang] }
        $count = 0
        foreach ($key in $locale.Entries.Keys) {
            if ($locale.Entries[$key] -cne $key) { continue }
            if ($null -ne $allowed -and $allowed.Contains($key)) { continue }
            $allErrors += "$($file.Name): 未翻译（值等于英文源文）→ $key"
            if ($count++ -ge 20) { $allErrors += "$($file.Name): 未翻译条目过多，仅列出前 20 条"; break }
        }
    }
}

# 孤儿键：基准表中的键在源码里完全找不到（改文案后遗留的死条目）
$orphans = @()
if (!$SkipOrphan) {
    $sourceDirs = @($SourceDir | ForEach-Object {
        if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $root $_ }
    } | Where-Object { Test-Path $_ })

    # 视图里的 L.T("...") 同样会命中词表，孤儿键检测必须把 .cshtml 也算进来（2026-10-20）
    # 程序集元数据（AssemblyTitle/Description）与 PackageDescription 是单值，按 Core 规范以英文源文入表（2026-10-21）
    $sourceText = ($sourceDirs | ForEach-Object { Get-ChildItem $_ -Recurse -File | Where-Object { $_.Extension -in '.cs', '.cshtml', '.csproj' } } |
        Where-Object { $_.FullName -notmatch '\\(obj|bin|Locales)\\' } |
        ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"

    # NewLife.Cube 通过 <Compile Include="..\NewLife.CubeNC\..."> 链接共享源码，
    # 其中的 L.T 调用同样会命中词表；孤儿键检测必须把这些文件算进来（2026-10-20）
    $linkProject = Join-Path $root "NewLife.Cube\NewLife.Cube.csproj"
    if (Test-Path $linkProject) {
        $csproj = [IO.File]::ReadAllText($linkProject)
        $linkRoot = Join-Path $root "NewLife.CubeNC"
        $linkedText = [regex]::Matches($csproj, 'Include="\.\.\\NewLife\.CubeNC\\([^"]+)"') |
            ForEach-Object { Join-Path $linkRoot ($_.Groups[1].Value.Replace('\', '/')) } |
            Where-Object { Test-Path $_ } |
            ForEach-Object { [IO.File]::ReadAllText($_) }
        if ($linkedText) { $sourceText += "`n" + ($linkedText -join "`n") }
    }

    # 统一换行，避免多行提示词因 CRLF 与 LF 差异被判为孤儿键
    $sourceText = $sourceText.Replace("`r`n", "`n")

    foreach ($key in $reference.Entries.Keys) {
        if ($metadataKeySet.Contains($key)) { continue }

        # 源码中的字面量形式：制表符写作 \t，引号写作 \"；ANSI 转义在源码中可能是 \e 或 \u001b
        $escaped = $key.Replace('\', '\\').Replace('"', '\"').Replace("`t", '\t').Replace("`r", '\r').Replace("`n", '\n')
        $variants = @($key, $escaped)
        $esc = [String][Char]27
        if ($key.Contains($esc)) {
            $variants += $key.Replace($esc, '\e')
            $variants += $key.Replace($esc, '\u001b')
            $variants += $key.Replace($esc, '\x1b')
        }
        $found = $false
        foreach ($v in $variants) {
            if ($sourceText.Contains($v)) { $found = $true; break }
        }
        if (!$found) {
            # 去掉 ANSI 颜色序列与制表符转义后再比对，避免 \e / \u001b 混用导致误报
            $plainKey = $key.Replace($esc, '') -replace '\[[0-9;]*m', ''
            $plainKey = $plainKey.Replace("`t", '\t')
            $plainSource = $sourceText -replace '(\\e|\\u001b|\\x1b)\[[0-9;]*m', ''
            if (!$plainSource.Contains($plainKey)) {
                $orphans += $key
            }
        }
    }
    foreach ($o in $orphans) { $allErrors += "孤儿键（源码中找不到）：$o" }
}

foreach ($e in $allErrors) { Write-Host $e -ForegroundColor Red }
foreach ($w in $allWarnings) { Write-Host $w -ForegroundColor Yellow }

if ($allErrors.Count -gt 0) {
    foreach ($o in $orphans) { Write-Host "孤儿键（源码中找不到）：$o" -ForegroundColor Red }
}

$count = (Get-ChildItem $localeDir -Filter *.yml).Count
if ($allErrors.Count -gt 0) {
    Write-Host "多语言词表校验未通过：$count 个语言，$($allErrors.Count) 个错误" -ForegroundColor Red
    exit 1
}

Write-Host "多语言词表校验通过：$count 个语言，基准 $($reference.Entries.Keys.Count) 条" -ForegroundColor Green
