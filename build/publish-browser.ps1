#!/usr/bin/env pwsh
#
# Publish the browser gallery into a clean directory.
#
# `dotnet publish -o artifacts/browser` does not remove the previous run's fingerprinted assets. A few
# publishes later the directory is much larger than the current build, and every stale file is uploaded.
# This script removes the target first, then publishes with the project's own AOT setting.
#
# Usage, from the repository root:
#   ./build/publish-browser.ps1                 # AOT, about thirteen minutes
#   ./build/publish-browser.ps1 -NoAot          # interpreter, much faster and smaller
#   ./build/publish-browser.ps1 -Output artifacts/browser-check

[CmdletBinding()]
param(
    [string]$Output = 'artifacts/browser',
    [switch]$NoAot
)

$ErrorActionPreference = 'Stop'

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$out = [System.IO.Path]::GetFullPath((Join-Path $root $Output))
$rootPrefix = $root.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $out.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Output must stay inside the repository: $out"
}

if (Test-Path -LiteralPath $out) {
    Remove-Item -LiteralPath $out -Recurse -Force
}

$project = Join-Path $root 'src/AvCharts.Demo.Browser/AvCharts.Demo.Browser.csproj'
$publishArgs = @('publish', $project, '-c', 'Release', '-o', $out)
if ($NoAot) {
    $publishArgs += '-p:RunAOTCompilation=false'
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$files = Get-ChildItem -LiteralPath $out -Recurse -File
$megabytes = [math]::Round((($files | Measure-Object -Property Length -Sum).Sum) / 1MB, 1)
"Published $($files.Count) files, $megabytes MB: $out"
