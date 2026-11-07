# Creates local test, Workshop-staging, and ZIP artifacts. Never deploys or uploads.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$assemblyInfo = [IO.File]::ReadAllText((Join-Path $repositoryRoot 'Properties\AssemblyInfo.cs'))
$versionMatch = [regex]::Match($assemblyInfo, 'AssemblyInformationalVersion\("(\d+\.\d+\.\d+-beta\.\d+)"\)')
if (-not $versionMatch.Success) { throw 'Expected an explicit beta version in AssemblyInfo.cs.' }
$betaVersion = $versionMatch.Groups[1].Value
$manifestPath = Join-Path $repositoryRoot 'item.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.Title -ne "Tacview Singleplayer Recorder [$betaVersion]" -or @($manifest.DependenciesIds).Count -ne 0) {
    throw 'Candidate manifest version or dependency declaration is inconsistent.'
}

function Assert-ArtifactPath([string]$Path) {
    $absolutePath = [IO.Path]::GetFullPath($Path)
    $artifactPrefix = (Join-Path $repositoryRoot 'artifacts') + [IO.Path]::DirectorySeparatorChar
    if (-not $absolutePath.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Artifact path escaped the repository: $absolutePath"
    }
    # MSBuild recreates staging directories. Reject junctions/symlinks before
    # allowing its RemoveDir targets to touch any existing generated directory.
    $ancestor = $absolutePath
    while ($ancestor -and $ancestor.Length -gt $repositoryRoot.Length) {
        if (Test-Path -LiteralPath $ancestor) {
            $item = Get-Item -LiteralPath $ancestor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Artifact path contains a reparse point: $ancestor"
            }
        }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
}

$betaRoot = Join-Path $repositoryRoot "artifacts\beta\$betaVersion"
$testRoot = Join-Path $betaRoot 'test-package\VTOLVRTacviewSPRecorder'
$workshopRoot = Join-Path $betaRoot 'workshop-staging\VTOLVRTacviewSPRecorder'
$releaseRoot = Join-Path $betaRoot 'release'
$zipPath = Join-Path $releaseRoot "Tacview-Singleplayer-Recorder-$betaVersion.zip"
foreach ($path in @($testRoot, $workshopRoot, $releaseRoot, $zipPath)) { Assert-ArtifactPath $path }

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetPath = if ($dotnetCommand) { $dotnetCommand.Source } else { Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
if (-not (Test-Path -LiteralPath $dotnetPath)) { throw '.NET SDK was not found.' }
& $dotnetPath msbuild (Join-Path $repositoryRoot 'VTOLVRTacviewSPRecorder.csproj') `
    '-restore' '-t:CreateTestPackage;CreateWorkshopStaging' '-p:Configuration=Release' '-p:DeployMod=false' `
    "-p:TestPackageRoot=$testRoot" "-p:WorkshopStagingRoot=$workshopRoot" '-verbosity:minimal' '-nologo'
if ($LASTEXITCODE -ne 0) { throw 'Candidate build/staging failed.' }

$dllName = 'VTOLVRTacviewSPRecorder.dll'
$testDll = Join-Path $testRoot $dllName
$workshopDll = Join-Path $workshopRoot $dllName
$expectedTestFiles = @($dllName, 'VTOLVRTacviewSPRecorder.pdb', 'item.json')
if (Compare-Object $expectedTestFiles @(Get-ChildItem -LiteralPath $testRoot -Force | Select-Object -ExpandProperty Name)) {
    throw 'Unexpected private test package contents.'
}
if (Compare-Object @($dllName) @(Get-ChildItem -LiteralPath $workshopRoot -Force | Select-Object -ExpandProperty Name)) {
    throw 'Workshop staging must contain exactly the Release DLL.'
}
$dllHash = (Get-FileHash -LiteralPath $testDll -Algorithm SHA256).Hash.ToLowerInvariant()
if ($dllHash -ne (Get-FileHash -LiteralPath $workshopDll -Algorithm SHA256).Hash.ToLowerInvariant()) {
    throw 'Test and Workshop DLL hashes differ.'
}
$fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($testDll)
if ($fileVersion.ProductVersion -ne $betaVersion -or $fileVersion.FileVersion -ne ($betaVersion.Split('-')[0] + '.0')) {
    throw 'Built DLL version does not match the manifest.'
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::Create, [IO.FileAccess]::Write)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in @($dllName, 'item.json')) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, (Join-Path $testRoot $name), "VTOLVRTacviewSPRecorder/$name",
            [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose(); $stream.Dispose() }

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = Join-Path $releaseRoot 'SHA256SUMS.txt'
Assert-ArtifactPath $checksumPath
@("$zipHash  $([IO.Path]::GetFileName($zipPath))", "$dllHash  VTOLVRTacviewSPRecorder/$dllName") |
    Set-Content -LiteralPath $checksumPath -Encoding ASCII
Write-Output "Prepared local beta $betaVersion (not deployed or published)."
Write-Output "ZIP: $zipPath"
Write-Output "Workshop staging: $workshopRoot"
Write-Output "DLL SHA-256: $dllHash"
Write-Output "ZIP SHA-256: $zipHash"
