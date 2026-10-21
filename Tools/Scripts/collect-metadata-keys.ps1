# 收集允许部分语言缺失的可选键（实体元数据、程序集标题/描述等），写入 metadata-keys.json
# 默认只输出「在部分语言中缺失」的键，并保留清单中已有的手工条目；
# 加 -All 输出源码中扫描到的全部元数据键（XCode 用它生成完整清单）。
#
# 用法：
#   pwsh -File Tools/Scripts/collect-metadata-keys.ps1 -All
#   pwsh -File Tools/Scripts/collect-metadata-keys.ps1 -Model XCode/Membership/Member.xml -All
#   pwsh -File Tools/Scripts/collect-metadata-keys.ps1 -All -OutFile metadata-coverage.csv
param(
    # Model.xml：表与字段描述（含首个分隔符前的显示名）
    [String[]]$Model = @(),
    # 源码目录：扫描 [DisplayName]/[Description]/[Category] 特性与 csproj 程序集元数据
    [String[]]$SourceDir = @("XCode", "XCodeTool"),
    # 额外项目文件：收集 AssemblyTitle/Description
    [String[]]$Project = @(),
    [String]$LocaleDir = "XCode\Locales",
    [String]$Output = "metadata-keys.json",
    [Switch]$All,
    [String]$OutFile
)

$ErrorActionPreference = "Stop"

# pwsh -File 会把 "A,B" 当成单个参数，这里统一拆成多个值
$Model = @($Model | ForEach-Object { $_ -split '[,;]' } | Where-Object { $_ })
$SourceDir = @($SourceDir | ForEach-Object { $_ -split '[,;]' } | Where-Object { $_ })
$Project = @($Project | ForEach-Object { $_ -split '[,;]' } | Where-Object { $_ })

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$localeDir = if ([IO.Path]::IsPathRooted($LocaleDir)) { $LocaleDir } else { Join-Path $root $LocaleDir }
if (!(Test-Path $localeDir)) {
    Write-Host "未找到语言目录：$localeDir" -ForegroundColor Red
    exit 1
}
$outputPath = if ([IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $localeDir $Output }

$pattern = '^\s*"((?:[^"\\]|\\.)*)"\s*:\s*"((?:[^"\\]|\\.)*)"\s*$'
function Unescape-Yaml([string]$text) {
    return [regex]::Replace($text, '\\(u[0-9a-fA-F]{4}|.)', {
        param($m)
        $v = $m.Groups[1].Value
        if ($v.Length -eq 5) { return [string][char][Convert]::ToInt32($v.Substring(1), 16) }
        switch -CaseSensitive ($v) {
            'n' { return "`n" } 'r' { return "`r" } 't' { return "`t" }
            '\\' { return '\' } '"' { return '"' } default { return $m.Value }
        }
    })
}

# 模型描述：与 ModelResolver.GetDisplayName 保持一致，含首个分隔符前的短语（仅对 CJK 源文）
$keys = [Collections.Generic.HashSet[String]]::new([StringComparer]::Ordinal)
function Add-ModelKey([String]$text) {
    if ([String]::IsNullOrWhiteSpace($text)) { return }

    $value = $text.Trim()
    [void]$keys.Add($value)
    if ($value -notmatch '[\u4e00-\u9fff]') { return }

    $index = $value.IndexOfAny([Char[]]@('.', '。', ',', '，', ';', '；', ':', '：', "`r", "`n"))
    if ($index -gt 0) {
        $display = $value.Substring(0, $index).Trim()
        if ($display) { [void]$keys.Add($display) }
    }
}

foreach ($item in $Model) {
    $path = if ([IO.Path]::IsPathRooted($item)) { $item } else { Join-Path $root $item }
    if (!(Test-Path -LiteralPath $path)) {
        Write-Host "未找到模型文件：$path" -ForegroundColor Red
        exit 1
    }

    $xml = [xml](Get-Content -LiteralPath $path -Raw -Encoding UTF8)
    foreach ($table in $xml.SelectNodes("//*[local-name()='Table']")) {
        Add-ModelKey ([String]$table.Description)
        foreach ($column in $table.SelectNodes(".//*[local-name()='Column']")) {
            Add-ModelKey ([String]$column.Description)
        }
    }
}

# 扫描源码：特性参数与 csproj 的程序集元数据
$sourceDirs = @($SourceDir | ForEach-Object {
    if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $root $_ }
} | Where-Object { Test-Path $_ })

$attributeKeys = [Collections.Generic.HashSet[String]]::new([StringComparer]::Ordinal)
foreach ($key in $keys) { [void]$attributeKeys.Add($key) }

foreach ($file in ($sourceDirs | ForEach-Object { Get-ChildItem $_ -Recurse -Include *.cs, *.csproj } |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin|Locales)[\\/]' })) {
    if ($file.Extension -eq ".csproj") {
        $text = [IO.File]::ReadAllText($file.FullName)
        foreach ($m in [regex]::Matches($text, '<(?:AssemblyTitle|Description)>([^<]+)</(?:AssemblyTitle|Description)>')) {
            $value = [System.Net.WebUtility]::HtmlDecode($m.Groups[1].Value.Trim())
            if ($value.Length -gt 0) { [void]$attributeKeys.Add($value) }
        }
        continue
    }

    # 逐行扫描并跳过注释行，避免把注释掉的特性当成源文
    $inBlock = $false
    foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding UTF8) {
        $trimmed = $line.Trim()
        if ($inBlock) {
            if ($trimmed.Contains("*/")) { $inBlock = $false }
            continue
        }
        if ($trimmed.StartsWith("/*")) { $inBlock = !$trimmed.Contains("*/"); continue }
        if ($trimmed.StartsWith("//")) { continue }

        foreach ($m in [regex]::Matches($trimmed, '\[(?:DisplayName|Description|Category)\("((?:[^"\\]|\\.)*)"')) {
            $value = $m.Groups[1].Value.Replace('\"', '"').Replace('\\', '\')
            if ($value.Length -gt 0) { [void]$attributeKeys.Add($value) }
        }
    }
}

# 显式指定的项目文件
foreach ($item in $Project) {
    $path = if ([IO.Path]::IsPathRooted($item)) { $item } else { Join-Path $root $item }
    if (!(Test-Path -LiteralPath $path)) {
        Write-Host "未找到项目文件：$path" -ForegroundColor Red
        exit 1
    }

    $text = [IO.File]::ReadAllText($path)
    foreach ($m in [regex]::Matches($text, '<(?:AssemblyTitle|Description)>([^<]+)</(?:AssemblyTitle|Description)>')) {
        $value = [System.Net.WebUtility]::HtmlDecode($m.Groups[1].Value.Trim())
        if ($value.Length -gt 0) { [void]$attributeKeys.Add($value) }
    }
}

# 读取各语言词条
$tables = [ordered]@{}
foreach ($file in Get-ChildItem $localeDir -Filter *.yml | Sort-Object Name) {
    $entries = [Collections.Generic.HashSet[String]]::new([StringComparer]::Ordinal)
    foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding UTF8) {
        $m = [regex]::Match($line, $pattern)
        if ($m.Success) { [void]$entries.Add((Unescape-Yaml $m.Groups[1].Value)) }
    }
    $tables[$file.BaseName] = $entries
}

# 基准语言（zh-Hans.yml）
$baselineName = "zh-Hans"
$baseline = $tables[$baselineName]
if ($null -eq $baseline) { $baseline = $tables[(Get-ChildItem $localeDir -Filter *.yml | Select-Object -First 1).BaseName] }

$missingInBaseline = @($attributeKeys | Where-Object { !$baseline.Contains($_) } | Sort-Object)

# 默认输出的键：基准语言里有，但不是每个语言都有
$partial = [Collections.Generic.SortedSet[String]]::new([StringComparer]::Ordinal)
foreach ($key in $attributeKeys) {
    if (!$baseline.Contains($key)) { continue }
    foreach ($culture in $tables.Keys) {
        if ($culture -eq "en") { continue }
        if (!$tables[$culture].Contains($key)) { [void]$partial.Add($key); break }
    }
}

# 保留清单中已有的条目（模型描述、语言名等非特性来源的可选键）
$existing = [Collections.Generic.SortedSet[String]]::new([StringComparer]::Ordinal)
if (Test-Path -LiteralPath $outputPath) {
    foreach ($item in (Get-Content -LiteralPath $outputPath -Encoding UTF8 -Raw | ConvertFrom-Json).keys) {
        if (![String]::IsNullOrEmpty($item)) { [void]$existing.Add($item) }
    }
}

$list = [Collections.Generic.SortedSet[String]]::new([StringComparer]::Ordinal)
if ($All) {
    foreach ($key in $attributeKeys) { [void]$list.Add($key) }
}
else {
    foreach ($key in $partial) { [void]$list.Add($key) }
}
foreach ($key in $existing) { [void]$list.Add($key) }

$json = [ordered]@{
    "comment" = "允许只出现在部分语言的可选键（实体元数据、程序集标题/描述等）。由 Tools/Scripts/collect-metadata-keys.ps1 生成，缺失时回退源文。"
    "updated" = (Get-Date -Format "yyyy-MM-dd")
    "keys"    = @($list)
}
[IO.File]::WriteAllText($outputPath, ($json | ConvertTo-Json -Depth 3), [Text.UTF8Encoding]::new($false))

if ($OutFile) {
    $rows = @()
    foreach ($key in ($attributeKeys | Sort-Object)) {
        $row = [ordered]@{ Key = $key }
        foreach ($culture in $tables.Keys) { $row[$culture] = if ($tables[$culture].Contains($key)) { "Y" } else { "" } }
        $rows += [pscustomobject]$row
    }
    $rows | Export-Csv -LiteralPath $OutFile -NoTypeInformation -Encoding UTF8
    Write-Host "已导出覆盖矩阵：$OutFile"
}

Write-Host "可选键清单：$($list.Count) 条 → $outputPath"
Write-Host "源码元数据键：$($attributeKeys.Count) 条"
if ($missingInBaseline.Count -gt 0) {
    Write-Host "基准语言缺少以下元数据键（元数据源文，通常无需进入基准表）：$($missingInBaseline.Count) 条" -ForegroundColor Yellow
}
