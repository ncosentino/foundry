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

function Assert-ExactSdkContract {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedVersion
    )

    $contract = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    Assert-Contract ($contract.sdk.version -eq $ExpectedVersion) "'$Path' must pin SDK '$ExpectedVersion'."
    Assert-Contract ($contract.sdk.rollForward -eq 'disable') "'$Path' must disable SDK roll-forward."
    Assert-Contract ($contract.sdk.allowPrerelease -eq $false) "'$Path' must reject prerelease SDKs."
    Assert-Contract ($contract.sdk.version -match '^\d+\.\d+\.\d+$') "'$Path' contains a floating or malformed SDK version."
}

function Test-RunnerImageContract {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $rootGlobal = Get-RepositoryFile $Root 'global.json'
    $docsGlobal = Get-RepositoryFile $Root '.github/dotnet/sdk-9/global.json'
    $dockerfilePath = Get-RepositoryFile $Root '.github/runner-images/foundry-ci/Dockerfile'
    $workflowPath = Get-RepositoryFile $Root '.github/workflows/runner-image.yml'
    $ciPath = Get-RepositoryFile $Root '.github/workflows/ci.yml'
    $docsPath = Get-RepositoryFile $Root '.github/workflows/docs.yml'

    Assert-ExactSdkContract $rootGlobal '10.0.302'
    Assert-ExactSdkContract $docsGlobal '9.0.316'

    $dockerfile = Get-Content -LiteralPath $dockerfilePath -Raw
    $fromLines = @(
        $dockerfile -split "\r?\n" |
            Where-Object { $_ -match '^\s*FROM\s+' }
    )
    Assert-Contract ($fromLines.Count -gt 0) 'The runner Dockerfile must contain a FROM instruction.'
    foreach ($fromLine in $fromLines) {
        Assert-Contract (
            $fromLine -match '@sha256:[0-9a-f]{64}(?:\s|$)'
        ) "Every production FROM image must be pinned by SHA-256 digest: '$fromLine'."
    }

    Assert-Contract (
        $dockerfile -match 'myoung34/github-runner:ubuntu-noble@sha256:881a6b81df476e9b9ce7f9451b0efbacba7496ac4483613e24849a2c9b1ffd60'
    ) 'The Dockerfile must use the reviewed PitCrew-compatible runner base digest.'
    Assert-Contract ($dockerfile -notmatch '(?mi)^\s*(COPY|ADD)\s+') 'The runner image must not copy repository source into an image layer.'
    Assert-Contract (
        $dockerfile -notmatch '(?mi)^\s*(ARG|ENV)\s+[A-Z0-9_]*(TOKEN|SECRET|PASSWORD|CREDENTIAL|API_?KEY|PRIVATE_?KEY)[A-Z0-9_]*'
    ) 'The runner Dockerfile must not declare credential-bearing ARG or ENV inputs.'
    Assert-Contract ($dockerfile -match 'dotnet-sdk-10\.0\.302-linux-x64\.tar\.gz') 'The image must install SDK 10.0.302.'
    Assert-Contract ($dockerfile -match 'dotnet-sdk-9\.0\.316-linux-x64\.tar\.gz') 'The image must install SDK 9.0.316.'
    Assert-Contract (
        $dockerfile -match '10069bec8783596484a610332f090d562802a41b9b40e3327a5a5688b572e10c296ae300f940d40461f23c157ed1b0843c2f8e6b3f20d8d8d9d83432d8143bac'
    ) 'The SDK 10.0.302 archive hash is missing or incorrect.'
    Assert-Contract (
        $dockerfile -match '5a8558afd648c14a835e00ae08fa556083f50e3ada164d3e73293fcd4850b0519a27c11f2dae95a9bbe4af432be33bf14451ef11ba69527e34f9cf3077a1c2b5'
    ) 'The SDK 9.0.316 archive hash is missing or incorrect.'
    foreach ($package in @('clang', 'file', 'zlib1g-dev')) {
        Assert-Contract ($dockerfile -match "(?m)\b$([regex]::Escape($package))\b") "NativeAOT prerequisite '$package' is missing."
    }
    foreach ($verification in @(
        '/actions-runner/bin/Runner.Listener',
        'dotnet --list-sdks',
        'clang --version',
        'pwsh --version',
        'git --version',
        'gh --version'
    )) {
        Assert-Contract ($dockerfile.Contains($verification)) "Image verification '$verification' is missing."
    }
    Assert-Contract ($dockerfile -match 'org\.opencontainers\.image\.source') 'The image must carry an OCI source label.'
    Assert-Contract ($dockerfile -match 'org\.opencontainers\.image\.revision') 'The image must carry an OCI revision label.'

    $workflow = Get-Content -LiteralPath $workflowPath -Raw
    $usesLines = @(
        $workflow -split "\r?\n" |
            Where-Object { $_ -match '^\s*uses:\s*' }
    )
    Assert-Contract ($usesLines.Count -gt 0) 'The runner image workflow must use reviewed actions.'
    foreach ($usesLine in $usesLines) {
        Assert-Contract (
            $usesLine -match '@[0-9a-f]{40}(?:\s+#.*)?$'
        ) "Every runner image action must be pinned to an immutable commit SHA: '$usesLine'."
    }
    Assert-Contract ($workflow -match '(?m)^\s*pull_request:\s*$') 'Runner image validation must run for pull requests.'
    Assert-Contract ($workflow -match '(?m)^\s*push:\s*$') 'Runner image publication must run for trusted main pushes.'
    Assert-Contract ($workflow -notmatch '(?m)^\s*pull_request_target:\s*$') 'Runner image validation must never use pull_request_target.'
    Assert-Contract ($workflow -match 'runs-on:\s*ubuntu-24\.04') 'Runner image jobs must use GitHub-hosted ubuntu-24.04.'
    Assert-Contract ($workflow -notmatch 'self-hosted') 'Runner image jobs must not use self-hosted workers.'
    Assert-Contract ($workflow -match 'context:\s*\.github/runner-images/foundry-ci') 'Docker builds must use the narrow runner-image context.'
    Assert-Contract ($workflow -match 'ghcr\.io/\$\{\{\s*github\.repository_owner\s*\}\}/foundry-runner:sha-\$\{\{\s*github\.sha\s*\}\}') 'Trusted publication must use the immutable source-SHA tag.'
    Assert-Contract ($workflow -match 'packages:\s*write') 'The trusted publication job must request packages: write.'
    Assert-Contract ($workflow -match 'provenance:\s*mode=max') 'Runner image publication must emit maximum provenance.'
    Assert-Contract ($workflow -match 'sbom:\s*true') 'Runner image publication must emit an SBOM.'
    Assert-Contract ($workflow -match "github\.event_name == 'push'") 'Publication must be restricted to trusted push events.'
    foreach ($path in @(
        'global.json',
        '.github/dotnet/sdk-9/global.json',
        '.github/runner-images/foundry-ci/Dockerfile',
        '.github/workflows/runner-image.yml',
        'scripts/test-runner-image.ps1'
    )) {
        Assert-Contract ($workflow.Contains($path)) "Runner image path filters must include '$path'."
    }

    $ci = Get-Content -LiteralPath $ciPath -Raw
    $docs = Get-Content -LiteralPath $docsPath -Raw
    $forkRoute = "github.event_name == 'pull_request' && github.event.pull_request.head.repo.fork && 'ubuntu-latest' || vars.CI_RUNNER"
    Assert-Contract ($ci.Contains($forkRoute)) 'CI fork routing must be evaluated before CI_RUNNER.'
    Assert-Contract ($docs.Contains($forkRoute)) 'Documentation fork routing must be evaluated before CI_RUNNER.'
    foreach ($jobName in @('build-test-pack:', 'aot:', 'aot-harness:')) {
        Assert-Contract ($ci.Contains($jobName)) "Required CI job '$jobName' was renamed or removed."
    }
    Assert-Contract ($docs -match '(?m)^\s{2}docs:\s*$') "Required check job 'docs' was renamed or removed."
}

function Copy-ContractFixture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,

        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot
    )

    foreach ($relativePath in @(
        'global.json',
        '.github/dotnet/sdk-9/global.json',
        '.github/runner-images/foundry-ci/Dockerfile',
        '.github/workflows/runner-image.yml',
        '.github/workflows/ci.yml',
        '.github/workflows/docs.yml'
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

    $fixture = Join-Path ([IO.Path]::GetTempPath()) "foundry-runner-contract-$([Guid]::NewGuid().ToString('N'))"
    try {
        Copy-ContractFixture $RepositoryRoot $fixture
        & $Mutation $fixture
        $rejected = $false
        try {
            Test-RunnerImageContract $fixture
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

Test-RunnerImageContract ([IO.Path]::GetFullPath($RepositoryRoot))

if ($SelfTest) {
    Assert-MutationRejected 'mutable base image' {
        param($root)
        $path = Join-Path $root '.github/runner-images/foundry-ci/Dockerfile'
        (Get-Content -LiteralPath $path -Raw) `
            -replace '@sha256:[0-9a-f]{64}', '' |
            Set-Content -LiteralPath $path -NoNewline
    }
    Assert-MutationRejected 'floating primary SDK' {
        param($root)
        $path = Join-Path $root 'global.json'
        (Get-Content -LiteralPath $path -Raw) `
            -replace '10\.0\.302', '10.0.x' |
            Set-Content -LiteralPath $path -NoNewline
    }
    Assert-MutationRejected 'broad self-hosted image build' {
        param($root)
        $path = Join-Path $root '.github/workflows/runner-image.yml'
        (Get-Content -LiteralPath $path -Raw) `
            -replace 'runs-on: ubuntu-24\.04', 'runs-on: [self-hosted, linux, x64]' |
            Set-Content -LiteralPath $path -NoNewline
    }
    Assert-MutationRejected 'mutable workflow action tag' {
        param($root)
        $path = Join-Path $root '.github/workflows/runner-image.yml'
        (Get-Content -LiteralPath $path -Raw) `
            -replace '@[0-9a-f]{40}', '@v4' |
            Set-Content -LiteralPath $path -NoNewline
    }
    Assert-MutationRejected 'source-copying Docker instruction' {
        param($root)
        $path = Join-Path $root '.github/runner-images/foundry-ci/Dockerfile'
        Add-Content -LiteralPath $path "`nCOPY . /src"
    }
    Assert-MutationRejected 'credential-bearing Docker argument' {
        param($root)
        $path = Join-Path $root '.github/runner-images/foundry-ci/Dockerfile'
        Add-Content -LiteralPath $path "`nARG GITHUB_TOKEN"
    }
    Assert-MutationRejected 'fork routing after CI_RUNNER' {
        param($root)
        $path = Join-Path $root '.github/workflows/ci.yml'
        $content = Get-Content -LiteralPath $path -Raw
        $original = "github.event_name == 'pull_request' && github.event.pull_request.head.repo.fork && 'ubuntu-latest' || vars.CI_RUNNER"
        $mutated = "vars.CI_RUNNER || github.event_name == 'pull_request' && github.event.pull_request.head.repo.fork && 'ubuntu-latest'"
        $content.Replace($original, $mutated) |
            Set-Content -LiteralPath $path -NoNewline
    }
}

Write-Host 'Foundry runner image contract passed.'
