param(
    [string]$RepositoryRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
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

function Assert-JsonContract {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$SchemaPath
    )

    $errors = @()
    $valid = Test-Json `
        -Json (Get-Content -LiteralPath $Path -Raw -Encoding UTF8) `
        -SchemaFile $SchemaPath `
        -ErrorAction SilentlyContinue `
        -ErrorVariable errors
    Assert-Contract $valid (
        "'$Path' does not conform to '$SchemaPath': $($errors -join '; ')")
}

function Test-ResearchAutomationContract {
    param(
        [Parameter(Mandatory)]
        [string]$Root
    )

    $Root = [IO.Path]::GetFullPath((Resolve-Path $Root).Path)
    $automationRoot = Join-Path $Root '.github\repository-automation'
    $distributionPath = Get-RepositoryFile `
        $Root '.github/repository-automation/runtime-distribution.json'
    $distributionSchemaPath = Get-RepositoryFile `
        $Root '.github/repository-automation/runtime-distribution.schema.json'
    $configurationPath = Get-RepositoryFile `
        $Root '.github/repository-automation/config-research-needed.json'
    $configurationSchemaPath = Get-RepositoryFile `
        $Root '.github/repository-automation/schemas/repository-config.schema.json'
    $policyPath = Get-RepositoryFile `
        $Root '.github/repository-automation/policies/research-needed.json'
    $policySchemaPath = Get-RepositoryFile `
        $Root '.github/repository-automation/schemas/module-policy.schema.json'
    $workflowPath = Get-RepositoryFile `
        $Root '.github/workflows/repository-automation-research-needed.yml'
    $profilePath = Get-RepositoryFile `
        $Root '.pitcrew/runner-profile.json'

    Assert-JsonContract $distributionPath $distributionSchemaPath
    Assert-JsonContract $configurationPath $configurationSchemaPath
    Assert-JsonContract $policyPath $policySchemaPath

    $distribution = Get-Content -LiteralPath $distributionPath -Raw |
        ConvertFrom-Json
    Assert-Contract (
        [string]$distribution.runtime.command -ceq 'repository-automation' -and
        [string]$distribution.runtime.version -ceq '0.8.2' -and
        [string]$distribution.runtime.contract_version -ceq '1'
    ) 'The private repository-automation runtime must remain pinned to 0.8.2 contract 1.'
    Assert-Contract (
        [string]$distribution.installation.github_hosted -ceq 'unavailable' -and
        [string]$distribution.installation.self_hosted -ceq 'preinstalled-only'
    ) 'The runtime must remain unavailable on hosted runners and preinstalled-only on PitCrew.'
    Assert-Contract (
        [string]$distribution.analysis_engine.package_id -ceq '@github/copilot' -and
        [string]$distribution.analysis_engine.version -ceq '1.0.78'
    ) 'The analysis engine must remain the exact reviewed GitHub Copilot CLI package.'
    Assert-Contract (
        -not (Test-Path -LiteralPath (
            Join-Path $automationRoot 'packages') -PathType Container)
    ) 'The private runtime package must not be committed to the public repository.'

    $configuration = Get-Content -LiteralPath $configurationPath -Raw |
        ConvertFrom-Json
    $module = @($configuration.modules)
    Assert-Contract (
        $module.Count -eq 1 -and
        [string]$module[0].id -ceq 'research-needed' -and
        [string]$module[0].version -ceq '1'
    ) 'Configuration must contain only research-needed v1.'
    Assert-Contract (
        [string]$configuration.runtime.minimum_version -ceq '0.8.2'
    ) 'Configuration must require repository-automation 0.8.2.'
    Assert-Contract (
        @($module[0].triggers).Count -eq 1 -and
        [string]$module[0].triggers[0].kind -ceq 'manual'
    ) 'Research automation configuration must be manual-only.'
    Assert-Contract (
        [string]$module[0].settings.trigger_label -ceq 'research-needed' -and
        [string]$module[0].settings.completion_label -ceq 'research-completed' -and
        [bool]$module[0].settings.publish_to_body -eq $false
    ) 'Research lifecycle labels or comment-only publication changed.'
    Assert-Contract (
        @($module[0].settings.allowed_url_prefixes).Count -eq 1 -and
        [string]$module[0].settings.allowed_url_prefixes[0] -ceq 'https://github.com/'
    ) 'The pilot must allow only GitHub HTTPS sources.'

    $policy = Get-Content -LiteralPath $policyPath -Raw |
        ConvertFrom-Json
    Assert-Contract (
        @($policy.operations) -join ',' -ceq
            'issue.labels.update,issue.comment.upsert,issue.body-section.upsert'
    ) 'Research policy operations changed.'
    Assert-Contract (
        [int]$policy.limits.operations -eq 3 -and
        [int]$policy.limits.created_issues -eq 0 -and
        [int]$policy.limits.relationships -eq 0 -and
        [int]$policy.limits.dispatches -eq 0
    ) 'Research policy mutation limits changed.'
    Assert-Contract (
        @($policy.labels.allowed) -join ',' -ceq
            'research-needed,research-completed' -and
        @($policy.markers.allowed) -join ',' -ceq
            'repository-automation.research-needed'
    ) 'Research policy labels or marker changed.'

    $workflow = Get-Content -LiteralPath $workflowPath -Raw -Encoding UTF8
    Assert-Contract (
        $workflow -match '(?m)^# live-llm-workflow:\s*true\s*$'
    ) 'Research workflow must retain the live-LLM marker.'
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
        ) "Research workflow contains forbidden automation or transport '$forbidden'."
    }
    Assert-Contract (
        ([regex]::Matches(
            $workflow,
            '(?m)^  (resolve|analyze|apply):\s*$')).Count -eq 3
    ) 'Research workflow must contain resolve, analyze, and apply jobs.'
    Assert-Contract (
        ([regex]::Matches(
            $workflow,
            '(?m)^    runs-on:\s*foundry-ci\s*$')).Count -eq 3
    ) 'Every research workflow job must run on foundry-ci.'
    Assert-Contract (
        $workflow -match "vars\.REPOSITORY_AUTOMATION_RESEARCH_NEEDED_ENABLED == 'manual'"
    ) 'Research workflow must remain disabled unless its activation variable is manual.'
    Assert-Contract (
        $workflow -match 'IsNullOrWhiteSpace\(\$env:AUTHORIZATION_REASON\)'
    ) 'Research workflow must reject whitespace-only authorization reasons.'
    Assert-Contract (
        $workflow -match '/mnt/pitcrew-data/repository-automation' -and
        $workflow -match 'expected repository-automation 0\.8\.2'
    ) 'Research workflow must select and verify the private PitCrew runtime.'
    Assert-Contract (
        $workflow -match 'COPILOT_GITHUB_TOKEN:\s*\$\{\{\s*github\.token\s*\}\}' -and
        $workflow -notmatch 'secrets\.COPILOT_GITHUB_TOKEN'
    ) 'Research workflow must use the ephemeral job token for Copilot requests.'
    Assert-Contract (
        $workflow -match '(?m)^\s{6}copilot-requests:\s*write\s*$' -and
        $workflow -match '(?m)^\s{6}issues:\s*write\s*$'
    ) 'Research workflow must retain separate Copilot and issue-write jobs.'

    $profile = Get-Content -LiteralPath $profilePath -Raw |
        ConvertFrom-Json
    Assert-Contract (
        [string]$profile.'$schema' -ceq
            'https://raw.githubusercontent.com/ncosentino/pitcrew/0613544e58e20af67ff414dc3a216b3d1ee842e7/runner-profile.schema.json'
    ) 'PitCrew profile must use the reviewed v0.8.2 schema commit.'
    Assert-Contract (
        @($profile.readOnlyVolumes).Count -eq 1 -and
        [string]$profile.readOnlyVolumes[0].name -ceq 'repository-automation' -and
        [string]$profile.readOnlyVolumes[0].source -ceq 'foundry-repository-automation'
    ) 'PitCrew profile must mount the operator-owned runtime volume.'
    $verification = @($profile.verificationCommands) -join "`n"
    Assert-Contract (
        $verification.Contains(
            'a960047f3034b77ffc6e3d793bada073469c3e04a7ef86c1ea667e66dfe10421',
            [StringComparison]::Ordinal) -and
        $verification.Contains(
            'a4b6a68551a0b5906ef309c33538481cf2de67b1d2d8f781b7d9dd7c84390b41',
            [StringComparison]::Ordinal) -and
        $verification.Contains(
            '"version":"0.8.2"',
            [StringComparison]::Ordinal)
    ) 'PitCrew profile must verify the exact package, file manifest, and runtime version.'

    foreach ($path in @(
        $automationRoot,
        (Join-Path $Root 'scripts\repository-automation')
    )) {
        foreach ($file in Get-ChildItem -LiteralPath $path -Recurse -File |
            Where-Object {
                $_.Extension -in @('.json', '.ps1') -and
                $_.Name -cne 'Test-FoundryResearchAutomation.ps1'
            }) {
            $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
            Assert-Contract (
                $content -notmatch '(?i)[A-Z]:\\Users\\|/home/[^/\s]+'
            ) "Public automation file '$($file.FullName)' contains private or machine-specific provenance."
        }
    }

    $invalidRejectedResult = [ordered]@{
        '$schema' = 'urn:repository-automation:apply-result:1'
        schema_version = '1'
        request_id = 'test-request'
        idempotency_key = "sha256:$('a' * 64)"
        proposal_sha256 = "sha256:$('b' * 64)"
        status = 'rejected'
        retryable = $false
        completed_at = '2026-08-11T00:00:00Z'
        current_state_sha256 = "sha256:$('c' * 64)"
        operation_results = @(
            [ordered]@{
                operation_id = 'operation-1'
                kind = 'issue.comment.upsert'
                status = 'applied'
                resource = $null
                failure = $null
            }
        )
        failure = [ordered]@{
            code = 'proposal-rejected'
            message = 'The proposal was rejected.'
        }
        external_output = $null
    } | ConvertTo-Json -Depth 12 -Compress
    $invalidRejectedAccepted = Test-Json `
        -Json $invalidRejectedResult `
        -SchemaFile (Get-RepositoryFile `
            $Root '.github/repository-automation/schemas/apply-result.schema.json') `
        -ErrorAction SilentlyContinue
    Assert-Contract (
        -not $invalidRejectedAccepted
    ) 'Rejected apply results must not report an applied operation.'

    Push-Location $Root
    try {
        $requestJson =
            & (Join-Path $Root (
                'scripts\repository-automation\New-RepositoryAutomationWorkRequest.ps1')) `
                -ModuleId 'research-needed' `
                -ModuleVersion '1' `
                -ConfigurationPath '.github/repository-automation/config-research-needed.json' `
                -Trigger 'manual' `
                -EventName 'workflow_dispatch' `
                -RepositoryId 'R_foundry' `
                -Repository 'ncosentino/foundry' `
                -Revision ('a' * 40) `
                -DeliveryId 'test:1' `
                -RequestId 'test-request' `
                -WorkItemNumber 13 `
                -WorkItemProviderId 'I_foundry_13'
        $request = $requestJson | ConvertFrom-Json
        Assert-Contract (
            [string]$request.source.trigger -ceq 'manual' -and
            [int]$request.source.work_item.number -eq 13
        ) 'Generated work request did not preserve the manual issue identity.'

        $hostedRejected = $false
        try {
            & (Join-Path $Root (
                'scripts\repository-automation\Initialize-RepositoryAutomationRuntime.ps1')) `
                -RunnerEnvironment github-hosted |
                Out-Null
        } catch {
            $hostedRejected = $_.Exception.Message.Contains(
                'does not distribute the private repository-automation runtime',
                [StringComparison]::Ordinal)
        }
        Assert-Contract $hostedRejected (
            'GitHub-hosted runtime initialization must fail before package installation.')
    } finally {
        Pop-Location
    }
}

function Copy-ContractFixture {
    param(
        [Parameter(Mandatory)]
        [string]$SourceRoot,

        [Parameter(Mandatory)]
        [string]$DestinationRoot
    )

    foreach ($relativeDirectory in @(
        '.github/repository-automation',
        'scripts/repository-automation'
    )) {
        $source = Join-Path $SourceRoot ($relativeDirectory -replace '/', '\')
        $destination = Join-Path $DestinationRoot (
            $relativeDirectory -replace '/', '\')
        New-Item -ItemType Directory -Force -Path (
            Split-Path -Parent $destination) | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Recurse
    }
    foreach ($relativePath in @(
        '.github/workflows/repository-automation-research-needed.yml',
        '.pitcrew/runner-profile.json'
    )) {
        $source = Join-Path $SourceRoot ($relativePath -replace '/', '\')
        $destination = Join-Path $DestinationRoot ($relativePath -replace '/', '\')
        New-Item -ItemType Directory -Force -Path (
            Split-Path -Parent $destination) | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }
}

function Assert-MutationRejected {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [scriptblock]$Mutation
    )

    $fixture = Join-Path (
        [IO.Path]::GetTempPath()
    ) "foundry-research-$([Guid]::NewGuid().ToString('N'))"
    try {
        Copy-ContractFixture `
            -SourceRoot $RepositoryRoot `
            -DestinationRoot $fixture
        & $Mutation $fixture
        $rejected = $false
        try {
            Test-ResearchAutomationContract $fixture
        } catch {
            $rejected = $true
        }
        Assert-Contract $rejected "Mutation '$Name' was not rejected."
    } finally {
        if (Test-Path -LiteralPath $fixture) {
            Remove-Item -LiteralPath $fixture -Recurse -Force
        }
    }
}

Test-ResearchAutomationContract $RepositoryRoot

if ($SelfTest) {
    Assert-MutationRejected 'event trigger' {
        param($root)
        $path = Join-Path $root (
            '.github\repository-automation\config-research-needed.json')
        $configuration = Get-Content -LiteralPath $path -Raw |
            ConvertFrom-Json
        $configuration.modules[0].triggers = @(
            [PSCustomObject]@{
                kind = 'event'
                adapter = 'github'
                event = 'issues'
                types = @('labeled')
            }
        )
        $configuration | ConvertTo-Json -Depth 12 |
            Set-Content -LiteralPath $path
    }
    Assert-MutationRejected 'hosted runtime package' {
        param($root)
        $path = Join-Path $root (
            '.github\repository-automation\runtime-distribution.json')
        $distribution = Get-Content -LiteralPath $path -Raw |
            ConvertFrom-Json
        $distribution.installation.github_hosted = 'bundled-exact-package'
        $distribution | ConvertTo-Json -Depth 12 |
            Set-Content -LiteralPath $path
    }
    Assert-MutationRejected 'runtime volume source' {
        param($root)
        $path = Join-Path $root '.pitcrew\runner-profile.json'
        $profile = Get-Content -LiteralPath $path -Raw |
            ConvertFrom-Json
        $profile.readOnlyVolumes[0].source = 'other-volume'
        $profile | ConvertTo-Json -Depth 12 |
            Set-Content -LiteralPath $path
    }
}

Write-Host 'Foundry manual research automation contract passed.'
