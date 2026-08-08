#Requires -Version 7.0
<#
.SYNOPSIS
    Inventory Foundry-declared validation and build surfaces.

.PARAMETER ProjectRoot
    Repository root to inspect.

.PARAMETER Json
    Emit JSON instead of a PSCustomObject.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path $ProjectRoot).Path

function Get-RelativePath {
    param([string]$Path)
    return [IO.Path]::GetRelativePath($ProjectRoot, $Path).Replace('\', '/')
}

function Get-ProjectFiles {
    $excludedDirectories = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @(
        '.git',
        '.next',
        '.nuxt',
        '.output',
        '__pycache__',
        'artifacts',
        'bin',
        'build',
        'coverage',
        'dist',
        'node_modules',
        'obj',
        'site',
        'target'
    )) {
        [void]$excludedDirectories.Add($name)
    }

    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($ProjectRoot)
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($item in (Get-ChildItem $directory -Force)) {
            if ($item.PSIsContainer) {
                if (
                    -not $excludedDirectories.Contains($item.Name) -and
                    -not ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)
                ) {
                    $pending.Push($item.FullName)
                }
            }
            else {
                $item
            }
        }
    }
}

$projectFiles = @(Get-ProjectFiles)
$dotnetSolutions = @(
    $projectFiles |
        Where-Object Extension -in @('.sln', '.slnx') |
        Sort-Object FullName |
        ForEach-Object { Get-RelativePath $_.FullName }
)
$dotnetProjects = @(
    $projectFiles |
        Where-Object Extension -CEQ '.csproj' |
        Sort-Object FullName |
        ForEach-Object { Get-RelativePath $_.FullName }
)
$msbuildContracts = @(
    $projectFiles |
        Where-Object Name -in @(
            'Directory.Build.props',
            'Directory.Build.targets',
            'Directory.Packages.props'
        ) |
        Sort-Object FullName |
        ForEach-Object { Get-RelativePath $_.FullName }
)
$sdkContracts = @(
    $projectFiles |
        Where-Object Name -CEQ 'global.json' |
        Sort-Object FullName |
        ForEach-Object { Get-RelativePath $_.FullName }
)

$workflowRoot = Join-Path $ProjectRoot '.github' 'workflows'
$workflows = @(
    if (Test-Path $workflowRoot -PathType Container) {
        Get-ChildItem $workflowRoot -File |
            Where-Object Extension -in @('.yml', '.yaml') |
            Sort-Object Name |
            ForEach-Object {
                $content = Get-Content $_.FullName -Raw -Encoding UTF8
                $name = [regex]::Match($content, '(?m)^name:\s*(.+?)\s*$')
                [PSCustomObject]@{
                    path = Get-RelativePath $_.FullName
                    name = if ($name.Success) {
                        $name.Groups[1].Value.Trim("'`"")
                    }
                    else {
                        $_.BaseName
                    }
                }
            }
    }
)

$contractScripts = @(
    $projectFiles |
        Where-Object {
            $_.Extension -CEQ '.ps1' -and
            $_.FullName.StartsWith(
                (Join-Path $ProjectRoot 'scripts'),
                [StringComparison]::OrdinalIgnoreCase)
        } |
        Sort-Object FullName |
        ForEach-Object {
            $content = Get-Content $_.FullName -Raw -Encoding UTF8
            [PSCustomObject]@{
                path = Get-RelativePath $_.FullName
                selfTest = $content -match '\[switch\]\s*\$SelfTest'
            }
        }
)

$documentation = [PSCustomObject]@{
    config = @(
        $projectFiles |
            Where-Object Name -in @('mkdocs.yml', 'mkdocs.yaml') |
            Sort-Object FullName |
            ForEach-Object { Get-RelativePath $_.FullName }
    )
    requirements = @(
        $projectFiles |
            Where-Object Name -like 'requirements*.txt' |
            Sort-Object FullName |
            ForEach-Object { Get-RelativePath $_.FullName }
    )
    hooks = @(
        $projectFiles |
            Where-Object {
                $_.Extension -CEQ '.py' -and
                (Get-RelativePath $_.FullName) -like 'docs/hooks/*'
            } |
            Sort-Object FullName |
            ForEach-Object { Get-RelativePath $_.FullName }
    )
    tests = @(
        $projectFiles |
            Where-Object {
                (Get-RelativePath $_.FullName) -like 'scripts/tests/test_*.py'
            } |
            Sort-Object FullName |
            ForEach-Object { Get-RelativePath $_.FullName }
    )
}

$runnerProfiles = @(
    $projectFiles |
        Where-Object {
            (Get-RelativePath $_.FullName) -like '.pitcrew/*.json'
        } |
        Sort-Object FullName |
        ForEach-Object { Get-RelativePath $_.FullName }
)

$inventory = [PSCustomObject]@{
    projectRoot = $ProjectRoot
    dotnetSolutions = $dotnetSolutions
    dotnetProjects = $dotnetProjects
    msbuildContracts = $msbuildContracts
    sdkContracts = $sdkContracts
    workflows = $workflows
    contractScripts = $contractScripts
    documentation = $documentation
    runnerProfiles = $runnerProfiles
}

if ($Json) {
    $inventory | ConvertTo-Json -Depth 12
}
else {
    $inventory
}
