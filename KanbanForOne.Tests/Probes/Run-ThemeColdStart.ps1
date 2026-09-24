param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\..\artifacts\ui-refresh\theme-hover\cold-start'),
    [switch]$VerifyPointer
)

$ErrorActionPreference = 'Stop'
$probeProject = Join-Path $PSScriptRoot 'ThemeColdStart\ThemeColdStart.csproj'
$isolatedRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('KanbanForOne-ThemeColdStart-' + [Guid]::NewGuid().ToString('N'))
$appDirectory = Join-Path $isolatedRoot 'app'
$null = New-Item -ItemType Directory -Path $appDirectory -Force
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

# The apphost lives inside this fresh temp directory. AppPaths therefore points
# all SQLite, options, attachments and preference writes into isolated storage.
dotnet build $probeProject -c Release -o $appDirectory --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Cold-start probe build failed.' }
Set-Content -LiteralPath (Join-Path $appDirectory '.isolated-theme-probe') -Value 'Test-only disposable application root'
$executable = Join-Path $appDirectory 'ThemeColdStart.exe'
$modes = @('save-dark', 'verify-dark')
if ($VerifyPointer) { $modes += 'verify-pointer' }
foreach ($mode in $modes) {
    $resultFile = Join-Path $OutputDirectory ($mode + '.json')
    $errorFile = Join-Path $OutputDirectory ($mode + '.stderr.txt')
    $process = Start-Process -FilePath $executable -ArgumentList @($mode, ('"' + $resultFile + '"')) -WorkingDirectory $appDirectory -WindowStyle Hidden -RedirectStandardError $errorFile -PassThru
    if (-not $process.WaitForExit(30000)) {
        $process.Kill()
        throw "Cold-start probe timed out: $mode"
    }
    if ($process.ExitCode -ne 0) {
        $errorText = Get-Content -LiteralPath $errorFile -Raw
        throw "Cold-start probe failed ($mode, $($process.ExitCode)): $errorText"
    }
    Get-Content -LiteralPath $resultFile -Raw
}
$first = Get-Content -LiteralPath (Join-Path $OutputDirectory 'save-dark.json') -Raw | ConvertFrom-Json
$second = Get-Content -LiteralPath (Join-Path $OutputDirectory 'verify-dark.json') -Raw | ConvertFrom-Json
if ($first.processId -eq $second.processId) { throw 'Cold-start validation must use two different processes.' }
Write-Output "Cold-start persistence passed in two isolated processes. Disposable app directory: $appDirectory"
