param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ModelId,

    [Parameter(Mandatory = $true)]
    [bool]$ConfirmCopilotEnterpriseBilling
)

$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
}

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}

$inputDirectory = Join-Path $outputRoot 'inputs'
$statusPath = Join-Path $outputRoot 'preflight-status.json'
New-Item -ItemType Directory -Path $inputDirectory -Force | Out-Null

function Get-CanonicalSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    $text = [System.IO.File]::ReadAllText($Path).ReplaceLineEndings("`n")
    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($text)
    return [Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($bytes)
    ).ToLowerInvariant()
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )

    $json = $Value | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText(
        $Path,
        $json.ReplaceLineEndings("`n") + "`n",
        [System.Text.UTF8Encoding]::new($false))
}

function Stop-Preflight {
    param([Parameter(Mandatory = $true)][string]$Message)

    Write-JsonFile `
        -Path $statusPath `
        -Value ([ordered]@{
            schemaVersion = '1.0'
            state = 'Failed'
            reason = $Message
            advisoryOnly = $true
            provider = 'github-copilot'
            runnerEnvironment = $env:HARNESS_EVAL_RUNNER_ENVIRONMENT
            inferenceAttempted = $false
            fullPairedExecutionTask = 'T119'
        })
    throw $Message
}

Write-JsonFile `
    -Path $statusPath `
    -Value ([ordered]@{
        schemaVersion = '1.0'
        state = 'Started'
        reason = 'Hosted evaluation preflight started.'
        advisoryOnly = $true
        provider = 'github-copilot'
        runnerEnvironment = $env:HARNESS_EVAL_RUNNER_ENVIRONMENT
        inferenceAttempted = $false
        fullPairedExecutionTask = 'T119'
    })

$caseSetRoot = Join-Path $repoRoot 'artifacts/eval/case-sets/harness-001/v1.0'
$manifestPath = Join-Path $caseSetRoot 'manifest.json'
$analysisPlanPath = Join-Path $caseSetRoot 'analysis-plan.md'
$pricingPath = Join-Path $caseSetRoot 'pricing/github-copilot.v1.json'

foreach ($requiredPath in @($manifestPath, $analysisPlanPath, $pricingPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        Stop-Preflight -Message "Required hosted evaluation input '$requiredPath' does not exist."
    }
}

Copy-Item -LiteralPath $caseSetRoot -Destination (Join-Path $inputDirectory 'case-set') -Recurse

$caps = [ordered]@{
    plannedRuns = [int]$env:HARNESS_EVAL_PLANNED_RUNS
    maximumAttempts = [int]$env:HARNESS_EVAL_MAX_ATTEMPTS
    maximumRequestsPerAttempt = [int]$env:HARNESS_EVAL_MAX_REQUESTS_PER_ATTEMPT
    maximumReservedRequests = [int]$env:HARNESS_EVAL_MAX_RESERVED_REQUESTS
    workflowTimeoutMinutes = [int]$env:HARNESS_EVAL_WORKFLOW_TIMEOUT_MINUTES
    schedulingDeadlineMinutes = [int]$env:HARNESS_EVAL_SCHEDULING_DEADLINE_MINUTES
    maximumAttemptSeconds = [int]$env:HARNESS_EVAL_MAX_ATTEMPT_SECONDS
    maximumOutputTokens = [int]$env:HARNESS_EVAL_MAX_OUTPUT_TOKENS
    maximumConcurrency = [int]$env:HARNESS_EVAL_MAX_CONCURRENCY
    estimatedCostCapUsd = [decimal]$env:HARNESS_EVAL_COST_CAP_USD
}

if ($caps.maximumAttempts * $caps.maximumRequestsPerAttempt -ne $caps.maximumReservedRequests) {
    Stop-Preflight `
        -Message 'The reserved worst-case request budget does not match attempts multiplied by requests per attempt.'
}

$immutableInputs = [ordered]@{
    schemaVersion = '1.0'
    caseSetId = 'harness-001'
    caseSetVersion = 'v1.0'
    workflowRunId = $env:GITHUB_RUN_ID
    workflowRunAttempt = $env:GITHUB_RUN_ATTEMPT
    gitSha = $env:GITHUB_SHA
    modelId = $ModelId
    provider = 'github-copilot'
    authentication = 'GITHUB_TOKEN with copilot-requests:write'
    billingProduct = 'GitHub Copilot Enterprise'
    runnerEnvironment = $env:HARNESS_EVAL_RUNNER_ENVIRONMENT
    runnerLabels = $env:HARNESS_EVAL_RUNNER_LABELS
    confirmCopilotEnterpriseBilling = $ConfirmCopilotEnterpriseBilling
    manifestSha256 = Get-CanonicalSha256 -Path $manifestPath
    analysisPlanSha256 = Get-CanonicalSha256 -Path $analysisPlanPath
    pricingTableSha256 = Get-CanonicalSha256 -Path $pricingPath
    caps = $caps
}
Write-JsonFile -Path (Join-Path $inputDirectory 'immutable-inputs.json') -Value $immutableInputs

$pricing = Get-Content $pricingPath -Raw | ConvertFrom-Json
$modelPricing = $pricing.models | Where-Object modelId -eq $ModelId | Select-Object -First 1
if ($null -eq $modelPricing) {
    Stop-Preflight -Message "Model '$ModelId' is not present in the frozen pricing table."
}

if ([decimal]$modelPricing.reservedWorstCaseUsdPerRequest -ne
    [decimal]$env:HARNESS_EVAL_ESTIMATED_USD_PER_REQUEST) {
    Stop-Preflight -Message 'The workflow request-cost reservation does not match the frozen pricing table.'
}

if ([int]$modelPricing.maximumOutputTokensPerRequest -ne
    [int]$env:HARNESS_EVAL_MAX_OUTPUT_TOKENS) {
    Stop-Preflight -Message 'The workflow output-token cap does not match the frozen pricing table.'
}

if ([int]$modelPricing.minimumRequestIntervalMilliseconds -ne
    [int]$env:HARNESS_EVAL_MIN_REQUEST_INTERVAL_MS) {
    Stop-Preflight -Message 'The workflow provider pacing does not match the frozen pricing table.'
}

$state = 'CopilotBillingNotConfirmed'
$reason = 'GitHub Copilot Enterprise billing was not explicitly affirmed; no inference request was made.'
$failureMessage = $null

if ($ConfirmCopilotEnterpriseBilling) {
    $token = $env:GITHUB_TOKEN
    if (-not [string]::Equals(
        $env:HARNESS_EVAL_RUNNER_ENVIRONMENT,
        'self-hosted',
        [System.StringComparison]::OrdinalIgnoreCase)) {
        $state = 'Failed'
        $reason = 'Harness evaluation is restricted to a PitCrew self-hosted runner.'
        $failureMessage = $reason
    }
    elseif ([string]::IsNullOrWhiteSpace($token)) {
        $state = 'Failed'
        $reason = 'GITHUB_TOKEN was unavailable for GitHub Copilot authentication.'
        $failureMessage = $reason
    }
    else {
        $state = 'Ready'
        $reason = 'PitCrew self-hosted execution, GitHub Copilot Enterprise billing, token presence, pricing, and protocol caps were validated; no preflight inference request was made.'
    }
}

Write-JsonFile `
    -Path $statusPath `
    -Value ([ordered]@{
        schemaVersion = '1.0'
        state = $state
        reason = $reason
        advisoryOnly = $true
        provider = 'github-copilot'
        runnerEnvironment = $env:HARNESS_EVAL_RUNNER_ENVIRONMENT
        inferenceAttempted = $false
        fullPairedExecutionTask = 'T119'
    })

$checksumPath = Join-Path $outputRoot 'checksums.sha256'
$checksumLines = Get-ChildItem $outputRoot -Recurse -File |
    Where-Object { $_.FullName -ne $checksumPath } |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath = [System.IO.Path]::GetRelativePath($outputRoot, $_.FullName).Replace('\', '/')
        $hash = Get-CanonicalSha256 -Path $_.FullName
        "$hash  $relativePath"
    }
[System.IO.File]::WriteAllLines(
    $checksumPath,
    $checksumLines,
    [System.Text.UTF8Encoding]::new($false))

if ($env:GITHUB_OUTPUT) {
    "preflight-status=$state" | Add-Content $env:GITHUB_OUTPUT
    "artifact-directory=$outputRoot" | Add-Content $env:GITHUB_OUTPUT
}

Write-Host "Harness evaluation preflight state: $state"
Write-Host $reason

if ($failureMessage) {
    throw $failureMessage
}
