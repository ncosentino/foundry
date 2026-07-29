param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [Parameter(Mandatory = $true)]
    [string[]]$GlobalJsonFiles,

    [string[]]$InstalledSdks
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = if ([IO.Path]::IsPathRooted($RepositoryRoot)) {
    [IO.Path]::GetFullPath($RepositoryRoot)
}
else {
    [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $RepositoryRoot))
}
$requiredVersions = [Collections.Generic.List[string]]::new()

foreach ($relativePath in $GlobalJsonFiles) {
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        continue
    }

    if ([IO.Path]::IsPathRooted($relativePath)) {
        throw "SDK contract path '$relativePath' must be relative to the repository root."
    }

    $path = [IO.Path]::GetFullPath(
        (Join-Path $root ($relativePath.Trim() -replace '/', [IO.Path]::DirectorySeparatorChar)))
    $relative = [IO.Path]::GetRelativePath($root, $path)
    if ([IO.Path]::IsPathRooted($relative) -or
        $relative -eq '..' -or
        $relative.StartsWith("..$([IO.Path]::DirectorySeparatorChar)", [StringComparison]::Ordinal)) {
        throw "SDK contract path '$relativePath' escapes the repository root."
    }

    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "SDK contract '$relativePath' does not exist."
    }

    try {
        $contract = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    }
    catch {
        throw "SDK contract '$relativePath' is not valid JSON: $($_.Exception.Message)"
    }

    if ($null -eq $contract.sdk) {
        throw "SDK contract '$relativePath' does not contain an sdk object."
    }

    $version = [string]$contract.sdk.version
    if ($version -notmatch '^\d+\.\d+\.\d+$') {
        throw "SDK contract '$relativePath' must contain one exact stable SDK version."
    }

    $rollForwardValid = $contract.sdk.rollForward -eq 'disable'
    if (-not $rollForwardValid) {
        throw "SDK contract '$relativePath' must set rollForward to disable."
    }

    $allowPrereleaseValid = $contract.sdk.allowPrerelease -eq $false
    if (-not $allowPrereleaseValid) {
        throw "SDK contract '$relativePath' must set allowPrerelease to false."
    }

    if (-not $requiredVersions.Contains($version)) {
        $requiredVersions.Add($version)
    }
}

if ($requiredVersions.Count -eq 0) {
    throw 'At least one exact SDK contract is required.'
}

$installedVersions = if ($PSBoundParameters.ContainsKey('InstalledSdks')) {
    @($InstalledSdks | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}
else {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) {
        @()
    }
    else {
        @(
            & $dotnet.Source --list-sdks |
                ForEach-Object {
                    if ($_ -match '^(\d+\.\d+\.\d+)\s') {
                        $Matches[1]
                    }
                } |
                Where-Object { $_ } |
                Select-Object -Unique
        )
    }
}

$missingVersions = @(
    $requiredVersions |
        Where-Object { $installedVersions -notcontains $_ }
)
$setupRequired = $missingVersions.Count -gt 0

[pscustomobject]@{
    schemaVersion = '1.0'
    requiredVersions = @($requiredVersions)
    installedVersions = @($installedVersions)
    missingVersions = @($missingVersions)
    setupRequired = $setupRequired
} | ConvertTo-Json -Depth 4 -Compress
