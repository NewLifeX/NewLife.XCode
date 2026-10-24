param(
    [String]$Configuration = "Release",
    [Int32]$TimeoutSec = 180
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot/../.."
$project = Join-Path $PSScriptRoot "MobileSmoke.csproj"
$package = "com.newlife.smoke.xcode"

# 模拟器可能仍在启动，先等待系统就绪，避免 install 失败被误报为冒烟超时
$bootDeadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $bootDeadline) {
    $booted = (& adb shell getprop sys.boot_completed 2>$null | Out-String).Trim()
    if ($booted -eq "1") { break }
    Start-Sleep -Seconds 2
}
if ($booted -ne "1") { throw "Android emulator did not finish booting" }

dotnet build $project -c $Configuration -f net10.0-android --nologo
if ($LASTEXITCODE -ne 0) { throw "Android build failed" }

$apk = Get-ChildItem (Join-Path $root "Bin/MobileSmoke") -Recurse -Filter *.apk |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $apk) { throw "APK not found" }

# 先卸载，避免 AOT/非 AOT 构建切换后残留原生库导致 UnsatisfiedLinkError
adb uninstall $package 2>$null | Out-Null
adb install -r $apk.FullName | Out-Host
adb logcat -c
adb shell monkey -p $package -c android.intent.category.LAUNCHER 1 | Out-Host

Start-Sleep -Seconds 5
$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    $hit = adb logcat -d -s MobileSmoke:* | Select-String "SMOKE_OK|SMOKE_FAIL" | Select-Object -Last 1
    if ($hit) {
        Write-Host $hit.Line
        if ($hit.Line -match "SMOKE_OK") { exit 0 }
        exit 1
    }

    # 进程提前退出时立即导出崩溃栈，不再等满超时
    $appPid = (adb shell pidof $package 2>$null | Out-String).Trim()
    if (-not $appPid) {
        adb logcat -d -b crash -v time | Out-Host
        throw "Android smoke process exited before reporting a result"
    }

    Start-Sleep -Seconds 2
}

adb logcat -d -s MobileSmoke:* | Out-Host
adb logcat -d -b crash -v time | Out-Host
throw "Android smoke timed out"
