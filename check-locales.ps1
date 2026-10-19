# 多语言词表一致性校验：键集合、占位符、格式说明符
# 以基准语言文件（默认 zh-Hans.yml）为准，所有语言必须提供完全相同的键集合
#
# 用法：
#   pwsh -File check-locales.ps1                                      # Core 默认目录
#   pwsh -File check-locales.ps1 -LocaleDir src\Lib\Locales -SourceDir src\Lib
#   pwsh -File check-locales.ps1 -LocaleDir src\Lib\Locales -SkipOrphan   # 下游只想校验词表内部一致性
#   pwsh -File check-locales.ps1 -LocaleDir XCode\Locales -SourceDir XCode -RequireTranslated   # 要求所有语言均已翻译
param(
    [String]$LocaleDir = "NewLife.Core\Locales",
    [String]$Baseline = "zh-Hans.yml",
    [String[]]$SourceDir = @("NewLife.Core", "NewLife.Core.SourceGenerator"),
    [Switch]$RequireTranslated,
    [String[]]$AllowSameValue = @("{0} format {1}"),
    [Switch]$SkipOrphan,
    [Switch]$SkipMessageCheck,
    [String[]]$AllowMissingMessage = @(
        '[{0}/{1}] {2}',
        '[{0}] DataSource={1} Database={2} User={3}',
        'CreateSplit: {0}->{2}, {1}->{3}',
        'RestoreSplit: {0}, {1}',
        'TDengine {0}',
        'Tran.Begin {0} {1}',
        'Tran.Commit {0} {1} Executes={2}',
        'Tran.Rollback {0} {1} Executes={2}',
        '{0} {1} {2} => {3} {4}',
        '.CreateItem({0})',
        '.CreateSlaveItem({0})',
        '.UpdateData {0} Expire={1} Visit={2}',
        '{0}({1},{2})',
        '{0}({1})'
    )
)

$ErrorActionPreference = "Stop"

# pwsh -File 会把 "XCode,XCodeTool" 当成单个参数，这里统一拆成多个目录
$SourceDir = @($SourceDir | ForEach-Object { $_ -split '[,;]' } | Where-Object { $_ })

$root = $PSScriptRoot
$localeDir = if ([IO.Path]::IsPathRooted($LocaleDir)) { $LocaleDir } else { Join-Path $root $LocaleDir }
if (!(Test-Path $localeDir)) {
    Write-Host "未找到语言目录：$localeDir" -ForegroundColor Red
    exit 1
}

$pattern = '^\s*"((?:[^"\\]|\\.)*)"\s*:\s*"((?:[^"\\]|\\.)*)"\s*$'
$placeholder = '\{(\d+)(?:,-?\d+)?(:[^}]*)?\}'

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

foreach ($file in Get-ChildItem $localeDir -Filter *.yml | Sort-Object Name) {
    if ($file.Name -eq $Baseline) { continue }
    $locale = Read-Locale $file.FullName
    foreach ($e in $locale.Errors) { $allErrors += "$($file.Name): $e" }

    foreach ($key in $reference.Entries.Keys) {
        if (!$locale.Entries.Contains($key)) {
            $allErrors += "$($file.Name): 缺少键 $key"
        }
    }
    foreach ($key in $locale.Entries.Keys) {
        if (!$reference.Entries.Contains($key)) {
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

        # 值与英文源文完全相同视为未翻译（少数语言的同形词见 AllowSameValue）
        if ($RequireTranslated -and [String]::Equals($value, $key, [StringComparison]::Ordinal) -and !$AllowSameValue.Contains($key)) {
            $allErrors += "$($file.Name): 未翻译（值与英文源文相同）→ $key"
        }
    }
}

# 孤儿键：基准表中的键在源码里完全找不到（改文案后遗留的死条目）
$orphans = @()
if (!$SkipOrphan) {
    $sourceDirs = @($SourceDir | ForEach-Object {
        if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $root $_ }
    } | Where-Object { Test-Path $_ })

    $sourceText = ($sourceDirs | ForEach-Object { Get-ChildItem $_ -Recurse -Filter *.cs } |
        Where-Object { $_.FullName -notmatch '\\(obj|bin|Locales)\\' } |
        ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"

    foreach ($key in $reference.Entries.Keys) {
        # 源码中的字面量形式：制表符写作 \t，引号写作 \"
        $escaped = $key.Replace('\', '\\').Replace('"', '\"').Replace("`t", '\t').Replace("`r", '\r').Replace("`n", '\n')
        if (!$sourceText.Contains($key) -and !$sourceText.Contains($escaped)) {
            $orphans += $key
        }
    }
    foreach ($o in $orphans) { $allErrors += "孤儿键（源码中找不到）：$o" }
}

# 源码消息缺少词条：L.T / XException 系列 / 带前缀日志会按词表翻译，缺键时静默回退英文
if (!$SkipMessageCheck) {
    $messagePatterns = @(
        'L\.T\(\s*"((?:[^"\\]|\\.)*)"',
        'new\s+(?:XCodeException|XException|EntityException)\s*\(\s*"((?:[^"\\]|\\.)*)"',
        '(?:Info|Error|Warn|Debug|Fatal)Prefixed\([^,]*,\s*"((?:[^"\\]|\\.)*)"',
        'XTrace\.WriteLine\(\s*"((?:[^"\\]|\\.)*)"',
        '[\w]*[Ll]og\??\.(?:Info|Error|Warn|Debug|Fatal)\(\s*"((?:[^"\\]|\\.)*)"',
        'WriteLog\(\s*"((?:[^"\\]|\\.)*)"',
        'Localization\.Translate\(\s*"((?:[^"\\]|\\.)*)"'
    )
    $sourceDirs = @($SourceDir | ForEach-Object {
        if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $root $_ }
    } | Where-Object { Test-Path $_ })
    $missingMessages = [ordered]@{}

    foreach ($dir in $sourceDirs) {
        foreach ($file in Get-ChildItem $dir -Recurse -Filter *.cs) {
            if ($file.FullName -match '\\(obj|bin|Locales)\\') { continue }
            # 去掉注释，避免把示例代码当成真实调用
            $text = [regex]::Replace([IO.File]::ReadAllText($file.FullName), '/\*.*?\*/', '', 'Singleline')
            $text = [regex]::Replace($text, '(?m)^\s*//.*$', '')
            foreach ($pattern in $messagePatterns) {
                foreach ($m in [regex]::Matches($text, $pattern)) {
                    $key = Unescape-Yaml $m.Groups[1].Value
                    if ($AllowMissingMessage.Contains($key)) { continue }
                    if (!$reference.Entries.Contains($key) -and !$missingMessages.Contains($key)) {
                        $rel = if ($file.FullName.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                            $file.FullName.Substring($root.Length).TrimStart("\", "/")
                        } else { $file.FullName }
                        $missingMessages[$key] = $rel
                    }
                }
            }

            # 空前缀没有翻译收益：*Prefixed 的意义是前缀不参与查表，空前缀应直接用 XTrace.WriteLine
            foreach ($m in [regex]::Matches($text, '(?:Info|Error|Warn|Debug|Fatal)Prefixed\(\s*(?:prefix\s*:\s*)?(?:null|String\.Empty|"")\s*,')) {
                $lineNo = ($text.Substring(0, $m.Index) -split "`n").Count
                $rel = if ($file.FullName.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                    $file.FullName.Substring($root.Length).TrimStart("\", "/")
                } else { $file.FullName }
                $allErrors += "空前缀日志应使用 XTrace.WriteLine：$rel`:$lineNo"
            }
        }
    }

    foreach ($key in $missingMessages.Keys) {
        $allErrors += "源码消息缺少词条：$key（$($missingMessages[$key])）"
    }
}

$count = (Get-ChildItem $localeDir -Filter *.yml).Count
if ($allErrors.Count -gt 0) {
    foreach ($e in $allErrors) { Write-Host $e -ForegroundColor Red }
    Write-Host "多语言词表校验未通过：$count 个语言，$($allErrors.Count) 个错误" -ForegroundColor Red
    exit 1
}

if ($allWarnings.Count -gt 0) {
    foreach ($w in $allWarnings) { Write-Host $w -ForegroundColor Yellow }
}

Write-Host "多语言词表校验通过：$count 个语言，基准 $($reference.Entries.Count) 条" -ForegroundColor Green
