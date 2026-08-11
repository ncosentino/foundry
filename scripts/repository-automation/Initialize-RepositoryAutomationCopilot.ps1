#Requires -Version 7.0
<#
.SYNOPSIS
    Installs and verifies the bounded Copilot analysis engine.

.DESCRIPTION
    Public generated-project command. Installs one exact public Copilot CLI package
    into an owned job-local directory, verifies its reported version, and exports the
    command path for the repository-automation runtime. Initial agentic adapters are
    Linux-only.
#>
[CmdletBinding()]
param(
    [string]$DistributionPath =
        '.github/repository-automation/runtime-distribution.json',

    [string]$GitHubPath,

    [string]$GitHubEnvironment,

    [string]$ToolRoot
)

$ErrorActionPreference = 'Stop'

if ($IsWindows) {
    throw 'capability-unavailable: repository-automation Copilot analysis currently requires Linux.'
}
if ([IO.Path]::IsPathRooted($DistributionPath)) {
    throw 'DistributionPath must be repository-relative.'
}
$distributionFullPath = [IO.Path]::GetFullPath(
    (Join-Path (Get-Location).Path $DistributionPath))
$repositoryPrefix = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar
) + [IO.Path]::DirectorySeparatorChar
if (-not $distributionFullPath.StartsWith(
    $repositoryPrefix,
    [StringComparison]::Ordinal
)) {
    throw 'DistributionPath must stay within the repository.'
}
$distributionText = Get-Content `
    -LiteralPath $distributionFullPath `
    -Raw `
    -Encoding UTF8
$distribution = $distributionText | ConvertFrom-Json
$schemaPath = Join-Path (
    Split-Path $distributionFullPath -Parent
) ([string]$distribution.'$schema')
$schemaErrors = @()
$distributionValid = Test-Json `
    -Json $distributionText `
    -Schema (Get-Content $schemaPath -Raw -Encoding UTF8) `
    -ErrorAction SilentlyContinue `
    -ErrorVariable schemaErrors
if (-not $distributionValid) {
    throw "Copilot distribution contract is invalid: $($schemaErrors -join '; ')"
}

$null = Get-Command npm -CommandType Application -ErrorAction Stop
if ([string]::IsNullOrWhiteSpace($ToolRoot)) {
    $baseTemp =
        if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
            [IO.Path]::GetTempPath()
        } else {
            $env:RUNNER_TEMP
        }
    $ToolRoot = Join-Path $baseTemp 'repository-automation-copilot'
}
$installationRoot = Join-Path $ToolRoot (
    "v$([string]$distribution.analysis_engine.version)")
if (Test-Path -LiteralPath $installationRoot) {
    Remove-Item -LiteralPath $installationRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $installationRoot -Force | Out-Null
$package = "$(
    [string]$distribution.analysis_engine.package_id
)@$(
    [string]$distribution.analysis_engine.version
)"
$installOutput = @(
    & npm install `
        --global `
        --prefix $installationRoot `
        $package `
        --registry ([string]$distribution.analysis_engine.package_source) `
        --no-audit `
        --no-fund 2>&1
)
if ($LASTEXITCODE -ne 0) {
    throw "capability-unavailable: exact Copilot CLI installation failed: $($installOutput -join "`n")"
}

$binPath = Join-Path $installationRoot 'bin'
$copilotPath = Join-Path $binPath (
    [string]$distribution.analysis_engine.command)
if (-not (Test-Path -LiteralPath $copilotPath -PathType Leaf)) {
    throw 'capability-unavailable: installed Copilot CLI command is missing.'
}
$versionOutput = @(
    & $copilotPath --no-auto-update version 2>&1
) -join "`n"
if ($LASTEXITCODE -ne 0 -or
    $versionOutput -notmatch
        "GitHub Copilot CLI $([regex]::Escape([string]$distribution.analysis_engine.version))(?:\D|$)") {
    throw "capability-unavailable: Copilot CLI version verification failed."
}

$env:PATH = "$binPath$([IO.Path]::PathSeparator)$($env:PATH)"
if (-not [string]::IsNullOrWhiteSpace($GitHubPath)) {
    Add-Content `
        -LiteralPath $GitHubPath `
        -Encoding UTF8 `
        -Value $binPath
}
if (-not [string]::IsNullOrWhiteSpace($GitHubEnvironment)) {
    Add-Content `
        -LiteralPath $GitHubEnvironment `
        -Encoding UTF8 `
        -Value "COPILOT_CLI_PATH=$copilotPath"
}

return [PSCustomObject]@{
    Path    = $copilotPath
    Version = [string]$distribution.analysis_engine.version
}
