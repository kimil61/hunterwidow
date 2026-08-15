[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

function Assert-CommandSucceeded {
    param(
        [string]$Description,
        [scriptblock]$Action
    )

    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Assert-NoMatches {
    param(
        [string]$Description,
        [string]$Pattern,
        [string[]]$Paths
    )

    $matches = & rg -n --glob '*.cs' $Pattern @Paths
    if ($LASTEXITCODE -eq 0) {
        throw "$Description found forbidden matches:`n$matches"
    }

    if ($LASTEXITCODE -ne 1) {
        throw "$Description could not run (rg exit code $LASTEXITCODE)."
    }
}

Push-Location $projectRoot
try {
    Assert-CommandSucceeded 'Full content validation' {
        dotnet run --project Tools/ContentValidator/ContentValidator.csproj -- Assets/StreamingAssets/content
    }
    Assert-CommandSucceeded 'Minimal content validation' {
        dotnet run --project Tools/ContentValidator/ContentValidator.csproj -- Assets/StreamingAssets/content_minimal
    }
    Assert-CommandSucceeded '22-cycle economy simulation' {
        dotnet run --project Tools/EconomySim/EconomySim.csproj -- Assets/StreamingAssets/content --cycles 22
    }

    Assert-NoMatches 'Domain engine-reference scan' 'UnityEngine|UnityEditor|MonoBehaviour|ScriptableObject|DateTime\.Now|UnityEngine\.Random|\bTime\.' @('Assets/Scripts/Domain')
    Assert-NoMatches 'Runtime content-ID coupling scan' '"cfg_[a-z0-9_]+"' @('Assets/Scripts', 'Assets/Editor', 'Tools')
    Assert-NoMatches 'Domain tuning-number literal scan' '\b(?:[2-9][0-9]*|1[0-9]+|(?:0|1)\.[0-9]+)[dDfFmM]?\b' @(
        'Assets/Scripts/Domain/Alchemy',
        'Assets/Scripts/Domain/Combat',
        'Assets/Scripts/Domain/Common',
        'Assets/Scripts/Domain/Cycle',
        'Assets/Scripts/Domain/Dive',
        'Assets/Scripts/Domain/Economy',
        'Assets/Scripts/Domain/Enemy',
        'Assets/Scripts/Domain/Erosion',
        'Assets/Scripts/Domain/Inventory',
        'Assets/Scripts/Domain/Narrative',
        'Assets/Scripts/Domain/Persistence',
        'Assets/Scripts/Domain/Progression'
    )
    Assert-NoMatches 'Hard-coded player display-fragment scan' '" x"|" · "|"G"|"←"|"→"|"↑"|"↓"|string\.Join\(\s*", "' @('Assets/Scripts/Unity/Gameplay')
    Assert-NoMatches 'Hard-coded direct GUI text scan' 'GUI\.(?:Label|Button|Box|Window)\([^\r\n]*,\s*"' @('Assets/Scripts/Unity')

    Write-Output 'Static MVP verification passed.'
}
finally {
    Pop-Location
}
