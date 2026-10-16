param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe',
    [string]$OutputDirectory = 'Builds\Windows'
)

$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
$outputPath = Join-Path (Join-Path $projectPath $OutputDirectory) 'ProjectCrusade.exe'
$logPath = Join-Path $projectPath 'Logs\build-windows.log'
if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity Editor not found: $UnityPath"
}
New-Item -ItemType Directory -Force -Path (Split-Path $outputPath), (Split-Path $logPath) | Out-Null
$arguments = '-batchmode -quit -projectPath "{0}" -buildTarget Win64 -buildWindows64Player "{1}" -logFile "{2}"' -f $projectPath, $outputPath, $logPath
$buildProcess = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($buildProcess.ExitCode -ne 0) {
    throw "Unity build failed with exit code $($buildProcess.ExitCode). Log: $logPath"
}
if (-not (Test-Path -LiteralPath $outputPath)) {
    throw "Unity exited without producing the player. Log: $logPath"
}
if (-not (Select-String -LiteralPath $logPath -Pattern 'Build Successful' -Quiet)) {
    throw "Unity did not report a successful build. Log: $logPath"
}
Write-Output "Build completed: $outputPath"
