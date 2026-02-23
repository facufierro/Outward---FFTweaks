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
$devStatePath = "$solutionDir\.dev-version.json"

$projects = @(
    @{
        Name = "FFT.TrueHardcore"
        ProjectFile = "$solutionDir\FFT.TrueHardcore\FFT.TrueHardcore.csproj"
        DllName = "FFT.TrueHardcore.dll"
    },
    @{
        Name = "FFT.Knives_Master"
        ProjectFile = "$solutionDir\FFT.Knives_Master\FFT.Knives_Master.csproj"
        DllName = "FFT.Knives_Master.dll"
    },
    @{
        Name = "FFT.Classfixes_Part_1"
        ProjectFile = "$solutionDir\FFT.Classfixes_Part_1\FFT.Classfixes_Part_1.csproj"
        DllName = "FFT.Classfixes_Part_1.dll"
    },
    @{
        Name = "FFT.Classfixes_Part_2"
        ProjectFile = "$solutionDir\FFT.Classfixes_Part_2\FFT.Classfixes_Part_2.csproj"
        DllName = "FFT.Classfixes_Part_2.dll"
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

function Get-NextDevVersion([string]$releaseVersion) {
    [void](Get-SemVerParts -value $releaseVersion)

    $baseVersion = $releaseVersion
    if (Test-Path $devStatePath) {
        $state = Get-Content $devStatePath | ConvertFrom-Json
        if ($state -and $state.release_version -eq $releaseVersion -and -not [string]::IsNullOrWhiteSpace($state.dev_version)) {
            $baseVersion = $state.dev_version
        }
    }

    $nextDevVersion = Get-BumpedVersion -currentVersion $baseVersion -bumpType "patch"

    $nextState = [PSCustomObject]@{
        release_version = $releaseVersion
        dev_version = $nextDevVersion
        updated_utc = (Get-Date).ToUniversalTime().ToString("o")
    }
    $nextState | ConvertTo-Json -Depth 5 | Set-Content -Path $devStatePath -Encoding UTF8

    return $nextDevVersion
}

function Ensure-ManifestAuthor($manifest) {
    if ([string]::IsNullOrWhiteSpace($manifest.author_name)) {
        $manifest | Add-Member -NotePropertyName author_name -NotePropertyValue "fierrof" -Force
    }

    return $manifest
}

function Invoke-PackageBuild($manifest, [string]$channel) {
    $manifest = Ensure-ManifestAuthor -manifest $manifest

    $releaseVersion = $manifest.version_number
    [void](Get-SemVerParts -value $releaseVersion)

    $packageVersion = if ($channel -eq "dev") {
        Get-NextDevVersion -releaseVersion $releaseVersion
    } else {
        $releaseVersion
    }

    $modName = $manifest.name
    $author = $manifest.author_name
    $zipName = "$author-$modName-$packageVersion.zip"
    $channelBinDir = Join-Path $binDir $channel

    Write-Host "Build channel: $channel"
    Write-Host "Release version: $releaseVersion"
    Write-Host "Package version: $packageVersion"

    Write-Host "Cleaning bin and obj folders..."
    foreach ($project in $projects) {
        $projectDir = Split-Path $project.ProjectFile -Parent
        $projectBin = Join-Path $projectDir "bin"
        $projectObj = Join-Path $projectDir "obj"

        if (Test-Path $projectBin) {
            Remove-Item $projectBin -Recurse -Force -ErrorAction SilentlyContinue
        }

        if (Test-Path $projectObj) {
            Remove-Item $projectObj -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
    }

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
        author_name = $author
        name = $manifest.name
        version_number = $packageVersion
        website_url = $manifest.website_url
        description = $manifest.description
        icon = $manifest.icon
        dependencies = @($manifest.dependencies)
    }
    $buildManifest | ConvertTo-Json -Depth 10 | Set-Content -Path "$publishDir\manifest.json" -Encoding UTF8

    Copy-Item "$solutionDir\README.md" -Destination $publishDir -Force
    Copy-Item "$solutionDir\CHANGELOG.md" -Destination $publishDir -Force
    if (Test-Path "$solutionDir\icon.png") {
        Copy-Item "$solutionDir\icon.png" -Destination $publishDir -Force
    }

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
    if (-not (Test-Path $channelBinDir)) {
        New-Item -ItemType Directory -Path $channelBinDir -Force | Out-Null
    }

    $zipPattern = "$author-$modName-*.zip"
    Get-ChildItem -Path $channelBinDir -Filter $zipPattern -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne $zipName } |
        Remove-Item -Force -ErrorAction SilentlyContinue

    Write-Host "Zipping to $zipName ..."
    $zipPath = "$channelBinDir\$zipName"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath

    Write-Host "Build complete: $zipPath"
}

$manifest = Get-Manifest
$manifest = Ensure-ManifestAuthor -manifest $manifest

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
