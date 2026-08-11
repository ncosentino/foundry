#Requires -Version 7.0
<#
.SYNOPSIS
    Builds one bounded repository-automation work request.

.DESCRIPTION
    Public generated-project command. Reads trusted workflow context and the selected
    module configuration, resolves only the requested issue identity when necessary,
    and emits a contract-version-1 work request. Raw event payload content is never
    copied into the request.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$')]
    [string]$ModuleId,

    [Parameter(Mandatory)]
    [ValidatePattern('^[1-9][0-9]*$')]
    [string]$ModuleVersion,

    [Parameter(Mandatory)]
    [string]$ConfigurationPath,

    [Parameter(Mandatory)]
    [ValidateSet('event', 'schedule', 'manual', 'dispatch')]
    [string]$Trigger,

    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z][a-z0-9_-]*$')]
    [string]$EventName,

    [Parameter(Mandatory)]
    [string]$RepositoryId,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$Repository,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$Revision,

    [Parameter(Mandatory)]
    [string]$DeliveryId,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$')]
    [string]$RequestId,

    [ValidateRange(0, [int]::MaxValue)]
    [int]$WorkItemNumber = 0,

    [string]$WorkItemProviderId,

    [string]$DispatchType,

    [string]$GitHubOutputPath
)

$ErrorActionPreference = 'Stop'

if (-not [string]::IsNullOrWhiteSpace($DispatchType) -and
    $DispatchType -notmatch '^[a-z][a-z0-9_-]*$') {
    throw 'DispatchType is invalid.'
}
if ([IO.Path]::IsPathRooted($ConfigurationPath)) {
    throw 'ConfigurationPath must be repository-relative.'
}
$configurationFullPath = [IO.Path]::GetFullPath(
    (Join-Path (Get-Location).Path $ConfigurationPath))
$repositoryRoot = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar
) + [IO.Path]::DirectorySeparatorChar
$pathComparison = if ($IsWindows) {
    [StringComparison]::OrdinalIgnoreCase
} else {
    [StringComparison]::Ordinal
}
if (-not $configurationFullPath.StartsWith(
    $repositoryRoot,
    $pathComparison
)) {
    throw 'ConfigurationPath must stay within the repository.'
}
if (-not (Test-Path -LiteralPath $configurationFullPath -PathType Leaf)) {
    throw "Repository-automation configuration is missing: $ConfigurationPath"
}

$configurationText = Get-Content `
    -LiteralPath $configurationFullPath `
    -Raw `
    -Encoding UTF8
$configuration = $configurationText | ConvertFrom-Json
$module = @(
    $configuration.modules |
        Where-Object {
            [string]$_.id -ceq $ModuleId -and
            [string]$_.version -ceq $ModuleVersion -and
            [bool]$_.enabled
        }
)
if ($module.Count -ne 1) {
    throw "Configuration must enable exactly one $ModuleId v$ModuleVersion module."
}

if ($WorkItemNumber -gt 0 -and
    [string]::IsNullOrWhiteSpace($WorkItemProviderId)) {
    if ([string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN) -or
        [string]::IsNullOrWhiteSpace($env:GITHUB_API_URL)) {
        throw 'Resolving a manual work item requires GITHUB_TOKEN and GITHUB_API_URL.'
    }
    $headers = @{
        Accept                 = 'application/vnd.github+json'
        Authorization          = "Bearer $($env:GITHUB_TOKEN)"
        'X-GitHub-Api-Version' = '2022-11-28'
    }
    $issue = Invoke-RestMethod `
        -Method Get `
        -Uri "$($env:GITHUB_API_URL.TrimEnd('/'))/repos/$Repository/issues/$WorkItemNumber" `
        -Headers $headers
    $WorkItemProviderId = [string]$issue.node_id
}

$workItem =
    if ($WorkItemNumber -gt 0) {
        if ([string]::IsNullOrWhiteSpace($WorkItemProviderId)) {
            throw 'Work item provider identity is required.'
        }
        [ordered]@{
            kind        = 'issue'
            provider_id = $WorkItemProviderId
            number      = $WorkItemNumber
        }
    } else {
        $null
    }
$hint = [ordered]@{}
if ($WorkItemNumber -gt 0) {
    $hint.issue_number = $WorkItemNumber
}
if ($Trigger -ceq 'dispatch') {
    if ([string]::IsNullOrWhiteSpace($DispatchType)) {
        throw 'Dispatch work requests require DispatchType.'
    }
    $hint.dispatch_type = $DispatchType
}

$configurationHash = "sha256:$(
    (Get-FileHash -LiteralPath $configurationFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
)"
$identity = @(
    $RepositoryId,
    $(if ($null -eq $workItem) { 'none' } else { $workItem.provider_id }),
    $ModuleId,
    [string]$configuration.configuration_version,
    $Revision
) -join "`n"
$identityHash = [Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($identity))
$idempotencyKey = "sha256:$(
    [Convert]::ToHexString($identityHash).ToLowerInvariant()
)"
$request = [ordered]@{
    '$schema'       = 'urn:repository-automation:work-request:1'
    schema_version  = '1'
    request_id      = $RequestId
    idempotency_key = $idempotencyKey
    repository      = [ordered]@{
        provider  = 'github'
        id        = $RepositoryId
        full_name = $Repository
    }
    module           = [ordered]@{
        id      = $ModuleId
        version = $ModuleVersion
    }
    configuration    = [ordered]@{
        version = [int]$configuration.configuration_version
        sha256  = $configurationHash
    }
    source           = [ordered]@{
        trigger        = $Trigger
        event_name     = $EventName
        delivery_id    = $DeliveryId
        revision       = $Revision
        work_item      = $workItem
        untrusted_hint = $hint
    }
    requested_at      = [DateTimeOffset]::UtcNow.ToString('O')
}
$json = $request | ConvertTo-Json -Depth 12 -Compress

if (-not [string]::IsNullOrWhiteSpace($GitHubOutputPath)) {
    . (Join-Path $PSScriptRoot 'RepositoryAutomation.Functions.ps1')
    Write-GitHubOutputValue `
        -Path $GitHubOutputPath `
        -Name 'request' `
        -Value $json
}

return $json
