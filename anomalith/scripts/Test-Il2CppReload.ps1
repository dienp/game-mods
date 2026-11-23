param(
    [Parameter(Mandatory)] [string] $AnomalithDir,
    [ValidateRange(1, 60)] [int] $TimeoutSeconds = 10
)

$ErrorActionPreference = 'Stop'
$gameDir = (Resolve-Path -LiteralPath $AnomalithDir).ProviderPath
$exe = Join-Path $gameDir 'Anomalith.exe'
$log = Join-Path $gameDir 'BepInEx\LogOutput.log'
$game = Get-Process -Name 'Anomalith' -ErrorAction SilentlyContinue | Where-Object Path -EQ $exe
if (-not $game) {
    throw 'Start ANOMALITH with the IL2CPP Debug plugins installed first.'
}

$development = Join-Path $gameDir 'BepInEx\development'
$plugins = @(Get-ChildItem -LiteralPath $development -Filter 'Anomalith*.dll' -File)
if ($plugins.Count -eq 0) {
    throw "No ANOMALITH payloads found in $development"
}

function Get-ReloadCount([string] $Name) {
    @(Select-String -LiteralPath $log -Pattern "Hot reloaded $Name from" -SimpleMatch).Count
}

$results = foreach ($plugin in $plugins) {
    $before = Get-ReloadCount $plugin.BaseName
    if ($before -eq 0) {
        throw "$($plugin.BaseName) has no Debug load entry in this game session."
    }
    [pscustomobject] @{ Plugin = $plugin.BaseName; Before = $before; After = $before }
}
foreach ($plugin in $plugins) {
    $plugin.LastWriteTimeUtc = [DateTime]::UtcNow
}

$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    if ($game.HasExited) {
        throw "ANOMALITH exited during the check. Exit code $($game.ExitCode)."
    }
    foreach ($result in $results) {
        $result.After = Get-ReloadCount $result.Plugin
    }
    $pending = $results | Where-Object { $_.After -le $_.Before }
    if (-not $pending) {
        $results
        Write-Host "Reloaded $($plugins.Count) ANOMALITH plugins in PID $($game.Id)."
        return
    }
    Start-Sleep -Milliseconds 250
} while ([DateTime]::UtcNow -lt $deadline)

throw "No reload reported for $($pending.Plugin -join ', ') within $TimeoutSeconds seconds. See $log"
