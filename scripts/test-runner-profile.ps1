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

function Test-RunnerProfileContract {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $profilePath = Get-RepositoryFile $Root '.pitcrew/runner-profile.json'
    $actionPath = Get-RepositoryFile $Root '.github/actions/setup-dotnet/action.yml'
    $resolverPath = Get-RepositoryFile $Root 'scripts/resolve-dotnet-sdk-contract.ps1'
    $ciPath = Get-RepositoryFile $Root '.github/workflows/ci.yml'
    $docsPath = Get-RepositoryFile $Root '.github/workflows/docs.yml'
    $releasePath = Get-RepositoryFile $Root '.github/workflows/release.yml'
    $harnessAotPath = Get-RepositoryFile $Root '.github/workflows/harness-g1-aot.yml'
    $runnerImagePath = Get-RepositoryFile $Root '.github/workflows/runner-image.yml'

    $profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    Assert-Contract (
        $profile.'$schema' -eq 'https://raw.githubusercontent.com/ncosentino/pitcrew/87162e6fad6a961b9bc2f026639f2fa7df0795ba/runner-profile.schema.json'
    ) 'The PitCrew schema must be pinned to the reviewed commit.'
    Assert-Contract ($profile.schemaVersion -eq 1) 'The PitCrew profile schema version must be 1.'
    Assert-Contract ($profile.name -eq 'foundry-ci') "The profile name must be 'foundry-ci'."
    Assert-Contract (
        $profile.image -eq 'ghcr.io/ncosentino/foundry-runner@sha256:b03be39181c9cce46a680037262e4e2bf4eaeee1539d669a81543980f5f6d8e8'
    ) 'The profile must pin the anonymously verified public image digest.'
    Assert-Contract (
        $profile.image -match '^ghcr\.io/ncosentino/foundry-runner@sha256:[0-9a-f]{64}$'
    ) 'The profile image must be an immutable GHCR digest without a mutable tag.'
    Assert-Contract ($profile.replicas -eq 2) 'The profile must preserve two configured workers.'
    Assert-Contract ($profile.pullImage -eq $true) 'The profile must pull the approved image.'
    Assert-Contract ($profile.disableDefaultLabels -eq $true) 'The profile must disable default GitHub labels.'
    Assert-Contract (
        @($profile.labels).Count -eq 1 -and $profile.labels[0] -eq 'foundry'
    ) "The profile must declare only the explicit capability label 'foundry'."
    foreach ($forbiddenLabel in @('self-hosted', 'linux', 'x64', 'general-purpose')) {
        Assert-Contract (
            @($profile.labels) -notcontains $forbiddenLabel
        ) "The profile must not expose broad label '$forbiddenLabel'."
    }
    foreach ($command in @(
        'test -x /actions-runner/bin/Runner.Listener',
        "dotnet --list-sdks | grep -F '9.0.316'",
        "dotnet --list-sdks | grep -F '10.0.302'",
        'clang --version',
        'pwsh --version',
        'git --version',
        'gh --version'
    )) {
        Assert-Contract (
            @($profile.verificationCommands) -contains $command
        ) "Profile verification command '$command' is missing."
    }

    $action = Get-Content -LiteralPath $actionPath -Raw
    Assert-Contract ($action -match 'global-json-files:') 'The setup action must accept exact SDK contract files.'
    Assert-Contract ($action -match 'scripts/resolve-dotnet-sdk-contract\.ps1') 'The setup action must use the validated SDK resolver.'
    Assert-Contract (
        $action -match 'actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9'
    ) 'The hosted fallback must pin actions/setup-dotnet to the reviewed commit.'
    Assert-Contract (
        $action -match 'DOTNET_INSTALL_DIR:\s*\$\{\{\s*runner\.temp\s*\}\}/foundry-dotnet'
    ) 'Missing SDKs must install into one RUNNER_TEMP-backed directory.'
    Assert-Contract ($action -match 'setup-performed') 'The setup action must report whether installation occurred.'
    Assert-Contract ($action -match 'required-versions') 'The setup action must report the exact required SDK set.'

    $resolverText = Get-Content -LiteralPath $resolverPath -Raw
    foreach ($requiredCheck in @(
        "rollForward -eq 'disable'",
        'allowPrerelease -eq $false',
        '^\d+\.\d+\.\d+$',
        'setupRequired'
    )) {
        Assert-Contract ($resolverText.Contains($requiredCheck)) "SDK resolver check '$requiredCheck' is missing."
    }
    $allPresent = Invoke-SdkResolver $Root @('global.json', '.github/dotnet/sdk-9/global.json') @('9.0.316', '10.0.302')
    Assert-Contract ($allPresent.setupRequired -eq $false) 'Setup must be skipped only when every exact SDK is installed.'
    Assert-Contract (
        @($allPresent.requiredVersions) -join ',' -eq '10.0.302,9.0.316'
    ) 'The resolver did not preserve the complete requested SDK set.'
    $oneMissing = Invoke-SdkResolver $Root @('global.json', '.github/dotnet/sdk-9/global.json') @('10.0.302')
    Assert-Contract ($oneMissing.setupRequired -eq $true) 'One missing SDK must require installation of the complete set.'

    $workflows = [ordered]@{
        'ci.yml' = Get-Content -LiteralPath $ciPath -Raw
        'docs.yml' = Get-Content -LiteralPath $docsPath -Raw
        'release.yml' = Get-Content -LiteralPath $releasePath -Raw
        'harness-g1-aot.yml' = Get-Content -LiteralPath $harnessAotPath -Raw
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

    $forkRoute = "github.event_name == 'pull_request' && github.event.pull_request.head.repo.fork && 'ubuntu-latest' || vars.CI_RUNNER"
    Assert-Contract ($workflows['ci.yml'].Contains($forkRoute)) 'CI fork routing must remain before CI_RUNNER.'
    Assert-Contract ($workflows['docs.yml'].Contains($forkRoute)) 'Documentation fork routing must remain before CI_RUNNER.'
    foreach ($jobName in @('build-test-pack:', 'aot:', 'aot-harness:')) {
        Assert-Contract ($workflows['ci.yml'].Contains($jobName)) "Required CI job '$jobName' changed."
    }
    Assert-Contract ($workflows['docs.yml'] -match '(?m)^\s{2}docs:\s*$') "Required check job 'docs' changed."
    Assert-Contract (
        $workflows['harness-g1-aot.yml'] -match 'runs-on:\s*ubuntu-latest'
    ) 'Harness G1 AOT must remain explicitly GitHub-hosted.'
    Assert-Contract ($workflows['release.yml'] -match 'environment:\s*release') 'Release environment binding changed.'
    Assert-Contract ($workflows['release.yml'] -match 'packages:\s*write') 'Release package permission changed.'
    Assert-Contract ($workflows['release.yml'] -match 'id-token:\s*write') 'Release identity permission changed.'
    Assert-Contract ($workflows['release.yml'].Contains('NuGet/login@v1')) 'NuGet trusted publishing changed.'

    $runnerImageWorkflow = Get-Content -LiteralPath $runnerImagePath -Raw
    Assert-Contract (
        $runnerImageWorkflow -match 'runs-on:\s*ubuntu-24\.04'
    ) 'Runner image publication must remain GitHub-hosted.'
}

function Copy-ContractFixture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,

        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot
    )

    foreach ($relativePath in @(
        '.pitcrew/runner-profile.json',
        '.github/actions/setup-dotnet/action.yml',
        'global.json',
        '.github/dotnet/sdk-9/global.json',
        'scripts/resolve-dotnet-sdk-contract.ps1',
        '.github/workflows/ci.yml',
        '.github/workflows/docs.yml',
        '.github/workflows/release.yml',
        '.github/workflows/harness-g1-aot.yml',
        '.github/workflows/runner-image.yml'
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
        [scriptblock]$Mutation
    )

    $fixture = Join-Path ([IO.Path]::GetTempPath()) "foundry-profile-contract-$([Guid]::NewGuid().ToString('N'))"
    try {
        Copy-ContractFixture $RepositoryRoot $fixture
        & $Mutation $fixture
        $rejected = $false
        try {
            Test-RunnerProfileContract $fixture
        }
        catch {
            $rejected = $true
        }
        Assert-Contract $rejected "Mutation '$Name' was not rejected."
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

Test-RunnerProfileContract ([IO.Path]::GetFullPath($RepositoryRoot))

if ($SelfTest) {
    Assert-MutationRejected 'mutable profile image' {
        param($root)
        $path = Join-Path $root '.pitcrew/runner-profile.json'
        (Get-Content -LiteralPath $path -Raw) `
            -replace '@sha256:[0-9a-f]{64}', ':latest' |
            Set-Content -LiteralPath $path -NoNewline
    }
    Assert-MutationRejected 'broad profile label' {
        param($root)
        $path = Join-Path $root '.pitcrew/runner-profile.json'
        (Get-Content -LiteralPath $path -Raw) `
            -replace '"foundry"', '"self-hosted"' |
            Set-Content -LiteralPath $path -NoNewline
    }
    Assert-MutationRejected 'mutable setup-dotnet action' {
        param($root)
        $path = Join-Path $root '.github/actions/setup-dotnet/action.yml'
        (Get-Content -LiteralPath $path -Raw) `
            -replace 'actions/setup-dotnet@[0-9a-f]{40}', 'actions/setup-dotnet@v4' |
            Set-Content -LiteralPath $path -NoNewline
    }
    Assert-MutationRejected 'floating workflow SDK' {
        param($root)
        $path = Join-Path $root '.github/workflows/ci.yml'
        Add-Content -LiteralPath $path "`nenv:`n  DOTNET_VERSION: 10.0.x"
    }
    Assert-MutationRejected 'fork routing after CI_RUNNER' {
        param($root)
        $path = Join-Path $root '.github/workflows/docs.yml'
        $content = Get-Content -LiteralPath $path -Raw
        $original = "github.event_name == 'pull_request' && github.event.pull_request.head.repo.fork && 'ubuntu-latest' || vars.CI_RUNNER"
        $mutated = "vars.CI_RUNNER || github.event_name == 'pull_request' && github.event.pull_request.head.repo.fork && 'ubuntu-latest'"
        $content.Replace($original, $mutated) |
            Set-Content -LiteralPath $path -NoNewline
    }
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

Write-Host 'Foundry runner profile and SDK setup contract passed.'
