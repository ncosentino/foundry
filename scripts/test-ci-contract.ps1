param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Contract {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-RepositoryFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $path = Join-Path $Root ($RelativePath -replace '/', [IO.Path]::DirectorySeparatorChar)
    Assert-Contract (Test-Path -LiteralPath $path -PathType Leaf) "Required file '$RelativePath' does not exist."
    return $path
}

function Get-WorkflowFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $workflowRoot = Join-Path $Root '.github' 'workflows'
    Assert-Contract (Test-Path -LiteralPath $workflowRoot -PathType Container) 'Workflow directory does not exist.'
    return @(
        Get-ChildItem -LiteralPath $workflowRoot -File |
            Where-Object Extension -in @('.yml', '.yaml') |
            Sort-Object Name
    )
}

function Invoke-SdkResolver {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string[]]$Files,

        [Parameter(Mandatory = $true)]
        [string[]]$InstalledSdks
    )

    $resolver = Get-RepositoryFile $Root 'scripts/resolve-dotnet-sdk-contract.ps1'
    $json = & $resolver `
        -RepositoryRoot $Root `
        -GlobalJsonFiles $Files `
        -InstalledSdks $InstalledSdks
    return $json | ConvertFrom-Json
}

function Test-CiContract {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $Root = [IO.Path]::GetFullPath((Resolve-Path $Root).Path)
    $actionPath = Get-RepositoryFile $Root '.github/actions/setup-dotnet/action.yml'
    $resolverPath = Get-RepositoryFile $Root 'scripts/resolve-dotnet-sdk-contract.ps1'
    $ciPath = Get-RepositoryFile $Root '.github/workflows/ci.yml'
    $docsPath = Get-RepositoryFile $Root '.github/workflows/docs.yml'
    $releasePath = Get-RepositoryFile $Root '.github/workflows/release.yml'
    $workflowFiles = Get-WorkflowFiles $Root
    $allowedRunnerLabels = @('ubuntu-24.04', 'windows-latest')

    foreach ($workflow in $workflowFiles) {
        $relative = [IO.Path]::GetRelativePath($Root, $workflow.FullName).Replace('\', '/')
        $content = Get-Content -LiteralPath $workflow.FullName -Raw -Encoding UTF8
        foreach ($forbiddenToken in @('self-hosted', 'foundry-ci', 'CI_RUNNER')) {
            Assert-Contract (
                $content -notmatch [regex]::Escape($forbiddenToken)
            ) "Workflow '$relative' contains prohibited runner routing token '$forbiddenToken'."
        }
        Assert-Contract (
            $content.Contains('including windows-latest, are free and') -and
            $content.Contains('unlimited for this public repository')
        ) "Workflow '$relative' must explain that windows-latest is free and unlimited for this public repository."

        $runnerMatches = @(
            [regex]::Matches(
                $content,
                '(?m)^\s+runs-on:\s*(?<label>[^\r\n#]+?)\s*$')
        )
        Assert-Contract ($runnerMatches.Count -gt 0) "Workflow '$relative' has no runner labels."
        foreach ($runnerMatch in $runnerMatches) {
            $label = $runnerMatch.Groups['label'].Value.Trim()
            Assert-Contract (
                $allowedRunnerLabels -contains $label
            ) "Workflow '$relative' uses unsupported runner label '$label'."
        }
    }

    foreach ($legacyPath in @(
        '.pitcrew',
        '.github/runner-images'
    )) {
        $path = Join-Path $Root ($legacyPath -replace '/', [IO.Path]::DirectorySeparatorChar)
        $legacyFiles = @(
            Get-ChildItem -LiteralPath $path -File -Recurse -ErrorAction SilentlyContinue
        )
        Assert-Contract ($legacyFiles.Count -eq 0) "Legacy runner support remains under '$legacyPath'."
    }
    foreach ($legacyWorkflow in @(
        '.github/workflows/runner-image.yml',
        '.github/workflows/autonomous-phase-evaluation.yml'
    )) {
        $path = Join-Path $Root ($legacyWorkflow -replace '/', [IO.Path]::DirectorySeparatorChar)
        Assert-Contract (-not (Test-Path -LiteralPath $path)) "Legacy workflow '$legacyWorkflow' still exists."
    }

    $action = Get-Content -LiteralPath $actionPath -Raw -Encoding UTF8
    Assert-Contract ($action -match 'global-json-files:') 'The setup action must accept exact SDK contract files.'
    Assert-Contract ($action -match 'scripts/resolve-dotnet-sdk-contract\.ps1') 'The setup action must use the validated SDK resolver.'
    Assert-Contract (
        $action -match 'actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9'
    ) 'The setup action must pin actions/setup-dotnet to the reviewed commit.'
    Assert-Contract (
        $action -match 'DOTNET_INSTALL_DIR:\s*\$\{\{\s*runner\.temp\s*\}\}/foundry-dotnet'
    ) 'Missing SDKs must install into one RUNNER_TEMP-backed directory.'
    Assert-Contract ($action -match 'setup-performed') 'The setup action must report whether installation occurred.'
    Assert-Contract ($action -match 'required-versions') 'The setup action must report the exact required SDK set.'

    $resolverText = Get-Content -LiteralPath $resolverPath -Raw -Encoding UTF8
    foreach ($requiredCheck in @(
        "rollForward -eq 'disable'",
        'allowPrerelease -eq $false',
        '^\d+\.\d+\.\d+$',
        'setupRequired'
    )) {
        Assert-Contract ($resolverText.Contains($requiredCheck)) "SDK resolver check '$requiredCheck' is missing."
    }

    $allPresent = Invoke-SdkResolver $Root @(
        'global.json',
        '.github/dotnet/sdk-9/global.json'
    ) @('9.0.316', '10.0.302')
    Assert-Contract ($allPresent.setupRequired -eq $false) 'Setup must be skipped only when every exact SDK is installed.'
    Assert-Contract (
        @($allPresent.requiredVersions) -join ',' -eq '10.0.302,9.0.316'
    ) 'The resolver did not preserve the complete requested SDK set.'
    $oneMissing = Invoke-SdkResolver $Root @(
        'global.json',
        '.github/dotnet/sdk-9/global.json'
    ) @('10.0.302')
    Assert-Contract ($oneMissing.setupRequired -eq $true) 'One missing SDK must require installation of the complete set.'

    $workflows = [ordered]@{
        'ci.yml' = Get-Content -LiteralPath $ciPath -Raw -Encoding UTF8
        'docs.yml' = Get-Content -LiteralPath $docsPath -Raw -Encoding UTF8
        'release.yml' = Get-Content -LiteralPath $releasePath -Raw -Encoding UTF8
    }
    foreach ($entry in $workflows.GetEnumerator()) {
        Assert-Contract ($entry.Value -notmatch 'actions/setup-dotnet@') "Workflow '$($entry.Key)' still calls actions/setup-dotnet directly."
        Assert-Contract ($entry.Value -notmatch '\b(?:9|10)\.0\.x\b') "Workflow '$($entry.Key)' still contains a floating SDK range."
        Assert-Contract ($entry.Value -notmatch 'DOTNET_VERSION') "Workflow '$($entry.Key)' still uses DOTNET_VERSION."
        Assert-Contract (
            $entry.Value.Contains('uses: ./.github/actions/setup-dotnet')
        ) "Workflow '$($entry.Key)' does not use the repository setup action."
    }
    Assert-Contract (
        ([regex]::Matches($workflows['ci.yml'], 'uses: \./\.github/actions/setup-dotnet')).Count -eq 3
    ) 'CI must set up exact SDKs in build-test-pack, aot, and aot-harness.'
    Assert-Contract (
        $workflows['docs.yml'].Contains('.github/dotnet/sdk-9/global.json')
    ) 'Documentation must request both exact SDK contracts.'
    Assert-Contract (
        $workflows['release.yml'].Contains('.github/dotnet/sdk-9/global.json')
    ) 'Release documentation must request both exact SDK contracts.'

    foreach ($jobName in @('build-test-pack:', 'aot:', 'aot-harness:')) {
        Assert-Contract ($workflows['ci.yml'].Contains($jobName)) "Required CI job '$jobName' changed."
    }
    Assert-Contract ($workflows['docs.yml'] -match '(?m)^\s{2}docs:\s*$') "Required check job 'docs' changed."
    Assert-Contract ($workflows['release.yml'] -match 'environment:\s*release') 'Release environment binding changed.'
    Assert-Contract ($workflows['release.yml'] -match 'packages:\s*write') 'Release package permission changed.'
    Assert-Contract ($workflows['release.yml'] -match 'id-token:\s*write') 'Release identity permission changed.'
    Assert-Contract ($workflows['release.yml'].Contains('NuGet/login@v1')) 'NuGet trusted publishing changed.'
}

function Copy-CiFixture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,

        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot
    )

    foreach ($relativePath in @(
        '.github/actions/setup-dotnet/action.yml',
        '.github/dotnet/sdk-9/global.json',
        '.github/workflows/ci.yml',
        '.github/workflows/docs-cloudflare.yml',
        '.github/workflows/docs.yml',
        '.github/workflows/release.yml',
        'global.json',
        'scripts/resolve-dotnet-sdk-contract.ps1'
    )) {
        $source = Join-Path $SourceRoot ($relativePath -replace '/', [IO.Path]::DirectorySeparatorChar)
        $destination = Join-Path $DestinationRoot ($relativePath -replace '/', [IO.Path]::DirectorySeparatorChar)
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }
}

function Assert-MutationRejected {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedMessage,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Mutation
    )

    $fixture = Join-Path ([IO.Path]::GetTempPath()) "foundry-hosted-ci-contract-$([Guid]::NewGuid().ToString('N'))"
    try {
        Copy-CiFixture $RepositoryRoot $fixture
        & $Mutation $fixture
        $message = ''
        try {
            Test-CiContract $fixture
        }
        catch {
            $message = $_.Exception.Message
        }
        Assert-Contract (
            $message.Contains($ExpectedMessage, [StringComparison]::OrdinalIgnoreCase)
        ) "Mutation '$Name' failed for an unexpected reason: '$message'."
    }
    finally {
        if (Test-Path -LiteralPath $fixture) {
            Remove-Item -LiteralPath $fixture -Recurse -Force
        }
    }
}

function Assert-WindowsRunnerAccepted {
    $fixture = Join-Path ([IO.Path]::GetTempPath()) "foundry-windows-runner-$([Guid]::NewGuid().ToString('N'))"
    try {
        Copy-CiFixture $RepositoryRoot $fixture
        $path = Join-Path $fixture '.github\workflows\windows.yml'
        @'
name: Windows Contract Fixture

# Standard GitHub-hosted runners, including windows-latest, are free and
# unlimited for this public repository; larger runners are intentionally excluded.

on:
  workflow_dispatch:

jobs:
  verify:
    runs-on: windows-latest
    steps:
      - shell: pwsh
        run: Write-Host 'windows-latest is an allowed standard public runner.'
'@ | Set-Content -LiteralPath $path -NoNewline
        Test-CiContract $fixture
    }
    finally {
        if (Test-Path -LiteralPath $fixture) {
            Remove-Item -LiteralPath $fixture -Recurse -Force
        }
    }
}

function Assert-ResolverRejected {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $fixture = Join-Path ([IO.Path]::GetTempPath()) "foundry-sdk-contract-$([Guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Path $fixture | Out-Null
        $contractPath = Join-Path $fixture 'global.json'
        [IO.File]::WriteAllText(
            $contractPath,
            $Content,
            [Text.UTF8Encoding]::new($false))
        $rejected = $false
        try {
            $resolver = Get-RepositoryFile $RepositoryRoot 'scripts/resolve-dotnet-sdk-contract.ps1'
            & $resolver `
                -RepositoryRoot $fixture `
                -GlobalJsonFiles @('global.json') `
                -InstalledSdks @('10.0.302') |
                Out-Null
        }
        catch {
            $rejected = $true
        }
        Assert-Contract $rejected "SDK resolver mutation '$Name' was not rejected."
    }
    finally {
        if (Test-Path -LiteralPath $fixture) {
            Remove-Item -LiteralPath $fixture -Recurse -Force
        }
    }
}

Test-CiContract ([IO.Path]::GetFullPath($RepositoryRoot))

if ($SelfTest) {
    Assert-MutationRejected `
        -Name 'self-hosted runner' `
        -ExpectedMessage "prohibited runner routing token 'self-hosted'" `
        -Mutation {
            param($root)
            $path = Join-Path $root '.github\workflows\ci.yml'
            (Get-Content -LiteralPath $path -Raw -Encoding UTF8) `
                -replace 'runs-on: ubuntu-24\.04', 'runs-on: [self-hosted, linux, x64]' |
                Set-Content -LiteralPath $path -NoNewline
        }
    Assert-MutationRejected `
        -Name 'larger runner' `
        -ExpectedMessage "unsupported runner label 'ubuntu-24.04-16core'" `
        -Mutation {
            param($root)
            $path = Join-Path $root '.github\workflows\docs.yml'
            (Get-Content -LiteralPath $path -Raw -Encoding UTF8) `
                -replace 'runs-on: ubuntu-24\.04', 'runs-on: ubuntu-24.04-16core' |
                Set-Content -LiteralPath $path -NoNewline
        }
    Assert-MutationRejected `
        -Name 'repository runner override' `
        -ExpectedMessage "prohibited runner routing token 'CI_RUNNER'" `
        -Mutation {
            param($root)
            $path = Join-Path $root '.github\workflows\release.yml'
            (Get-Content -LiteralPath $path -Raw -Encoding UTF8) `
                -replace 'runs-on: ubuntu-24\.04', 'runs-on: ${{ vars.CI_RUNNER || ''ubuntu-24.04'' }}' |
                Set-Content -LiteralPath $path -NoNewline
        }
    Assert-MutationRejected `
        -Name 'missing public runner notice' `
        -ExpectedMessage 'must explain that windows-latest is free and unlimited' `
        -Mutation {
            param($root)
            $path = Join-Path $root '.github\workflows\docs-cloudflare.yml'
            (Get-Content -LiteralPath $path -Raw -Encoding UTF8) `
                -replace 'including windows-latest, are free and', 'use standard hosted capacity and' |
                Set-Content -LiteralPath $path -NoNewline
        }
    Assert-MutationRejected `
        -Name 'mutable setup-dotnet action' `
        -ExpectedMessage 'must pin actions/setup-dotnet' `
        -Mutation {
            param($root)
            $path = Join-Path $root '.github\actions\setup-dotnet\action.yml'
            (Get-Content -LiteralPath $path -Raw -Encoding UTF8) `
                -replace 'actions/setup-dotnet@[0-9a-f]{40}', 'actions/setup-dotnet@v4' |
                Set-Content -LiteralPath $path -NoNewline
        }
    Assert-MutationRejected `
        -Name 'floating workflow SDK' `
        -ExpectedMessage 'still contains a floating SDK range' `
        -Mutation {
            param($root)
            $path = Join-Path $root '.github\workflows\ci.yml'
            Add-Content -LiteralPath $path "`nenv:`n  DOTNET_VERSION: 10.0.x"
        }

    Assert-WindowsRunnerAccepted

    $missingRejected = $false
    try {
        Invoke-SdkResolver $RepositoryRoot @('missing-global.json') @('10.0.302') | Out-Null
    }
    catch {
        $missingRejected = $true
    }
    Assert-Contract $missingRejected 'A missing SDK contract was not rejected.'
    Assert-ResolverRejected 'malformed JSON' '{'
    Assert-ResolverRejected 'floating SDK range' '{"sdk":{"version":"10.0.x","rollForward":"disable","allowPrerelease":false}}'
    Assert-ResolverRejected 'prerelease SDK drift' '{"sdk":{"version":"10.0.302-preview.1","rollForward":"disable","allowPrerelease":false}}'
    Assert-ResolverRejected 'roll-forward drift' '{"sdk":{"version":"10.0.302","rollForward":"latestPatch","allowPrerelease":false}}'
    Assert-ResolverRejected 'prerelease opt-in' '{"sdk":{"version":"10.0.302","rollForward":"disable","allowPrerelease":true}}'
}

Write-Host 'Foundry GitHub-hosted CI and SDK contract passed.'
