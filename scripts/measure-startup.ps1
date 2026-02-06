param(
    [int]$WarmRuns = 7,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$NoBuild,
    [string]$OutputPath = "artifacts/startup-benchmark.csv"
)

$ErrorActionPreference = "Stop"

function Get-Median {
    param([double[]]$Values)

    if (-not $Values -or $Values.Count -eq 0) {
        return [double]::NaN
    }

    $sorted = $Values | Sort-Object
    $count = $sorted.Count

    if ($count % 2 -eq 1) {
        return [double]$sorted[[int]($count / 2)]
    }

    $left = [double]$sorted[($count / 2) - 1]
    $right = [double]$sorted[$count / 2]
    return ($left + $right) / 2.0
}

function Invoke-StartupRun {
    param(
        [string]$AppDll,
        [string]$TempOutputPath,
        [string]$Phase,
        [int]$Run
    )

    $beforeCount = if (Test-Path $TempOutputPath) { @(Get-Content $TempOutputPath).Count } else { 0 }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "dotnet"
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = ('"{0}" --startup-benchmark --startup-benchmark-output "{1}"' -f $AppDll, $TempOutputPath)

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $null = $process.Start()

    if (-not $process.WaitForExit(30000)) {
        try { $process.Kill($true) } catch { }
        throw "Startup benchmark timeout: $Phase run $Run"
    }

    $stdOut = $process.StandardOutput.ReadToEnd()
    $stdErr = $process.StandardError.ReadToEnd()

    if ($process.ExitCode -ne 0) {
        throw "Benchmark process failed (exit code $($process.ExitCode)): $Phase run $Run`nSTDOUT:`n$stdOut`nSTDERR:`n$stdErr"
    }

    if (-not (Test-Path $TempOutputPath)) {
        throw "Benchmark output file was not created: $TempOutputPath"
    }

    $lines = @(Get-Content $TempOutputPath)
    if ($lines.Count -le $beforeCount) {
        throw "No new benchmark output line produced: $Phase run $Run"
    }

    $lastLine = [string]$lines[$lines.Count - 1]
    $parts = $lastLine.Split(",")
    if ($parts.Length -lt 2) {
        throw "Malformed benchmark line: '$lastLine'"
    }

    $elapsed = [double]::Parse($parts[1], [System.Globalization.CultureInfo]::InvariantCulture)
    return [PSCustomObject]@{
        Phase = $Phase
        Run = $Run
        TimestampUtc = $parts[0]
        ElapsedMs = $elapsed
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$framework = "net8.0-windows"
$projectPath = Join-Path $repoRoot "CalcApp\\CalcApp.csproj"
$appDll = Join-Path $repoRoot "CalcApp\\bin\\$Configuration\\$framework\\CalcApp.dll"
$outputFullPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
$tempOutputPath = Join-Path ([System.IO.Path]::GetTempPath()) ("calc-startup-" + [Guid]::NewGuid().ToString("N") + ".csv")

if (-not $NoBuild) {
    dotnet build $projectPath -c $Configuration -v minimal
}

if (-not (Test-Path $appDll)) {
    throw "App output not found: $appDll"
}

$profilePath = Join-Path $env:LOCALAPPDATA "CalcApp\\Startup.profile"
if (Test-Path $profilePath) {
    Remove-Item $profilePath -Force -ErrorAction SilentlyContinue
}

if (Test-Path $tempOutputPath) {
    Remove-Item $tempOutputPath -Force -ErrorAction SilentlyContinue
}

$results = New-Object System.Collections.Generic.List[object]
$results.Add((Invoke-StartupRun -AppDll $appDll -TempOutputPath $tempOutputPath -Phase "cold" -Run 1))

for ($i = 1; $i -le $WarmRuns; $i++) {
    $results.Add((Invoke-StartupRun -AppDll $appDll -TempOutputPath $tempOutputPath -Phase "warm" -Run $i))
}

$outDir = Split-Path -Parent $outputFullPath
if (-not [string]::IsNullOrWhiteSpace($outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$results | Export-Csv -Path $outputFullPath -NoTypeInformation -Encoding utf8

$cold = ($results | Where-Object { $_.Phase -eq "cold" } | Select-Object -First 1).ElapsedMs
$warmValues = @($results | Where-Object { $_.Phase -eq "warm" } | ForEach-Object { [double]$_.ElapsedMs })
$warmStats = $warmValues | Measure-Object -Average -Minimum -Maximum
$warmMedian = Get-Median -Values $warmValues

$summary = [PSCustomObject]@{
    ColdMs = [Math]::Round($cold, 2)
    WarmAverageMs = [Math]::Round($warmStats.Average, 2)
    WarmMedianMs = [Math]::Round($warmMedian, 2)
    WarmMinMs = [Math]::Round($warmStats.Minimum, 2)
    WarmMaxMs = [Math]::Round($warmStats.Maximum, 2)
    WarmRuns = $WarmRuns
    OutputCsv = $outputFullPath
}

$summary | Format-List

if (Test-Path $tempOutputPath) {
    Remove-Item $tempOutputPath -Force -ErrorAction SilentlyContinue
}
