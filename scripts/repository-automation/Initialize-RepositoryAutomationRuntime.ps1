#Requires -Version 7.0
<#
.SYNOPSIS
    Resolves and verifies the repository-automation runtime capability.

.DESCRIPTION
    Public generated-project command. Installs the exact bundled runtime package into
    job-local storage on GitHub-hosted and self-hosted runners, then verifies the
    command identity, version, contract, and transport requirements.
#>
[CmdletBinding()]
param(
    [string]$DistributionPath =
        '.github/repository-automation/runtime-distribution.json',

    [Parameter(Mandatory)]
    [ValidateSet('github-hosted', 'self-hosted')]
    [string]$RunnerEnvironment,

    [string]$GitHubPath,

    [string]$ToolRoot
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'RepositoryAutomation.Functions.ps1')
if ([IO.Path]::IsPathRooted($DistributionPath)) {
    throw 'DistributionPath must be repository-relative.'
}
$distributionFullPath = [IO.Path]::GetFullPath(
    (Join-Path (Get-Location).Path $DistributionPath))
$repositoryPrefix = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar
) + [IO.Path]::DirectorySeparatorChar
$comparison = if ($IsWindows) {
    [StringComparison]::OrdinalIgnoreCase
} else {
    [StringComparison]::Ordinal
}
if (-not $distributionFullPath.StartsWith(
    $repositoryPrefix,
    $comparison
)) {
    throw 'DistributionPath must stay within the repository.'
}
if (-not (Test-Path -LiteralPath $distributionFullPath -PathType Leaf)) {
    throw "Runtime distribution contract is missing: $DistributionPath"
}
$distributionText = Get-Content `
    -LiteralPath $distributionFullPath `
    -Raw `
    -Encoding UTF8
$distribution = $distributionText | ConvertFrom-Json
$distributionDirectory = Split-Path $distributionFullPath -Parent
$schemaPath = Join-Path $distributionDirectory (
    [string]$distribution.'$schema')
if (-not (Test-Path -LiteralPath $schemaPath -PathType Leaf)) {
    throw 'Runtime distribution schema is missing.'
}
$schemaErrors = @()
$distributionValid = Test-Json `
    -Json $distributionText `
    -Schema (Get-Content $schemaPath -Raw -Encoding UTF8) `
    -ErrorAction SilentlyContinue `
    -ErrorVariable schemaErrors
if (-not $distributionValid) {
    throw "Runtime distribution contract is invalid: $($schemaErrors -join '; ')"
}
if ($RunnerEnvironment -ceq 'github-hosted' -and
    [string]$distribution.installation.github_hosted -ceq 'unavailable') {
    throw 'capability-unavailable: this repository does not distribute the private repository-automation runtime to GitHub-hosted runners.'
}

$null = Get-Command dotnet -CommandType Application -ErrorAction Stop
if ([string]::IsNullOrWhiteSpace($ToolRoot)) {
    $baseTemp =
        if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
            [IO.Path]::GetTempPath()
        } else {
            $env:RUNNER_TEMP
        }
    $ToolRoot = Join-Path $baseTemp 'repository-automation-tool'
}
$packageSource = [IO.Path]::GetFullPath(
    (Join-Path $distributionDirectory (
        [string]$distribution.runtime.package_source
    )))
$packageSourcePrefix = $distributionDirectory.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar
) + [IO.Path]::DirectorySeparatorChar
if (-not $packageSource.StartsWith(
    $packageSourcePrefix,
    $comparison
)) {
    throw 'capability-unavailable: bundled package source escapes the distribution directory.'
}
$packagePath = Join-Path $packageSource (
    [string]$distribution.runtime.package_file)
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    throw 'capability-unavailable: bundled runtime package is missing.'
}
$installationRoot = Join-Path $ToolRoot (
    "v$([string]$distribution.runtime.version)")
if (Test-Path -LiteralPath $installationRoot) {
    Remove-Item -LiteralPath $installationRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $installationRoot -Force |
    Out-Null
$toolName =
    if ($IsWindows) {
        'repository-automation.exe'
    } else {
        'repository-automation'
    }
$toolExecutable = Join-Path $installationRoot $toolName
$nugetConfigPath = Join-Path $installationRoot 'NuGet.config'
$escapedSource = [Security.SecurityElement]::Escape(
    $packageSource)
[IO.File]::WriteAllText(
    $nugetConfigPath,
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="repository-automation" value="$escapedSource" />
  </packageSources>
</configuration>
"@,
    [Text.UTF8Encoding]::new($false))
try {
    $installOutput = @(
        & dotnet tool install `
            ([string]$distribution.runtime.package_id) `
            --tool-path $installationRoot `
            --version ([string]$distribution.runtime.version) `
            --configfile $nugetConfigPath `
            --no-cache 2>&1
    )
    if ($LASTEXITCODE -ne 0) {
        throw "capability-unavailable: exact runtime package installation failed: $($installOutput -join "`n")"
    }
} finally {
    if (Test-Path -LiteralPath $nugetConfigPath) {
        Remove-Item -LiteralPath $nugetConfigPath -Force
    }
}

$pathSeparator = [IO.Path]::PathSeparator
$env:PATH = "$installationRoot$pathSeparator$($env:PATH)"
if (-not [string]::IsNullOrWhiteSpace($GitHubPath)) {
    Add-Content `
        -LiteralPath $GitHubPath `
        -Encoding UTF8 `
        -Value $installationRoot
}

$capabilityResult = Invoke-RepositoryAutomationCommand `
    -Arguments @(
        'capabilities',
        '--contract-version',
        [string]$distribution.runtime.contract_version
    ) `
    -MaximumOutputBytes 65536
if ($capabilityResult.ExitCode -ne 0) {
    throw "capability-unavailable: runtime capability command failed with exit code $($capabilityResult.ExitCode): $($capabilityResult.Error)"
}
try {
    $capabilities = $capabilityResult.Output | ConvertFrom-Json
} catch {
    throw "capability-unavailable: runtime capability response is invalid JSON: $($_.Exception.Message)"
}
if ([string]$capabilities.runtime.name -cne
    [string]$distribution.runtime.command) {
    throw 'capability-unavailable: runtime command identity is incorrect.'
}
if ([string]$capabilities.runtime.version -cne
    [string]$distribution.runtime.version) {
    throw "capability-unavailable: installed runtime $($capabilities.runtime.version) does not match exact version $($distribution.runtime.version)."
}
if (@($capabilities.supported_contract_versions) -notcontains
    [string]$distribution.runtime.contract_version) {
    throw 'capability-unavailable: runtime contract version is unsupported.'
}
if (
    [bool]$capabilities.requirements.docker -or
    [bool]$capabilities.requirements.actions_artifacts -or
    [bool]$capabilities.requirements.actions_cache
) {
    throw 'capability-unavailable: runtime requires unsupported execution transport.'
}

return $capabilities