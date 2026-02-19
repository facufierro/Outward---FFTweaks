param(
    [ValidateSet("build", "bump", "set-version")]
    [string]$Action = "build",

    [ValidateSet("dev", "release")]
    [string]$Channel = "dev",

    [ValidateSet("patch", "minor", "major")]
    [string]$Bump = "patch",

    [string]$Version,

    [switch]$BuildAfterChange
)

$ErrorActionPreference = "Stop"

$solutionDir = "$PSScriptRoot"
$binDir = "$solutionDir\bin"
$publishDir = "$solutionDir\bin\Debug\publish"
$outwardManagedPath = "D:\Games\Steam\steamapps\common\Outward\Outward_Data\Managed"
$bepInExCorePath = "c:\Users\fierr\AppData\Roaming\r2modmanPlus-local\OutwardDe\profiles\Classfixes\BepInEx\core"
$manifestPath = "$solutionDir\manifest.json"

$projects = @(
    @{
        Name = "FFT.TrueHardcore"
        ProjectFile = "$solutionDir\FFT.TrueHardcore\FFT.TrueHardcore.csproj"
        DllName = "FFT.TrueHardcore.dll"
    },
    @{
        Name = "FFT.KnivesMaster"
        ProjectFile = "$solutionDir\FFT.KnivesMaster\FFT.KnivesMaster.csproj"
        DllName = "FFT.KnivesMaster.dll"
    }
)

function Get-Manifest {
    if (-not (Test-Path $manifestPath)) {
        Write-Error "manifest.json not found at $manifestPath"
    }

    return Get-Content $manifestPath | ConvertFrom-Json
}

function Save-Manifest($manifest) {
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $manifestPath -Encoding UTF8
}

function Get-SemVerParts([string]$value) {
    $match = [regex]::Match($value, '^(\d+)\.(\d+)\.(\d+)$')
    if (-not $match.Success) {
        Write-Error "Version must be strict SemVer core (x.y.z). Current value: '$value'"
    }

    return @(
        [int]$match.Groups[1].Value,
        [int]$match.Groups[2].Value,
        [int]$match.Groups[3].Value
    )
}

function Get-BumpedVersion([string]$currentVersion, [string]$bumpType) {
    $parts = Get-SemVerParts -value $currentVersion
    $major = $parts[0]
    $minor = $parts[1]
    $patch = $parts[2]

    switch ($bumpType) {
        "major" {
            $major++
            $minor = 0
            $patch = 0
        }
        "minor" {
            $minor++
            $patch = 0
        }
        "patch" {
            $patch++
        }
    }

    return "$major.$minor.$patch"
}

function Invoke-PackageBuild($manifest, [string]$channel) {
    $releaseVersion = $manifest.version_number
    $packageVersion = if ($channel -eq "release") {
        $releaseVersion
    } else {
        "$releaseVersion-dev.$(Get-Date -Format yyyyMMddHHmmss)"
    }

    $modName = $manifest.name
    $author = "fierrof"
    $zipName = "$author-$modName-$packageVersion.zip"

    Write-Host "Build channel: $channel"
    Write-Host "Package version: $packageVersion"

    Write-Host "Cleaning bin and obj folders..."
    Get-ChildItem -Path $solutionDir -Include bin,obj -Recurse | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "Building and publishing projects..."
    foreach ($project in $projects) {
        Write-Host "Publishing $($project.Name)..."
        dotnet publish $project.ProjectFile -c Debug -o "$publishDir" -p:OutwardManagedPath="$outwardManagedPath" -p:BepInExCorePath="$bepInExCorePath"
        if ($LASTEXITCODE -ne 0) {
            Write-Error "dotnet publish failed for $($project.Name)"
        }
    }

    Write-Host "Copying assets..."
    if (-not (Test-Path $publishDir)) {
        New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    }

    $buildManifest = [PSCustomObject]@{
        name = $manifest.name
        version_number = $packageVersion
        website_url = $manifest.website_url
        description = $manifest.description
        dependencies = @($manifest.dependencies)
    }
    $buildManifest | ConvertTo-Json -Depth 10 | Set-Content -Path "$publishDir\manifest.json" -Encoding UTF8

    Copy-Item "$solutionDir\README.md" -Destination $publishDir -Force
    Copy-Item "$solutionDir\CHANGELOG.md" -Destination $publishDir -Force

    Write-Host "Verifying plugin DLLs..."
    foreach ($project in $projects) {
        $dllPath = "$publishDir\$($project.DllName)"
        if (-not (Test-Path $dllPath)) {
            Write-Host "Contents of publish dir:"
            Get-ChildItem $publishDir | Select-Object Name
            Write-Error "Main DLL not found at $dllPath"
        }

        Write-Host "Found DLL at $dllPath"
    }

    Write-Host "Preparing output directory..."
    if (-not (Test-Path $binDir)) {
        New-Item -ItemType Directory -Path $binDir -Force | Out-Null
    }

    Write-Host "Zipping to $zipName ..."
    $zipPath = "$binDir\$zipName"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath

    Write-Host "Build complete: $zipPath"
}

$manifest = Get-Manifest

switch ($Action) {
    "build" {
        Invoke-PackageBuild -manifest $manifest -channel $Channel
    }

    "set-version" {
        if ([string]::IsNullOrWhiteSpace($Version)) {
            Write-Error "-Version is required for -Action set-version"
        }

        [void](Get-SemVerParts -value $Version)
        $oldVersion = $manifest.version_number
        $manifest.version_number = $Version
        Save-Manifest -manifest $manifest
        Write-Host "Manifest version updated: $oldVersion -> $Version"

        if ($BuildAfterChange) {
            Invoke-PackageBuild -manifest $manifest -channel $Channel
        }
    }

    "bump" {
        $oldVersion = $manifest.version_number
        $newVersion = Get-BumpedVersion -currentVersion $oldVersion -bumpType $Bump
        $manifest.version_number = $newVersion
        Save-Manifest -manifest $manifest
        Write-Host "Manifest version bumped ($Bump): $oldVersion -> $newVersion"

        if ($BuildAfterChange) {
            Invoke-PackageBuild -manifest $manifest -channel $Channel
        }
    }
}
