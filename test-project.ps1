param(
    [ValidateSet('All', 'Validate', 'EditMode', 'PlayMode')][string]$Suite = 'All',
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe',
    [switch]$ProbeFailure
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity not found: $UnityPath" }
if ($ProbeFailure -and $Suite -ne 'EditMode') { throw 'ProbeFailure requires -Suite EditMode.' }
$testRunId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8)
$testReportPath = Join-Path $PSScriptRoot "Logs\Tests\$testRunId"
New-Item -ItemType Directory -Path $testReportPath -Force | Out-Null
Write-Output "Reports: $testReportPath"
$testSuites = if ($Suite -eq 'All') { @('Validate', 'EditMode', 'PlayMode') } else { @($Suite) }
foreach ($testSuite in $testSuites) {
    $testLog = Join-Path $testReportPath "$testSuite.log"
    $testArgs = '-batchmode -projectPath "{0}" -logFile "{1}"' -f $PSScriptRoot, $testLog
    if ($testSuite -eq 'Validate') {
        $testResult = Join-Path $testReportPath 'content.txt'
        $testArgs += ' -executeMethod ContentValidationCommand.ValidateContent -contentReport "{0}"' -f $testResult
    } else {
        $testResult = Join-Path $testReportPath "$testSuite.xml"
        $testAssembly = if ($testSuite -eq 'EditMode') { 'Crusade.EditMode.Tests' } else { 'Crusade.PlayMode.Tests' }
        $testArgs += ' -runTests -testPlatform {0} -assemblyNames {1} -testResults "{2}"' -f $testSuite, $testAssembly, $testResult
        if ($ProbeFailure) { $testArgs += ' -testFilter ContentTests.RejectsDuplicateIds -crusadeProbeFailure' }
    }
    $testProcess = Start-Process -FilePath $UnityPath -ArgumentList $testArgs -WindowStyle Hidden -PassThru
    if (-not $testProcess.WaitForExit(900000)) {
        Stop-Process -Id $testProcess.Id
        throw "$testSuite timed out after 15 minutes. Log: $testLog"
    }
    $testProcess.Refresh()
    if ($testProcess.ExitCode -ne 0) { throw "$testSuite exited $($testProcess.ExitCode). Log: $testLog" }
    if (-not (Test-Path -LiteralPath $testResult)) { throw "Missing $testSuite report: $testResult" }
    if ($testSuite -ne 'Validate') {
        [xml]$testXml = Get-Content -LiteralPath $testResult -Raw
        $testRoot = $testXml.'test-run'
        if ($null -eq $testRoot -or [int]$testRoot.total -lt 1 -or $testRoot.result -ne 'Passed' -or [int]$testRoot.failed -ne 0) {
            throw "$testSuite did not pass: $testResult"
        }
        Write-Output "$testSuite passed: $($testRoot.passed) tests."
    } else { Get-Content -LiteralPath $testResult }
}
