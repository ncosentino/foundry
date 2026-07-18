param(
    [string]$PackageDirectory = 'artifacts/packages',
    [string]$ExpectedVersion = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedPackageDirectory = if ([System.IO.Path]::IsPathRooted($PackageDirectory)) {
    $PackageDirectory
}
else {
    Join-Path $repoRoot $PackageDirectory
}

$expectedPackageIds = @(
    'NexusLabs.Foundry.Copilot',
    'NexusLabs.Foundry.Evaluation',
    'NexusLabs.Foundry.Evaluation.Reporting',
    'NexusLabs.Foundry.Langfuse',
    'NexusLabs.Foundry.MicrosoftAgentFramework',
    'NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers',
    'NexusLabs.Foundry.MicrosoftAgentFramework.DevUI',
    'NexusLabs.Foundry.MicrosoftAgentFramework.Generators',
    'NexusLabs.Foundry.MicrosoftAgentFramework.Testing',
    'NexusLabs.Foundry.MicrosoftAgentFramework.Workflows',
    'NexusLabs.Foundry.Needlr.MicrosoftAgentFramework',
    'NexusLabs.Foundry.Needlr.SemanticKernel',
    'NexusLabs.Foundry.Needlr.SemanticKernel.Generators'
)

$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure {
    param([string]$Message)

    $failures.Add($Message)
}

function Read-Package {
    param([System.IO.FileInfo]$PackageFile)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackageFile.FullName)
    try {
        $nuspecEntry = $archive.Entries |
            Where-Object { $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase) } |
            Select-Object -First 1

        if ($null -eq $nuspecEntry) {
            throw "Package '$($PackageFile.Name)' does not contain a nuspec."
        }

        $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
        try {
            [xml]$nuspec = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        $dependencyIds = @(
            $nuspec.SelectNodes("//*[local-name()='dependency']") |
                ForEach-Object { $_.GetAttribute('id') } |
                Where-Object { $_ }
        )

        return [pscustomobject]@{
            File = $PackageFile
            Id = [string]$nuspec.package.metadata.id
            Version = [string]$nuspec.package.metadata.version
            DependencyIds = $dependencyIds
            Entries = @($archive.Entries | ForEach-Object { $_.FullName })
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-PackageEntry {
    param(
        [hashtable]$PackagesById,
        [string]$PackageId,
        [string]$EntryPath
    )

    if (-not $PackagesById.ContainsKey($PackageId)) {
        return
    }

    if ($PackagesById[$PackageId].Entries -notcontains $EntryPath) {
        Add-Failure "Package '$PackageId' is missing '$EntryPath'."
    }
}

function Assert-PackageEntryAbsent {
    param(
        [hashtable]$PackagesById,
        [string]$PackageId,
        [string]$EntryPath
    )

    if (-not $PackagesById.ContainsKey($PackageId)) {
        return
    }

    if ($PackagesById[$PackageId].Entries -contains $EntryPath) {
        Add-Failure "Package '$PackageId' must not contain '$EntryPath'."
    }
}

function Assert-Dependency {
    param(
        [hashtable]$PackagesById,
        [string]$PackageId,
        [string]$DependencyId
    )

    if (-not $PackagesById.ContainsKey($PackageId)) {
        return
    }

    if ($PackagesById[$PackageId].DependencyIds -notcontains $DependencyId) {
        Add-Failure "Package '$PackageId' is missing dependency '$DependencyId'."
    }
}

if (-not (Test-Path $resolvedPackageDirectory)) {
    throw "Package directory '$resolvedPackageDirectory' does not exist."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$packageFiles = @(
    Get-ChildItem $resolvedPackageDirectory -Filter '*.nupkg' -File |
        Sort-Object Name
)

if ($packageFiles.Count -eq 0) {
    throw "No NuGet packages were found in '$resolvedPackageDirectory'."
}

$packages = @($packageFiles | ForEach-Object { Read-Package $_ })
$packagesById = @{}

foreach ($package in $packages) {
    if ($packagesById.ContainsKey($package.Id)) {
        Add-Failure "Duplicate package ID '$($package.Id)' was produced."
        continue
    }

    $packagesById[$package.Id] = $package
}

foreach ($expectedPackageId in $expectedPackageIds) {
    if (-not $packagesById.ContainsKey($expectedPackageId)) {
        Add-Failure "Expected package '$expectedPackageId' was not produced."
    }
}

foreach ($actualPackageId in $packagesById.Keys) {
    if ($actualPackageId -notin $expectedPackageIds) {
        Add-Failure "Unexpected package '$actualPackageId' was produced."
    }
}

$versions = @($packages | Select-Object -ExpandProperty Version -Unique)
if ($versions.Count -ne 1) {
    Add-Failure "Foundry packages do not share one version: $($versions -join ', ')."
}
elseif ($ExpectedVersion -and $versions[0] -ne $ExpectedVersion) {
    Add-Failure "Package version '$($versions[0])' does not match expected version '$ExpectedVersion'."
}

foreach ($package in $packages) {
    if ($package.Entries -notcontains 'README.md') {
        Add-Failure "Package '$($package.Id)' does not contain README.md."
    }

    $hasRuntimeOrAnalyzer = $package.Entries |
        Where-Object { $_ -like 'lib/*' -or $_ -like 'analyzers/*' } |
        Select-Object -First 1
    if (-not $hasRuntimeOrAnalyzer) {
        Add-Failure "Package '$($package.Id)' contains neither runtime nor analyzer assets."
    }

    foreach ($dependencyId in $package.DependencyIds) {
        if ($dependencyId -match '^NexusLabs\.Needlr\.(AgentFramework|Evaluation|Copilot)') {
            Add-Failure "Package '$($package.Id)' depends on retired package '$dependencyId'."
        }

        $isNeedlrIntegration = $package.Id.StartsWith(
            'NexusLabs.Foundry.Needlr.',
            [System.StringComparison]::Ordinal)
        if (-not $isNeedlrIntegration -and
            $dependencyId -match '^NexusLabs\.Needlr(?:\.|$)') {
            Add-Failure "Neutral package '$($package.Id)' depends on Needlr package '$dependencyId'."
        }
    }
}

Assert-PackageEntry $packagesById `
    'NexusLabs.Foundry.MicrosoftAgentFramework.Generators' `
    'analyzers/dotnet/cs/NexusLabs.Foundry.MicrosoftAgentFramework.Generators.dll'
Assert-PackageEntry $packagesById `
    'NexusLabs.Foundry.MicrosoftAgentFramework.Generators' `
    'build/NexusLabs.Foundry.MicrosoftAgentFramework.Generators.props'
Assert-PackageEntry $packagesById `
    'NexusLabs.Foundry.MicrosoftAgentFramework.Generators' `
    'build/NexusLabs.Foundry.MicrosoftAgentFramework.Generators.targets'
Assert-PackageEntry $packagesById `
    'NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers' `
    'analyzers/dotnet/cs/NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.dll'
Assert-PackageEntryAbsent $packagesById `
    'NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers' `
    'lib/netstandard2.0/NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.dll'
Assert-PackageEntry $packagesById `
    'NexusLabs.Foundry.Needlr.SemanticKernel.Generators' `
    'analyzers/dotnet/cs/NexusLabs.Foundry.Needlr.SemanticKernel.Generators.dll'
Assert-PackageEntry $packagesById `
    'NexusLabs.Foundry.Needlr.SemanticKernel' `
    'build/NexusLabs.Foundry.Needlr.SemanticKernel.targets'
Assert-PackageEntryAbsent $packagesById `
    'NexusLabs.Foundry.MicrosoftAgentFramework' `
    'analyzers/dotnet/cs/NexusLabs.Foundry.MicrosoftAgentFramework.Generators.dll'
Assert-PackageEntryAbsent $packagesById `
    'NexusLabs.Foundry.MicrosoftAgentFramework' `
    'analyzers/dotnet/cs/NexusLabs.Foundry.MicrosoftAgentFramework.Analyzers.dll'
Assert-PackageEntryAbsent $packagesById `
    'NexusLabs.Foundry.Needlr.SemanticKernel' `
    'analyzers/dotnet/cs/NexusLabs.Foundry.Needlr.SemanticKernel.Generators.dll'

Assert-Dependency $packagesById `
    'NexusLabs.Foundry.Needlr.MicrosoftAgentFramework' `
    'NexusLabs.Foundry.MicrosoftAgentFramework'
Assert-Dependency $packagesById `
    'NexusLabs.Foundry.Needlr.MicrosoftAgentFramework' `
    'NexusLabs.Needlr'
Assert-Dependency $packagesById `
    'NexusLabs.Foundry.Needlr.SemanticKernel' `
    'NexusLabs.Needlr'
Assert-Dependency $packagesById `
    'NexusLabs.Foundry.MicrosoftAgentFramework' `
    'OpenTelemetry.Api'

if ($failures.Count -gt 0) {
    Write-Host 'Package validation failed:' -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Validated $($packages.Count) Foundry packages at version $($versions[0])." -ForegroundColor Green
