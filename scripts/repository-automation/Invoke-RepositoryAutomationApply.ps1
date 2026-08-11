#Requires -Version 7.0
<#
.SYNOPSIS
    Invokes deterministic repository-automation apply.

.DESCRIPTION
    Public generated-project command. Verifies runtime capabilities, requires the
    narrow apply credential and configured automation principal, invokes deterministic
    apply, and records a bounded status summary. Nonzero runtime outcomes fail the job.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ConfigurationPath,

    [Parameter(Mandatory)]
    [string]$ProposalJson,

    [Parameter(Mandatory)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$ExpectedProposalBytes,

    [Parameter(Mandatory)]
    [ValidatePattern('^sha256:[0-9a-f]{64}$')]
    [string]$ExpectedProposalSha256,

    [Parameter(Mandatory)]
    [string]$ModuleId,

    [Parameter(Mandatory)]
    [string]$ModuleVersion,

    [string[]]$RequiredOperations = @(),

    [string]$MinimumRuntimeVersion = '0.1.0',

    [string]$GitHubStepSummaryPath
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'RepositoryAutomation.Functions.ps1')
if ([string]::IsNullOrWhiteSpace(
    $env:REPOSITORY_AUTOMATION_APPLY_TOKEN
)) {
    throw 'Apply requires REPOSITORY_AUTOMATION_APPLY_TOKEN.'
}
if ([string]::IsNullOrWhiteSpace($env:REPOSITORY_AUTOMATION_ACTOR)) {
    throw 'Apply requires REPOSITORY_AUTOMATION_ACTOR.'
}
$proposalBytes = [Text.Encoding]::UTF8.GetByteCount($ProposalJson)
if ($proposalBytes -ne $ExpectedProposalBytes) {
    throw "Proposal byte count changed between jobs: expected $ExpectedProposalBytes, got $proposalBytes."
}
$proposalHash = "sha256:$(
    [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData(
            [Text.Encoding]::UTF8.GetBytes($ProposalJson)
        )
    ).ToLowerInvariant()
)"
if ($proposalHash -cne $ExpectedProposalSha256) {
    throw 'Proposal SHA-256 changed between jobs.'
}

$null = Assert-RepositoryAutomationRuntime `
    -MinimumVersion $MinimumRuntimeVersion `
    -ModuleId $ModuleId `
    -ModuleVersion $ModuleVersion `
    -RequiredOperations $RequiredOperations `
    -RequiredProposalBytes $ExpectedProposalBytes
$result = Invoke-RepositoryAutomationCommand `
    -Arguments @(
        'apply',
        '--contract-version',
        '1',
        '--configuration',
        $ConfigurationPath
    ) `
    -InputJson $ProposalJson
$incompleteOperation = $null
if (-not [string]::IsNullOrWhiteSpace($result.Output)) {
    try {
        $applyResult = $result.Output | ConvertFrom-Json
        $incompleteOperation = @(
            $applyResult.operation_results |
                Where-Object {
                    [string]$_.status -in @('failed', 'rejected')
                } |
                Select-Object -Last 1
        )
        if (-not [string]::IsNullOrWhiteSpace($GitHubStepSummaryPath)) {
            Add-Content `
                -LiteralPath $GitHubStepSummaryPath `
                -Encoding UTF8 `
                -Value "Repository automation status: ``$($applyResult.status)``"
            if ($incompleteOperation.Count -eq 1) {
                Add-Content `
                    -LiteralPath $GitHubStepSummaryPath `
                    -Encoding UTF8 `
                    -Value (
                        "Incomplete operation: ``$([string]$incompleteOperation[0].operation_id)`` " +
                        "(``$([string]$incompleteOperation[0].kind)``), " +
                        "failure ``$([string]$incompleteOperation[0].failure.code)``"
                    )
            }
        }
    } catch {
        throw "repository-automation apply output is not valid JSON: $($_.Exception.Message)"
    }
}
if ($result.ExitCode -ne 0) {
    $operationDetail =
        if ($incompleteOperation.Count -eq 1) {
            "; incomplete operation '$([string]$incompleteOperation[0].operation_id)' " +
            "($([string]$incompleteOperation[0].kind), " +
            "$([string]$incompleteOperation[0].failure.code))"
        } else {
            ''
        }
    throw "repository-automation apply failed with exit code $($result.ExitCode)$operationDetail`: $($result.Error)"
}
if ([string]::IsNullOrWhiteSpace($result.Output)) {
    throw 'repository-automation apply returned no result.'
}

return $result.Output
