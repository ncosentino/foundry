#Requires -Version 7.0
<#
.SYNOPSIS
    Defines generated-project repository-automation command helpers.

.DESCRIPTION
    This dot-sourced library contains functions only. It invokes the stable
    repository-automation command, validates capability responses, bounds output, and
    writes multiline GitHub step outputs without interpreting model-produced text.
#>

function Invoke-RepositoryAutomationCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [string]$InputJson,

        [ValidateRange(1, [int]::MaxValue)]
        [int]$MaximumOutputBytes = 262144
    )

    $null = Get-Command repository-automation -ErrorAction Stop
    $errorPath = Join-Path (
        [IO.Path]::GetTempPath()
    ) "repository-automation-error-$([Guid]::NewGuid().ToString('N')).txt"
    try {
        $output =
            if ($PSBoundParameters.ContainsKey('InputJson')) {
                @(
                    $InputJson |
                        & repository-automation @Arguments 2> $errorPath
                ) -join "`n"
            } else {
                @(& repository-automation @Arguments 2> $errorPath) -join "`n"
            }
        $exitCode = $LASTEXITCODE
        $errorText =
            if (Test-Path -LiteralPath $errorPath -PathType Leaf) {
                Get-Content -LiteralPath $errorPath -Raw -Encoding UTF8
            } else {
                ''
            }
        $output = [string]$output
        $outputBytes = [Text.Encoding]::UTF8.GetByteCount($output)
        if ($outputBytes -gt $MaximumOutputBytes) {
            throw "repository-automation output is $outputBytes bytes; maximum is $MaximumOutputBytes bytes."
        }

        return [PSCustomObject]@{
            ExitCode = $exitCode
            Output   = $output
            Error    =
                if ($null -eq $errorText) {
                    ''
                } else {
                    ([string]$errorText).Trim()
                }
        }
    } finally {
        if (Test-Path -LiteralPath $errorPath) {
            Remove-Item -LiteralPath $errorPath -Force
        }
    }
}

function Test-SemanticVersionAtLeast {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Actual,

        [Parameter(Mandatory)]
        [string]$Minimum
    )

    $pattern =
        '^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:-(?<pre>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$'
    $actualMatch = [regex]::Match($Actual, $pattern)
    $minimumMatch = [regex]::Match($Minimum, $pattern)
    if (-not $actualMatch.Success -or -not $minimumMatch.Success) {
        throw "Runtime or minimum version is not valid SemVer: '$Actual', '$Minimum'."
    }

    foreach ($part in @('major', 'minor', 'patch')) {
        $actualPart = [int]$actualMatch.Groups[$part].Value
        $minimumPart = [int]$minimumMatch.Groups[$part].Value
        if ($actualPart -gt $minimumPart) { return $true }
        if ($actualPart -lt $minimumPart) { return $false }
    }

    $actualPre = [string]$actualMatch.Groups['pre'].Value
    $minimumPre = [string]$minimumMatch.Groups['pre'].Value
    if ([string]::IsNullOrEmpty($minimumPre)) {
        return [string]::IsNullOrEmpty($actualPre)
    }
    if ([string]::IsNullOrEmpty($actualPre)) {
        return $true
    }

    $actualIdentifiers = $actualPre.Split('.')
    $minimumIdentifiers = $minimumPre.Split('.')
    $count = [Math]::Max(
        $actualIdentifiers.Count,
        $minimumIdentifiers.Count)
    for ($index = 0; $index -lt $count; $index++) {
        if ($index -ge $actualIdentifiers.Count) { return $false }
        if ($index -ge $minimumIdentifiers.Count) { return $true }
        $actualIdentifier = $actualIdentifiers[$index]
        $minimumIdentifier = $minimumIdentifiers[$index]
        $actualNumeric = 0
        $minimumNumeric = 0
        $actualIsNumeric = [int]::TryParse(
            $actualIdentifier,
            [ref]$actualNumeric)
        $minimumIsNumeric = [int]::TryParse(
            $minimumIdentifier,
            [ref]$minimumNumeric)
        if ($actualIsNumeric -and $minimumIsNumeric) {
            if ($actualNumeric -gt $minimumNumeric) { return $true }
            if ($actualNumeric -lt $minimumNumeric) { return $false }
            continue
        }
        if ($actualIsNumeric -and -not $minimumIsNumeric) { return $false }
        if (-not $actualIsNumeric -and $minimumIsNumeric) { return $true }
        $comparison = [string]::CompareOrdinal(
            $actualIdentifier,
            $minimumIdentifier)
        if ($comparison -gt 0) { return $true }
        if ($comparison -lt 0) { return $false }
    }

    return $true
}

function Assert-RepositoryAutomationRuntime {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$MinimumVersion,

        [string]$ContractVersion = '1',

        [Parameter(Mandatory)]
        [string]$ModuleId,

        [Parameter(Mandatory)]
        [string]$ModuleVersion,

        [string[]]$RequiredOperations = @(),

        [ValidateRange(1, [int]::MaxValue)]
        [int]$RequiredProposalBytes = 262144
    )

    $result = Invoke-RepositoryAutomationCommand `
        -Arguments @(
            'capabilities',
            '--contract-version',
            $ContractVersion
        ) `
        -MaximumOutputBytes 65536
    if ($result.ExitCode -ne 0) {
        throw "repository-automation capability preflight failed with exit code $($result.ExitCode): $($result.Error)"
    }

    try {
        $capabilities = $result.Output | ConvertFrom-Json
    } catch {
        throw "repository-automation capability response is not valid JSON: $($_.Exception.Message)"
    }
    $runtimeVersion = [string]$capabilities.runtime.version
    if (-not (Test-SemanticVersionAtLeast `
        -Actual $runtimeVersion `
        -Minimum $MinimumVersion)) {
        throw "repository-automation $runtimeVersion is older than required $MinimumVersion."
    }
    if (@($capabilities.supported_contract_versions) -notcontains
        $ContractVersion) {
        throw "repository-automation does not support contract version $ContractVersion."
    }
    if (
        [bool]$capabilities.requirements.docker -or
        [bool]$capabilities.requirements.actions_artifacts -or
        [bool]$capabilities.requirements.actions_cache
    ) {
        throw 'repository-automation requires unsupported Docker, artifact, or cache transport.'
    }
    $module = @(
        $capabilities.modules |
            Where-Object {
                [string]$_.id -ceq $ModuleId -and
                @($_.versions) -contains $ModuleVersion
            }
    )
    if ($module.Count -ne 1) {
        throw "repository-automation does not provide $ModuleId v$ModuleVersion."
    }
    foreach ($operation in $RequiredOperations) {
        if (@($capabilities.operations) -notcontains $operation) {
            throw "repository-automation does not provide operation '$operation'."
        }
    }
    if ([int]$capabilities.limits.proposal_bytes -lt
        $RequiredProposalBytes) {
        throw "repository-automation proposal limit is smaller than required $RequiredProposalBytes bytes."
    }

    return $capabilities
}

function Write-GitHubOutputValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Name,

        [AllowEmptyString()]
        [Parameter(Mandatory)]
        [string]$Value
    )

    $delimiter = "REPOSITORY_AUTOMATION_$([Guid]::NewGuid().ToString('N'))"
    if ($Value.Contains($delimiter, [StringComparison]::Ordinal)) {
        throw 'Generated GitHub output delimiter collides with output content.'
    }
    Add-Content -LiteralPath $Path -Encoding UTF8 -Value @(
        "$Name<<$delimiter",
        $Value,
        $delimiter
    )
}
