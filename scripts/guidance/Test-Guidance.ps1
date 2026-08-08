#Requires -Version 7.0
<#
.SYNOPSIS
    Validate Foundry's agent-guidance ownership and loading contract.

.PARAMETER RepositoryRoot
    Repository root to validate.

.PARAMETER SelfTest
    Run controlled negative fixtures after validating the repository.
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepositoryRoot = [IO.Path]::GetFullPath((Resolve-Path $RepositoryRoot).Path)
. (Join-Path $PSScriptRoot 'InstructionGlob.Functions.ps1')

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

function Get-RepositoryPath {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [string]$RelativePath,

        [switch]$Directory
    )

    $path = Join-Path $Root (
        $RelativePath -replace '/', [IO.Path]::DirectorySeparatorChar)
    if ($Directory) {
        Assert-Contract (
            Test-Path -LiteralPath $path -PathType Container
        ) "Required directory '$RelativePath' does not exist."
    }
    else {
        Assert-Contract (
            Test-Path -LiteralPath $path -PathType Leaf
        ) "Required file '$RelativePath' does not exist."
    }
    return [IO.Path]::GetFullPath($path)
}

function Get-TextMetric {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $content = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    return [PSCustomObject]@{
        lines = @(Get-Content -LiteralPath $Path -Encoding UTF8).Count
        bytes = [Text.Encoding]::UTF8.GetByteCount($content)
        content = $content
    }
}

function Get-Frontmatter {
    param(
        [Parameter(Mandatory)]
        [string]$Content,

        [Parameter(Mandatory)]
        [string]$DisplayPath
    )

    $match = [regex]::Match(
        $Content,
        '\A---\r?\n(?<frontmatter>.*?)\r?\n---(?:\r?\n|\z)',
        [Text.RegularExpressions.RegexOptions]::Singleline)
    Assert-Contract $match.Success "File '$DisplayPath' has no valid frontmatter."

    $values = [ordered]@{}
    foreach ($line in ($match.Groups['frontmatter'].Value -split '\r?\n')) {
        $field = [regex]::Match($line, '^\s*([A-Za-z0-9_-]+)\s*:\s*(.*?)\s*$')
        if ($field.Success) {
            $values[$field.Groups[1].Value] = (
                $field.Groups[2].Value.Trim().Trim('"', "'"))
        }
    }

    return [PSCustomObject]@{
        values = $values
        body = $Content.Substring($match.Length)
    }
}

function Get-InstructionRecords {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [object]$Contract
    )

    $instructionRoot = Get-RepositoryPath `
        -Root $Root `
        -RelativePath $Contract.instructions.root `
        -Directory
    $records = @(
        Get-ChildItem $instructionRoot -Recurse -Filter '*.instructions.md' -File |
            Sort-Object FullName |
            ForEach-Object {
                $relative = [IO.Path]::GetRelativePath(
                    $Root,
                    $_.FullName
                ).Replace('\', '/')
                $metric = Get-TextMetric $_.FullName
                $frontmatter = Get-Frontmatter `
                    -Content $metric.content `
                    -DisplayPath $relative
                Assert-Contract (
                    $frontmatter.values.Contains('applyTo')
                ) "Instruction '$relative' has no applyTo value."
                $applyTo = [string]$frontmatter.values['applyTo']
                Assert-Contract (
                    -not [string]::IsNullOrWhiteSpace($applyTo)
                ) "Instruction '$relative' has an empty applyTo value."
                try {
                    [void](Split-InstructionGlobPatterns -ApplyTo $applyTo)
                    foreach ($pattern in Split-InstructionGlobPatterns -ApplyTo $applyTo) {
                        [void](Expand-InstructionGlobPattern -Pattern $pattern)
                    }
                }
                catch {
                    throw "Instruction '$relative' has invalid applyTo '$applyTo': $($_.Exception.Message)"
                }

                $threshold = $Contract.instructions.individualReviewThreshold
                if (
                    $metric.lines -gt $threshold.lines -or
                    $metric.bytes -gt $threshold.bytes
                ) {
                    Assert-Contract (
                        $frontmatter.values.Contains('reviewThresholdReason') -and
                        -not [string]::IsNullOrWhiteSpace(
                            [string]$frontmatter.values['reviewThresholdReason'])
                    ) "Instruction '$relative' exceeds the individual review threshold without reviewThresholdReason."
                }
                Assert-Contract (
                    $frontmatter.body -notmatch '(?i)\.instructions\.md'
                ) "Instruction '$relative' refers to another instruction file."

                [PSCustomObject]@{
                    path = $relative
                    applyTo = $applyTo
                    lines = $metric.lines
                    bytes = $metric.bytes
                }
            }
    )
    Assert-Contract ($records.Count -gt 0) 'No instruction files were found.'
    return $records
}

function Get-MkDocsNavigation {
    param(
        [Parameter(Mandatory)]
        [string]$MapPath
    )

    $content = Get-Content -LiteralPath $MapPath -Raw -Encoding UTF8
    return @(
        [regex]::Matches($content, '(?m):\s+([^\s#]+\.md)\s*$') |
            ForEach-Object {
                $_.Groups[1].Value.Replace('\', '/')
            } |
            Sort-Object -CaseSensitive -Unique
    )
}

function Test-DocumentationLinks {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [System.IO.FileInfo[]]$Files
    )

    $rootPrefix = $Root.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

    foreach ($file in $Files) {
        $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        $relative = [IO.Path]::GetRelativePath(
            $Root,
            $file.FullName
        ).Replace('\', '/')
        $links = [regex]::Matches(
            $content,
            '!?\[[^\]]*\]\((?<target><[^>]+>|[^)\s]+)(?:\s+["''][^"'']*["''])?\)')
        foreach ($link in $links) {
            $target = $link.Groups['target'].Value.Trim('<', '>')
            if (
                [string]::IsNullOrWhiteSpace($target) -or
                $target.StartsWith('#', [StringComparison]::Ordinal) -or
                $target.StartsWith('/', [StringComparison]::Ordinal) -or
                $target -match '^[A-Za-z][A-Za-z0-9+.-]*:'
            ) {
                continue
            }

            $pathPart = ($target -split '[#?]', 2)[0]
            if ([string]::IsNullOrWhiteSpace($pathPart)) {
                continue
            }
            $decoded = [Uri]::UnescapeDataString($pathPart)
            $resolved = [IO.Path]::GetFullPath(
                (Join-Path $file.DirectoryName (
                    $decoded -replace '/', [IO.Path]::DirectorySeparatorChar)))
            Assert-Contract (
                $resolved.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -or
                $resolved.Equals($Root, [StringComparison]::OrdinalIgnoreCase)
            ) "Documentation link '$target' in '$relative' escapes the repository."
            Assert-Contract (
                Test-Path -LiteralPath $resolved
            ) "Documentation link '$target' in '$relative' does not resolve."
        }
    }
}

function Get-AdrRecords {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [object]$Contract,

        [Parameter(Mandatory)]
        [string[]]$Navigation
    )

    $adrRoot = Get-RepositoryPath `
        -Root $Root `
        -RelativePath $Contract.adrs.root `
        -Directory
    $allowedStatuses = @(
        'Proposed',
        'Accepted',
        'Rejected',
        'Superseded',
        'Deprecated'
    )
    $records = @(
        Get-ChildItem $adrRoot -File -Filter 'adr-*.md' |
            Sort-Object Name |
            ForEach-Object {
                $match = [regex]::Match(
                    $_.Name,
                    '^adr-(\d{4})-[a-z0-9]+(?:-[a-z0-9]+)*\.md$')
                Assert-Contract $match.Success "ADR filename '$($_.Name)' does not match adr-NNNN-title-slug.md."
                $metric = Get-TextMetric $_.FullName
                $frontmatter = Get-Frontmatter `
                    -Content $metric.content `
                    -DisplayPath "docs/adr/$($_.Name)"
                foreach ($field in @(
                    'title',
                    'status',
                    'date',
                    'authors',
                    'tags',
                    'supersedes',
                    'superseded_by'
                )) {
                    Assert-Contract (
                        $frontmatter.values.Contains($field)
                    ) "ADR '$($_.Name)' is missing frontmatter field '$field'."
                }
                $id = $match.Groups[1].Value
                Assert-Contract (
                    [string]$frontmatter.values['title'] -match "^ADR-${id}:"
                ) "ADR '$($_.Name)' title does not begin with ADR-$id."
                Assert-Contract (
                    $allowedStatuses -contains [string]$frontmatter.values['status']
                ) "ADR '$($_.Name)' has unsupported status '$($frontmatter.values['status'])'."
                Assert-Contract (
                    $Navigation -contains "adr/$($_.Name)"
                ) "ADR '$($_.Name)' is missing from mkdocs.yml navigation."

                [PSCustomObject]@{
                    id = $id
                    name = $_.Name
                    status = [string]$frontmatter.values['status']
                    supersedes = [string]$frontmatter.values['supersedes']
                    supersededBy = [string]$frontmatter.values['superseded_by']
                }
            }
    )

    $duplicates = @($records | Group-Object id | Where-Object Count -gt 1)
    Assert-Contract ($duplicates.Count -eq 0) 'Duplicate ADR identifiers were found.'

    $byName = @{}
    foreach ($record in $records) {
        $byName[$record.name] = $record
    }
    foreach ($record in $records) {
        if ($record.status -eq 'Superseded') {
            Assert-Contract (
                -not [string]::IsNullOrWhiteSpace($record.supersededBy)
            ) "ADR '$($record.name)' has status Superseded without superseded_by."
        }
        if (-not [string]::IsNullOrWhiteSpace($record.supersededBy)) {
            Assert-Contract (
                $byName.ContainsKey($record.supersededBy)
            ) "ADR '$($record.name)' references missing superseding ADR '$($record.supersededBy)'."
            Assert-Contract (
                $record.status -eq 'Superseded'
            ) "ADR '$($record.name)' has superseded_by but status is '$($record.status)'."
            Assert-Contract (
                $byName[$record.supersededBy].supersedes -eq $record.name
            ) "ADR '$($record.name)' and '$($record.supersededBy)' do not have reciprocal supersession metadata."
        }
        if (-not [string]::IsNullOrWhiteSpace($record.supersedes)) {
            Assert-Contract (
                $byName.ContainsKey($record.supersedes)
            ) "ADR '$($record.name)' references missing superseded ADR '$($record.supersedes)'."
            Assert-Contract (
                $byName[$record.supersedes].supersededBy -eq $record.name
            ) "ADR '$($record.name)' and '$($record.supersedes)' do not have reciprocal supersession metadata."
        }
    }
    return $records
}

function Test-PublicGuidanceContent {
    param(
        [Parameter(Mandatory)]
        [string]$Root
    )

    $paths = [Collections.Generic.List[string]]::new()
    foreach ($relative in @(
        'AGENTS.md',
        'CLAUDE.md',
        'README.md',
        'CHANGELOG.md',
        '.github/copilot-instructions.md'
    )) {
        $path = Join-Path $Root ($relative -replace '/', '\')
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $paths.Add($path)
        }
    }
    foreach ($directory in @(
        '.github/agents',
        '.github/instructions',
        '.github/skills',
        'docs'
    )) {
        $path = Join-Path $Root ($directory -replace '/', '\')
        if (Test-Path -LiteralPath $path -PathType Container) {
            Get-ChildItem $path -Recurse -File -Filter '*.md' |
                ForEach-Object { $paths.Add($_.FullName) }
        }
    }

    $absoluteUserPath = '(?i)([A-Z]:\\Users\\[^\\\r\n]+|/Users/[^/\s]+|/home/[^/\s]+)'
    foreach ($path in $paths) {
        $content = Get-Content -LiteralPath $path -Raw -Encoding UTF8
        $relative = [IO.Path]::GetRelativePath($Root, $path).Replace('\', '/')
        Assert-Contract (
            $content -notmatch $absoluteUserPath
        ) "Public guidance '$relative' contains a machine-specific user path."
    }

    $agentRoot = Join-Path $Root '.github' 'agents'
    if (Test-Path -LiteralPath $agentRoot -PathType Container) {
        foreach ($agent in Get-ChildItem $agentRoot -File -Filter '*.md') {
            $content = Get-Content -LiteralPath $agent.FullName -Raw -Encoding UTF8
            Assert-Contract (
                $content -notmatch '(?m)^### Package Versions'
            ) "Agent '$($agent.Name)' contains a maintained package-version table."
            Assert-Contract (
                $content -notmatch 'This repository \(Needlr\)'
            ) "Agent '$($agent.Name)' identifies this repository as Needlr."
            Assert-Contract (
                $content -notmatch 'github\.com/dotnet/ai-samples'
            ) "Agent '$($agent.Name)' treats the archived dotnet/ai-samples repository as current guidance."
        }
    }
}

function Test-GuidanceContract {
    param(
        [Parameter(Mandatory)]
        [string]$Root
    )

    $Root = [IO.Path]::GetFullPath((Resolve-Path $Root).Path)
    $contractPath = Get-RepositoryPath `
        -Root $Root `
        -RelativePath '.github/foundry-guidance.json'
    $schemaPath = Get-RepositoryPath `
        -Root $Root `
        -RelativePath '.github/foundry-guidance.schema.json'
    $contractJson = Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8
    $contract = $contractJson |
        ConvertFrom-Json
    $null = Get-Content -LiteralPath $schemaPath -Raw -Encoding UTF8 |
        ConvertFrom-Json
    $schemaValid = Test-Json `
        -Json $contractJson `
        -SchemaFile $schemaPath `
        -ErrorAction SilentlyContinue
    Assert-Contract $schemaValid 'Guidance contract does not conform to its JSON schema.'

    Assert-Contract ($contract.schemaVersion -eq 1) 'Guidance schemaVersion must be 1.'
    Assert-Contract (
        $contract.'$schema' -eq './foundry-guidance.schema.json'
    ) 'Guidance contract must reference ./foundry-guidance.schema.json.'

    $agentsPath = Get-RepositoryPath `
        -Root $Root `
        -RelativePath $contract.agents.path
    $agentsMetric = Get-TextMetric $agentsPath
    Assert-Contract (
        $agentsMetric.lines -le $contract.agents.maxLines
    ) "AGENTS.md exceeds $($contract.agents.maxLines) lines."
    Assert-Contract (
        $agentsMetric.bytes -le $contract.agents.maxBytes
    ) "AGENTS.md exceeds $($contract.agents.maxBytes) UTF-8 bytes."

    $claudePath = Get-RepositoryPath `
        -Root $Root `
        -RelativePath $contract.agents.redirects.claude
    $copilotPath = Get-RepositoryPath `
        -Root $Root `
        -RelativePath $contract.agents.redirects.copilot
    Assert-Contract (
        (Get-Content -LiteralPath $claudePath -Raw -Encoding UTF8).Trim() -ceq
            '@AGENTS.md'
    ) 'CLAUDE.md must contain only @AGENTS.md.'
    Assert-Contract (
        (Get-Content -LiteralPath $copilotPath -Raw -Encoding UTF8).Trim() -ceq
            'Follow [AGENTS.md](../AGENTS.md).'
    ) '.github/copilot-instructions.md must be the minimal AGENTS.md pointer.'

    $instructions = @(Get-InstructionRecords -Root $Root -Contract $contract)
    $contextRecords = @(
        foreach ($representativePath in $contract.instructions.representativePaths) {
            $fullPath = Join-Path $Root (
                [string]$representativePath -replace '/', '\')
            Assert-Contract (
                Test-Path -LiteralPath $fullPath -PathType Leaf
            ) "Representative path '$representativePath' does not exist."
            $matches = @(
                $instructions |
                    Where-Object {
                        Test-InstructionGlobMatch `
                            -ApplyTo $_.applyTo `
                            -RelativePath $representativePath
                    }
            )
            $lines = if ($matches.Count -eq 0) {
                0
            }
            else {
                [int]($matches | Measure-Object -Property lines -Sum).Sum
            }
            $bytes = if ($matches.Count -eq 0) {
                0
            }
            else {
                [int]($matches | Measure-Object -Property bytes -Sum).Sum
            }
            Assert-Contract (
                $lines -le $contract.instructions.matchedContext.maxLines -and
                $bytes -le $contract.instructions.matchedContext.maxBytes
            ) "Representative path '$representativePath' exceeds the hard matched-context ceiling."
            Assert-Contract (
                $lines -le $contract.instructions.matchedContext.targetLines -and
                $bytes -le $contract.instructions.matchedContext.targetBytes
            ) "Representative path '$representativePath' exceeds the target matched-context budget."
            [PSCustomObject]@{
                path = [string]$representativePath
                instructions = @(
                    $matches | ForEach-Object { $_.path }
                )
                lines = $lines
                bytes = $bytes
            }
        }
    )

    $docsRoot = Get-RepositoryPath `
        -Root $Root `
        -RelativePath $contract.docs.root `
        -Directory
    $mapPath = Get-RepositoryPath `
        -Root $Root `
        -RelativePath $contract.docs.mapPath
    $navigation = @(Get-MkDocsNavigation -MapPath $mapPath)
    $docs = @(
        Get-ChildItem $docsRoot -Recurse -File -Filter '*.md' |
            Sort-Object FullName
    )
    foreach ($doc in $docs) {
        $relative = [IO.Path]::GetRelativePath(
            $docsRoot,
            $doc.FullName
        ).Replace('\', '/')
        Assert-Contract (
            $navigation -contains $relative
        ) "Documentation page '$relative' is missing from mkdocs.yml navigation."
    }
    foreach ($entry in $navigation) {
        Assert-Contract (
            Test-Path -LiteralPath (
                Join-Path $docsRoot ($entry -replace '/', '\')) -PathType Leaf
        ) "mkdocs.yml navigation target '$entry' does not exist."
    }
    Test-DocumentationLinks -Root $Root -Files $docs

    $adrs = @(Get-AdrRecords `
        -Root $Root `
        -Contract $contract `
        -Navigation $navigation)

    foreach ($reviewPath in @(
        $contract.review.skillPath,
        $contract.review.instructionResolver,
        $contract.review.validationInventory,
        $contract.review.structuralValidator
    )) {
        [void](Get-RepositoryPath -Root $Root -RelativePath $reviewPath)
    }
    $reviewSkillPath = Get-RepositoryPath `
        -Root $Root `
        -RelativePath $contract.review.skillPath
    $reviewMetric = Get-TextMetric $reviewSkillPath
    Assert-Contract (
        $reviewMetric.lines -le $contract.review.maxLines
    ) "Review skill exceeds $($contract.review.maxLines) lines."
    Assert-Contract (
        $reviewMetric.bytes -le $contract.review.maxBytes
    ) "Review skill exceeds $($contract.review.maxBytes) UTF-8 bytes."

    $ciPath = Get-RepositoryPath `
        -Root $Root `
        -RelativePath '.github/workflows/ci.yml'
    $ci = Get-Content -LiteralPath $ciPath -Raw -Encoding UTF8
    Assert-Contract (
        $ci -match '(?m)^  guidance:\r?\n'
    ) "CI is missing the guidance prerequisite job."
    Assert-Contract (
        $ci -match '(?m)^\s+run:\s+scripts/guidance/Test-Guidance\.ps1 -SelfTest\s*$'
    ) "CI guidance job does not run the structural self-test."
    foreach ($job in @('build-test-pack', 'aot', 'aot-harness')) {
        Assert-Contract (
            $ci -match "(?m)^  $([regex]::Escape($job)):\r?\n    needs: guidance\s*$"
        ) "CI job '$job' must depend on guidance."
    }

    Test-PublicGuidanceContent -Root $Root

    $maxContext = $contextRecords |
        Sort-Object bytes, lines -Descending |
        Select-Object -First 1
    return [PSCustomObject]@{
        agentsLines = $agentsMetric.lines
        agentsBytes = $agentsMetric.bytes
        instructionCount = $instructions.Count
        instructionLines = [int]((
            $instructions | Measure-Object lines -Sum).Sum ?? 0)
        instructionBytes = [int]((
            $instructions | Measure-Object bytes -Sum).Sum ?? 0)
        maxContextPath = $maxContext.path
        maxContextLines = $maxContext.lines
        maxContextBytes = $maxContext.bytes
        documentationPages = $docs.Count
        adrCount = $adrs.Count
        reviewSkillLines = $reviewMetric.lines
        reviewSkillBytes = $reviewMetric.bytes
    }
}

function Copy-GuidanceFixture {
    param(
        [Parameter(Mandatory)]
        [string]$SourceRoot,

        [Parameter(Mandatory)]
        [string]$DestinationRoot
    )

    foreach ($relativePath in @(
        'AGENTS.md',
        'CLAUDE.md',
        'README.md',
        'CHANGELOG.md',
        'mkdocs.yml',
        '.github/copilot-instructions.md',
        '.github/foundry-guidance.json',
        '.github/foundry-guidance.schema.json'
    )) {
        $source = Join-Path $SourceRoot ($relativePath -replace '/', '\')
        $destination = Join-Path $DestinationRoot ($relativePath -replace '/', '\')
        New-Item -ItemType Directory -Path (
            Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }

    foreach ($relativeDirectory in @(
        '.github/agents',
        '.github/instructions',
        '.github/skills',
        'docs',
        'scripts/guidance'
    )) {
        $source = Join-Path $SourceRoot ($relativeDirectory -replace '/', '\')
        $destination = Join-Path $DestinationRoot ($relativeDirectory -replace '/', '\')
        New-Item -ItemType Directory -Path (
            Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Recurse
    }

    $contract = Get-Content `
        -LiteralPath (Join-Path $SourceRoot '.github\foundry-guidance.json') `
        -Raw |
        ConvertFrom-Json
    foreach ($relativePath in $contract.instructions.representativePaths) {
        $source = Join-Path $SourceRoot ([string]$relativePath -replace '/', '\')
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            continue
        }
        $destination = Join-Path $DestinationRoot (
            [string]$relativePath -replace '/', '\')
        New-Item -ItemType Directory -Path (
            Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
}

function Assert-MutationRejected {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$ExpectedMessage,

        [Parameter(Mandatory)]
        [scriptblock]$Mutation
    )

    $fixture = Join-Path (
        [IO.Path]::GetTempPath()
    ) "fg-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
    try {
        Copy-GuidanceFixture `
            -SourceRoot $RepositoryRoot `
            -DestinationRoot $fixture
        & $Mutation $fixture
        $message = ''
        try {
            Test-GuidanceContract $fixture | Out-Null
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

$result = Test-GuidanceContract $RepositoryRoot

if ($SelfTest) {
    Assert-MutationRejected `
        -Name 'oversized root guidance' `
        -ExpectedMessage 'AGENTS.md exceeds' `
        -Mutation {
            param($root)
            $path = Join-Path $root 'AGENTS.md'
            Add-Content -LiteralPath $path (
                "`n" + (1..80 | ForEach-Object { "extra guidance line $_" }) -join "`n")
        }
    Assert-MutationRejected `
        -Name 'invalid Claude redirect' `
        -ExpectedMessage 'CLAUDE.md must contain only' `
        -Mutation {
            param($root)
            Set-Content -LiteralPath (Join-Path $root 'CLAUDE.md') 'duplicated guidance'
        }
    Assert-MutationRejected `
        -Name 'oversized review skill' `
        -ExpectedMessage 'Review skill exceeds' `
        -Mutation {
            param($root)
            $path = Join-Path $root '.github\skills\review-changes\SKILL.md'
            $extra = 1..80 |
                ForEach-Object { "extra review line $_" }
            [IO.File]::AppendAllText(
                $path,
                "`n" + ($extra -join "`n"),
                [Text.UTF8Encoding]::new($false))
        }
    Assert-MutationRejected `
        -Name 'guidance schema drift' `
        -ExpectedMessage 'does not conform to its JSON schema' `
        -Mutation {
            param($root)
            $path = Join-Path $root '.github\foundry-guidance.json'
            $contract = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            $contract.agents.maxLines = 61
            [IO.File]::WriteAllText(
                $path,
                ($contract | ConvertTo-Json -Depth 12),
                [Text.UTF8Encoding]::new($false))
        }
    Assert-MutationRejected `
        -Name 'invalid instruction glob' `
        -ExpectedMessage 'invalid applyTo' `
        -Mutation {
            param($root)
            $path = Join-Path $root '.github\instructions\csharp.instructions.md'
            (Get-Content -LiteralPath $path -Raw) `
                -replace 'src/\*\*/\*\.cs', 'src/{broken' |
                Set-Content -LiteralPath $path -NoNewline
        }
    Assert-MutationRejected `
        -Name 'unmapped documentation' `
        -ExpectedMessage 'missing from mkdocs.yml navigation' `
        -Mutation {
            param($root)
            Set-Content -LiteralPath (
                Join-Path $root 'docs\unmapped.md') '# Unmapped'
        }
    Assert-MutationRejected `
        -Name 'missing ADR status' `
        -ExpectedMessage "missing frontmatter field 'status'" `
        -Mutation {
            param($root)
            $path = Join-Path $root 'docs\adr\adr-0012-docs-first-agent-guidance.md'
            (Get-Content -LiteralPath $path -Raw) `
                -replace '(?m)^status:.*\r?\n', '' |
                Set-Content -LiteralPath $path -NoNewline
        }
    Assert-MutationRejected `
        -Name 'superseded ADR without successor' `
        -ExpectedMessage 'status Superseded without superseded_by' `
        -Mutation {
            param($root)
            $path = Join-Path $root 'docs\adr\adr-0007-experimental-hybrid-context-compaction.md'
            (Get-Content -LiteralPath $path -Raw) `
                -replace '(?m)^superseded_by:.*$', 'superseded_by: ""' |
                Set-Content -LiteralPath $path -NoNewline
        }
    Assert-MutationRejected `
        -Name 'excessive aggregate context' `
        -ExpectedMessage 'target matched-context budget' `
        -Mutation {
            param($root)
            foreach ($index in 1..4) {
                $path = Join-Path $root ".github\instructions\extra-$index.instructions.md"
                $body = @(
                    '---',
                    'applyTo: "src/**/*.cs"',
                    '---',
                    '',
                    "# Extra $index"
                )
                $body += 1..75 | ForEach-Object { "- Independent rule $index.$_" }
                Set-Content -LiteralPath $path ($body -join "`n")
            }
        }
    Assert-MutationRejected `
        -Name 'stale agent package inventory' `
        -ExpectedMessage 'maintained package-version table' `
        -Mutation {
            param($root)
            Add-Content -LiteralPath (
                Join-Path $root '.github\agents\meai.agent.md') `
                "`n### Package Versions`n"
        }
    Assert-MutationRejected `
        -Name 'machine-specific public path' `
        -ExpectedMessage 'machine-specific user path' `
        -Mutation {
            param($root)
            Add-Content -LiteralPath (
                Join-Path $root 'docs\agent-guidance.md') `
                "`nC:\Users\Example\private.txt`n"
        }
    Assert-MutationRejected `
        -Name 'NativeAOT bypasses guidance prerequisite' `
        -ExpectedMessage "CI job 'aot' must depend on guidance" `
        -Mutation {
            param($root)
            $path = Join-Path $root '.github\workflows\ci.yml'
            (Get-Content -LiteralPath $path -Raw) `
                -replace '(?m)^(  aot:\r?\n)    needs: guidance\r?\n', '$1' |
                Set-Content -LiteralPath $path -NoNewline
        }
}

Write-Host (
    "Foundry guidance contract passed: " +
    "$($result.agentsLines) root lines, " +
    "$($result.instructionCount) instructions, " +
    "$($result.documentationPages) documentation pages, " +
    "$($result.adrCount) ADRs.")

return $result
