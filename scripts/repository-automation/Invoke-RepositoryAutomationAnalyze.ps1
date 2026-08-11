#Requires -Version 7.0
<#
.SYNOPSIS
    Invokes read-only repository-automation analysis.

.DESCRIPTION
    Public generated-project command. Verifies runtime capabilities, refuses an apply
    credential, invokes one bounded analysis request, and writes the proposal to a
    GitHub step output without artifacts or caches.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ConfigurationPath,

    [Parameter(Mandatory)]
    [string]$RequestJson,

    [Parameter(Mandatory)]
    [string]$GitHubOutputPath,

    [Parameter(Mandatory)]
    [string]$ModuleId,

    [Parameter(Mandatory)]
    [string]$ModuleVersion,

    [string[]]$RequiredOperations = @(),

    [string]$MinimumRuntimeVersion = '0.1.0',

    [ValidateRange(1024, 262144)]
    [int]$MaximumJobOutputBytes = 262144
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'RepositoryAutomation.Functions.ps1')
if (-not [string]::IsNullOrWhiteSpace(
    $env:REPOSITORY_AUTOMATION_APPLY_TOKEN
)) {
    throw 'Analyze refuses to run while the apply credential is present.'
}

$null = Assert-RepositoryAutomationRuntime `
    -MinimumVersion $MinimumRuntimeVersion `
    -ModuleId $ModuleId `
    -ModuleVersion $ModuleVersion `
    -RequiredOperations $RequiredOperations `
    -RequiredProposalBytes $MaximumJobOutputBytes
$result = Invoke-RepositoryAutomationCommand `
    -Arguments @(
        'analyze',
        '--contract-version',
        '1',
        '--configuration',
        $ConfigurationPath
    ) `
    -InputJson $RequestJson `
    -MaximumOutputBytes $MaximumJobOutputBytes
if ($result.ExitCode -ne 0) {
    throw "repository-automation analyze failed with exit code $($result.ExitCode): $($result.Error)"
}
if ([string]::IsNullOrWhiteSpace($result.Output)) {
    throw 'repository-automation analyze returned no proposal.'
}
$proposalBytes = [Text.Encoding]::UTF8.GetByteCount($result.Output)
$proposalHash = "sha256:$(
    [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData(
            [Text.Encoding]::UTF8.GetBytes($result.Output)
        )
    ).ToLowerInvariant()
)"

Write-GitHubOutputValue `
    -Path $GitHubOutputPath `
    -Name 'proposal' `
    -Value $result.Output
Write-GitHubOutputValue `
    -Path $GitHubOutputPath `
    -Name 'proposal_bytes' `
    -Value ([string]$proposalBytes)
Write-GitHubOutputValue `
    -Path $GitHubOutputPath `
    -Name 'proposal_sha256' `
    -Value $proposalHash
return $result.Output
