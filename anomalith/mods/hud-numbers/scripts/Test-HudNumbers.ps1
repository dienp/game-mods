param(
    [Parameter(Mandatory)] [string] $AnomalithDir,
    [Parameter(Mandatory)] [string] $ScreenshotDirectory,
    [ValidateRange(10, 600)] [int] $TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$gameDir = (Resolve-Path -LiteralPath $AnomalithDir).ProviderPath
$exe = Join-Path $gameDir 'Anomalith.exe'
$log = Join-Path $gameDir 'BepInEx\LogOutput.log'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "ANOMALITH executable missing from $gameDir"
}
if (Get-Process -Name 'Anomalith' -ErrorAction SilentlyContinue | Where-Object Path -EQ $exe) {
    throw 'Close this game installation before starting the HUD smoke test.'
}

$runId = [Guid]::NewGuid().ToString('N')
$output = Join-Path ([IO.Path]::GetFullPath($ScreenshotDirectory)) $runId
$screenshot = Join-Path $output 'hud-numbers.png'
$previousRun = $env:ANOMALITH_HUD_TEST_ID
$previousScreenshots = $env:ANOMALITH_TEST_SCREENSHOTS
try {
    $env:ANOMALITH_HUD_TEST_ID = $runId
    $env:ANOMALITH_TEST_SCREENSHOTS = $output
    $game = Start-Process -FilePath $exe -WorkingDirectory $gameDir -WindowStyle Normal -PassThru
} finally {
    $env:ANOMALITH_HUD_TEST_ID = $previousRun
    $env:ANOMALITH_TEST_SCREENSHOTS = $previousScreenshots
}

Write-Host "ANOMALITH started (PID $($game.Id)). Load a save and return to gameplay with HP/MP Numbers ON."
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
$reachedGameplay = $false
while ([DateTime]::UtcNow -lt $deadline) {
    if ($game.HasExited) {
        throw "The game exited before the smoke test passed. Exit code $($game.ExitCode)."
    }
    if (Test-Path -LiteralPath $log -PathType Leaf) {
        if (-not $reachedGameplay) {
            $reachedGameplay = [bool](Select-String -LiteralPath $log -Pattern "HUD smoke test $runId reached gameplay" -SimpleMatch)
        }
        $passed = Select-String -LiteralPath $log -Pattern "HUD smoke test $runId passed" -SimpleMatch
        if ($passed -and (Test-Path -LiteralPath $screenshot -PathType Leaf)) {
            $passed | Select-Object -Last 1 -ExpandProperty Line
            Get-Item -LiteralPath $screenshot | Select-Object -ExpandProperty FullName
            return
        }
    }
    Start-Sleep -Seconds 1
}
if (-not $reachedGameplay) {
    throw "The test did not reach gameplay with HP/MP Numbers enabled within $TimeoutSeconds seconds. See $log"
}
throw "The HUD did not produce a passing result and screenshot within $TimeoutSeconds seconds. See $log"
