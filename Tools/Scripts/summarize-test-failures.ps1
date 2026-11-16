[CmdletBinding()]
param(
    [String]$Path = 'TestResults',
    [Int32]$MaxLength = 600
)

# 汇总 trx 中的失败用例，写入 GitHub Step Summary 并输出 error 注解。
# 目的：Actions 页面直接看到哪个用例挂了，不必下载 trx 制品再逐条翻。
# 用法：pwsh -NoProfile -File Tools/Scripts/summarize-test-failures.ps1 -Path TestResults

$ErrorActionPreference = 'Continue'

$summary = $env:GITHUB_STEP_SUMMARY
$ns = @{ t = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010' }
$count = 0

Get-ChildItem -Path $Path -Filter *.trx -File -ErrorAction SilentlyContinue | ForEach-Object {
    $file = $_
    try
    {
        [xml]$xml = Get-Content -Path $file.FullName -Raw
    }
    catch
    {
        Write-Host "Skip unreadable trx: $($file.Name) - $($_.Exception.Message)"
        return
    }

    $results = Select-Xml -Xml $xml -XPath '//t:UnitTestResult[@outcome="Failed"]' -Namespace $ns
    foreach ($item in $results)
    {
        $name = $item.Node.testName
        $message = $item.Node.Output.ErrorInfo.Message
        if (-not $message) { $message = $item.Node.Output.ErrorInfo.StackTrace }
        if (-not $message) { $message = '(no message)' }

        # 压成一行，避免 trx 里的多行堆栈把摘要撑爆
        $message = ($message -replace '\s+', ' ').Trim()
        if ($message.Length -gt $MaxLength) { $message = $message.Substring(0, $MaxLength) + '...' }

        $count++
        # GitHub 命令里的 % 与换行需要转义，否则注解会被截断
        $annotation = ($name + ' - ' + $message).Replace('%', '%25').Replace("`r", ' ').Replace("`n", ' ')
        Write-Host "::error title=$($file.BaseName)::$annotation"
        if ($summary) { Add-Content -Path $summary -Value "- **$($name)** - $($message)" }
    }
}

if ($count -eq 0)
{
    if ($summary) { Add-Content -Path $summary -Value '- 未发现失败用例（或未生成 trx）' }
}
else
{
    Write-Host "Failed cases: $count"
}
