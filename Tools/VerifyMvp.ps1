[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath,
    [switch]$BuildPlayer
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$logsRoot = Join-Path $projectRoot 'Logs'
$testResults = Join-Path $logsRoot 'EditMode.VerifyMvp.xml'
$testLog = Join-Path $logsRoot 'EditMode.VerifyMvp.log'

New-Item -ItemType Directory -Force -Path $logsRoot | Out-Null

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity executable was not found: $UnityPath"
}

$runningUnity = Get-Process Unity -ErrorAction SilentlyContinue
if ($null -ne $runningUnity) {
    throw 'Close the Unity editor before running the headless verification loop.'
}

Push-Location $projectRoot
try {
    & (Join-Path $PSScriptRoot 'VerifyStaticMvp.ps1')

    $testProcess = Start-Process -FilePath $UnityPath -ArgumentList @(
        '-batchmode',
        '-projectPath', $projectRoot,
        '-runTests',
        '-testPlatform', 'EditMode',
        '-testResults', $testResults,
        '-logFile', $testLog
    ) -Wait -PassThru
    if ($testProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $testResults)) {
        throw "EditMode test process failed. Inspect $testLog"
    }

    [xml]$testXml = Get-Content -Raw -LiteralPath $testResults
    $testRun = $testXml.'test-run'
    if ($testRun.result -ne 'Passed' -or [int]$testRun.failed -ne 0) {
        throw "EditMode tests did not pass. Inspect $testResults"
    }

    if ($BuildPlayer) {
        $buildLog = Join-Path $logsRoot 'Build.WindowsMvp.VerifyMvp.log'
        $previousBuildPath = $env:HUNTERWIDOW_BUILD_PATH
        try {
            $env:HUNTERWIDOW_BUILD_PATH = Join-Path $projectRoot 'Builds\HunterWidowMvp.exe'
            $buildProcess = Start-Process -FilePath $UnityPath -ArgumentList @(
                '-batchmode',
                '-quit',
                '-projectPath', $projectRoot,
                '-executeMethod', 'HunterWidow.Editor.HunterWidowBuild.BuildWindowsMvp',
                '-logFile', $buildLog
            ) -Wait -PassThru
            if ($buildProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $buildLog)) {
                throw "Windows build process failed. Inspect $buildLog"
            }

            if (-not (Select-String -LiteralPath $buildLog -SimpleMatch 'Build Finished, Result: Success.' -Quiet)) {
                throw "Windows build did not report success. Inspect $buildLog"
            }
        }
        finally {
            $env:HUNTERWIDOW_BUILD_PATH = $previousBuildPath
        }
    }

    Write-Output "MVP verification passed. EditMode: $($testRun.passed)/$($testRun.total)."
}
finally {
    Pop-Location
}
