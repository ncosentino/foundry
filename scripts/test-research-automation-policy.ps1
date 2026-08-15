param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$DistributionRoot,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Contract {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-RepositoryFile {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [string]$RelativePath
    )

    $path = Join-Path $Root (
        $RelativePath -replace '/', [IO.Path]::DirectorySeparatorChar)
    Assert-Contract (
        Test-Path -LiteralPath $path -PathType Leaf
    ) "Required file '$RelativePath' does not exist."
    return $path
}

function Test-ResearchPolicyContract {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [string]$SharedDistributionRoot
    )

    $Root = [IO.Path]::GetFullPath((Resolve-Path $Root).Path)
    $configurationPath = Get-RepositoryFile `
        $Root '.github/repository-automation/config-research-needed.json'
    $policyPath = Get-RepositoryFile `
        $Root '.github/repository-automation/policies/research-needed.json'
    $workflowPath = Get-RepositoryFile `
        $Root '.github/workflows/repository-automation-research-needed.yml'
    $profilePath = Get-RepositoryFile `
        $Root '.pitcrew/runner-profile.json'

    foreach ($forbiddenPath in @(
        '.github/repository-automation/packages',
        '.github/repository-automation/schemas',
        '.github/repository-automation/runtime-distribution.json',
        '.github/repository-automation/runtime-distribution.schema.json',
        'scripts/repository-automation'
    )) {
        Assert-Contract (
            -not (Test-Path -LiteralPath (
                Join-Path $Root ($forbiddenPath -replace '/', '\')))
        ) "Foundry must not carry shared repository-automation surface '$forbiddenPath'."
    }

    $configuration = Get-Content -LiteralPath $configurationPath -Raw |
        ConvertFrom-Json
    $module = @($configuration.modules)
    Assert-Contract (
        [string]$configuration.runtime.minimum_version -ceq '0.8.4' -and
        [string]$configuration.runtime.contract_version -ceq '1'
    ) 'Foundry must select shared repository-automation 0.8.4 contract 1.'
    Assert-Contract (
        $module.Count -eq 1 -and
        [string]$module[0].id -ceq 'research-needed' -and
        [string]$module[0].version -ceq '1' -and
        @($module[0].triggers).Count -eq 1 -and
        [string]$module[0].triggers[0].kind -ceq 'manual'
    ) 'Foundry research configuration must select only manual research-needed v1.'
    Assert-Contract (
        [string]$module[0].settings.trigger_label -ceq 'research-needed' -and
        [string]$module[0].settings.completion_label -ceq 'research-completed' -and
        [bool]$module[0].settings.publish_to_body -eq $false -and
        @($module[0].settings.allowed_url_prefixes) -join ',' -ceq
            'https://github.com/'
    ) 'Foundry research labels, publication mode, or source allowlist changed.'

    $policy = Get-Content -LiteralPath $policyPath -Raw |
        ConvertFrom-Json
    Assert-Contract (
        @($policy.operations) -join ',' -ceq
            'issue.labels.update,issue.comment.upsert,issue.body-section.upsert' -and
        [int]$policy.limits.operations -eq 3 -and
        [int]$policy.limits.created_issues -eq 0 -and
        [int]$policy.limits.relationships -eq 0 -and
        [int]$policy.limits.dispatches -eq 0
    ) 'Foundry research mutation policy changed.'

    $workflow = Get-Content -LiteralPath $workflowPath -Raw -Encoding UTF8
    Assert-Contract (
        $workflow -match '(?m)^# live-llm-workflow:\s*true\s*$' -and
        $workflow -match '(?m)^  workflow_dispatch:\s*$'
    ) 'Research workflow must remain an explicitly marked manual live-LLM adapter.'
    foreach ($forbidden in @(
        '(?m)^  issues:\s*$',
        '(?m)^  issue_comment:\s*$',
        '(?m)^  pull_request(?:_target)?:\s*$',
        '(?m)^  push:\s*$',
        '(?m)^  schedule:\s*$',
        '(?m)^  repository_dispatch:\s*$',
        'actions/upload-artifact@',
        'actions/cache@',
        '(?i)\bdocker\s'
    )) {
        Assert-Contract (
            $workflow -notmatch $forbidden
        ) "Research workflow contains forbidden trigger or transport '$forbidden'."
    }
    Assert-Contract (
        ([regex]::Matches(
            $workflow,
            '(?m)^    runs-on:\s*foundry-ci\s*$')).Count -eq 3 -and
        $workflow -match "github\.actor == 'ncosentino'" -and
        $workflow -match "github\.triggering_actor == 'ncosentino'" -and
        $workflow -match 'inputs\.confirm_live_llm == true' -and
        $workflow -match 'IsNullOrWhiteSpace\(\$env:AUTHORIZATION_REASON\)'
    ) 'Research workflow authorization or runner boundary changed.'
    Assert-Contract (
        $workflow -match
            'group:\s*repository-automation-issue-\$\{\{\s*github\.repository_id\s*\}\}-\$\{\{\s*inputs\.work_item_number\s*\}\}' -and
        $workflow -match
            '/mnt/pitcrew-data/repository-automation/v0\.8\.4' -and
        $workflow -match
            '9c12dd6a66ded396436a0308d70ebc7b5293134ab5eb0eb341784497d100c7be'
    ) 'Research workflow shared concurrency or distribution pin changed.'
    Assert-Contract (
        $workflow -notmatch
            '(?i)&\s*"\./scripts/repository-automation/(?:New-|Invoke-)' -and
        $workflow -match
            'REPOSITORY_AUTOMATION_DISTRIBUTION_ROOT/scripts/repository-automation' -and
        ([regex]::Matches(
            $workflow,
            '(?m)^\s{8}working-directory:\s*/mnt/pitcrew-data/repository-automation/v0\.8\.4\s*$'
        )).Count -eq 3
    ) 'Research workflow must invoke shared adapter helpers rather than local copies.'
    Assert-Contract (
        $workflow -match '(?m)^\s{6}copilot-requests:\s*write\s*$' -and
        $workflow -match '(?m)^\s{6}issues:\s*write\s*$'
    ) 'Research workflow must retain separate Copilot and issue-write jobs.'

    $profile = Get-Content -LiteralPath $profilePath -Raw |
        ConvertFrom-Json
    Assert-Contract (
        @($profile.readOnlyVolumes).Count -eq 1 -and
        [string]$profile.readOnlyVolumes[0].name -ceq 'repository-automation' -and
        [string]$profile.readOnlyVolumes[0].source -ceq
            'foundry-repository-automation'
    ) 'PitCrew must mount one operator-owned shared automation distribution.'
    $verification = @($profile.verificationCommands) -join "`n"
    Assert-Contract (
        $verification -match
            '9c12dd6a66ded396436a0308d70ebc7b5293134ab5eb0eb341784497d100c7be' -and
        $verification -match
            'Invoke-RepositoryAutomationConformance\.ps1'
    ) 'PitCrew must verify the immutable distribution and shared conformance.'

    if ([string]::IsNullOrWhiteSpace($SharedDistributionRoot)) {
        return
    }

    $SharedDistributionRoot = [IO.Path]::GetFullPath(
        (Resolve-Path -LiteralPath $SharedDistributionRoot).Path)
    $manifestPath = Join-Path $SharedDistributionRoot 'files.sha256'
    Assert-Contract (
        (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).
            Hash.ToLowerInvariant() -ceq
            '9c12dd6a66ded396436a0308d70ebc7b5293134ab5eb0eb341784497d100c7be'
    ) 'Shared distribution manifest hash does not match the reviewed revision.'

    foreach ($line in Get-Content -LiteralPath $manifestPath -Encoding UTF8) {
        if ($line -notmatch '^(?<hash>[0-9a-f]{64})  \./(?<path>.+)$') {
            throw "Shared distribution manifest line is invalid: '$line'."
        }
        $filePath = Join-Path $SharedDistributionRoot (
            $Matches.path -replace '/', '\')
        Assert-Contract (
            Test-Path -LiteralPath $filePath -PathType Leaf
        ) "Shared distribution file is missing: '$($Matches.path)'."
        Assert-Contract (
            (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).
                Hash.ToLowerInvariant() -ceq $Matches.hash
        ) "Shared distribution file hash changed: '$($Matches.path)'."
    }

    $contractRoot = Join-Path $SharedDistributionRoot 'contract'
    $conformance = & (
        Join-Path $contractRoot `
            'conformance/Invoke-RepositoryAutomationConformance.ps1'
    ) -Root $contractRoot
    Assert-Contract $conformance.IsClean (
        "Shared conformance failed: $($conformance.Errors -join '; ')")

    foreach ($entry in @(
        @{
            Document = $configurationPath
            Schema = 'schemas/repository-config.schema.json'
        },
        @{
            Document = $policyPath
            Schema = 'schemas/module-policy.schema.json'
        }
    )) {
        $errors = @()
        $valid = Test-Json `
            -Json (Get-Content -LiteralPath $entry.Document -Raw -Encoding UTF8) `
            -SchemaFile (Join-Path $contractRoot $entry.Schema) `
            -ErrorAction SilentlyContinue `
            -ErrorVariable errors
        Assert-Contract $valid (
            "Foundry research policy violates shared conformance: $($errors -join '; ')")
    }
}

Test-ResearchPolicyContract `
    -Root $RepositoryRoot `
    -SharedDistributionRoot $DistributionRoot

if ($SelfTest) {
    $fixture = Join-Path (
        [IO.Path]::GetTempPath()
    ) "foundry-research-policy-$([Guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Force -Path $fixture | Out-Null
        foreach ($relativePath in @(
            '.github/ISSUE_TEMPLATE/research.yml',
            '.github/repository-automation/config-research-needed.json',
            '.github/repository-automation/policies/research-needed.json',
            '.github/workflows/repository-automation-research-needed.yml',
            '.pitcrew/runner-profile.json'
        )) {
            $source = Join-Path $RepositoryRoot ($relativePath -replace '/', '\')
            $destination = Join-Path $fixture ($relativePath -replace '/', '\')
            New-Item -ItemType Directory -Force -Path (
                Split-Path -Parent $destination) | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination
        }
        New-Item -ItemType Directory -Force -Path (
            Join-Path $fixture '.github/repository-automation/schemas') |
            Out-Null
        Set-Content -LiteralPath (
            Join-Path $fixture `
                '.github/repository-automation/schemas/local-copy.json') '{}'
        $rejected = $false
        try {
            Test-ResearchPolicyContract -Root $fixture
        } catch {
            $rejected = $true
        }
        Assert-Contract $rejected (
            'A repository-owned shared schema copy was not rejected.')
    } finally {
        if (Test-Path -LiteralPath $fixture) {
            Remove-Item -LiteralPath $fixture -Recurse -Force
        }
    }
}

Write-Host 'Foundry research opt-in policy contract passed.'
