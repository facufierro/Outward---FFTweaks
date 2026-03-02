param(
    [ValidateSet("build", "bump", "set-version", "sync-deps")]
    [string]$Action = "build",

    [ValidateSet("dev", "release")]
    [string]$Channel = "dev",

    [ValidateSet("patch", "minor", "major")]
    [string]$Bump = "patch",

    [string]$Version,

    [switch]$BuildAfterChange,

    [string]$PluginsRoot,

    [string]$BepInExCorePath,

    [string]$OutwardManagedPath,

    [switch]$SkipDependencySync
)

$ErrorActionPreference = "Stop"

$solutionDir = "$PSScriptRoot"
$binDir = "$solutionDir\bin"
$publishDir = "$solutionDir\bin\Debug\publish"
$manifestPath = "$solutionDir\manifest.json"
$devStatePath = "$solutionDir\.dev-version.json"

$projects = @(
    @{
        Name = "FFTweaks"
        ProjectFile = "$solutionDir\FFTweaks.csproj"
        DllName = "FFTweaks.dll"
    }
)

function Resolve-BepInExCorePath([string]$value) {
    if (-not [string]::IsNullOrWhiteSpace($value) -and (Test-Path (Join-Path $value "BepInEx.dll")) -and (Test-Path (Join-Path $value "0Harmony.dll"))) {
        return $value
    }

    $envPath = $env:R2MODMAN_BEPINEX_CORE
    if (-not [string]::IsNullOrWhiteSpace($envPath) -and (Test-Path (Join-Path $envPath "BepInEx.dll")) -and (Test-Path (Join-Path $envPath "0Harmony.dll"))) {
        return $envPath
    }

    $profilesRoot = Join-Path $env:APPDATA "r2modmanPlus-local\OutwardDe\profiles"
    if (Test-Path $profilesRoot) {
        $candidates = Get-ChildItem -Path $profilesRoot -Directory -ErrorAction SilentlyContinue |
            ForEach-Object {
                $corePath = Join-Path $_.FullName "BepInEx\core"
                if ((Test-Path (Join-Path $corePath "BepInEx.dll")) -and (Test-Path (Join-Path $corePath "0Harmony.dll"))) {
                    [PSCustomObject]@{
                        Path = $corePath
                        LastWriteTime = $_.LastWriteTime
                    }
                }
            } |
            Where-Object { $_ -ne $null } |
            Sort-Object LastWriteTime -Descending

        if ($candidates -and $candidates.Count -gt 0) {
            return $candidates[0].Path
        }
    }

    Write-Error "Could not resolve BepInEx core path. Pass -BepInExCorePath or set R2MODMAN_BEPINEX_CORE."
}

function Resolve-OutwardManagedPath([string]$value) {
    if (-not [string]::IsNullOrWhiteSpace($value) -and (Test-Path (Join-Path $value "Assembly-CSharp.dll"))) {
        return $value
    }

    $envPath = $env:OUTWARD_MANAGED_PATH
    if (-not [string]::IsNullOrWhiteSpace($envPath) -and (Test-Path (Join-Path $envPath "Assembly-CSharp.dll"))) {
        return $envPath
    }

    $candidateRoots = @(
        "D:\Games\Steam\steamapps\common\Outward",
        "C:\Program Files (x86)\Steam\steamapps\common\Outward",
        "C:\Program Files\Steam\steamapps\common\Outward"
    )

    foreach ($root in $candidateRoots) {
        $managedCandidates = @(
            (Join-Path $root "Outward_Data\Managed"),
            (Join-Path $root "Outward_Defed\Outward Definitive Edition_Data\Managed")
        )

        foreach ($candidate in $managedCandidates) {
            if (Test-Path (Join-Path $candidate "Assembly-CSharp.dll")) {
                return $candidate
            }
        }
    }

    Write-Error "Could not resolve Outward managed path. Pass -OutwardManagedPath or set OUTWARD_MANAGED_PATH."
}

function Resolve-PluginsRoot([string]$value, [string]$resolvedBepInExCorePath) {
    if (-not [string]::IsNullOrWhiteSpace($value) -and (Test-Path $value)) {
        return $value
    }

    $envPath = $env:R2MODMAN_PLUGINS_ROOT
    if (-not [string]::IsNullOrWhiteSpace($envPath) -and (Test-Path $envPath)) {
        return $envPath
    }

    if (-not [string]::IsNullOrWhiteSpace($resolvedBepInExCorePath)) {
        $fromCore = Join-Path (Split-Path $resolvedBepInExCorePath -Parent) "plugins"
        if (Test-Path $fromCore) {
            return $fromCore
        }
    }

    $profilesRoot = Join-Path $env:APPDATA "r2modmanPlus-local\OutwardDe\profiles"
    if (Test-Path $profilesRoot) {
        # Prefer a profile whose folder name matches the mod/solution name
        $modName = (Split-Path $script:solutionDir -Leaf)
        $preferredPlugins = Join-Path $profilesRoot "$modName\BepInEx\plugins"
        if (Test-Path $preferredPlugins) {
            return $preferredPlugins
        }

        $candidates = Get-ChildItem -Path $profilesRoot -Directory -ErrorAction SilentlyContinue |
            ForEach-Object {
                $pluginsPath = Join-Path $_.FullName "BepInEx\plugins"
                if (Test-Path $pluginsPath) {
                    [PSCustomObject]@{
                        Path = $pluginsPath
                        LastWriteTime = $_.LastWriteTime
                    }
                }
            } |
            Where-Object { $_ -ne $null } |
            Sort-Object LastWriteTime -Descending

        if ($candidates -and $candidates.Count -gt 0) {
            return $candidates[0].Path
        }
    }

    Write-Error "Could not resolve plugins root. Pass -PluginsRoot or set R2MODMAN_PLUGINS_ROOT."
}

function Get-Manifest {
    if (-not (Test-Path $manifestPath)) {
        Write-Error "manifest.json not found at $manifestPath"
    }

    return Get-Content $manifestPath | ConvertFrom-Json
}

function Save-Manifest($manifest) {
    $json = $manifest | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($manifestPath, $json, (New-Object System.Text.UTF8Encoding $false))
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
    $json = $nextState | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($devStatePath, $json, (New-Object System.Text.UTF8Encoding $false))

    return $nextDevVersion
}

function Get-Author($manifest) {
    if ($manifest.author_name -and -not [string]::IsNullOrWhiteSpace($manifest.author_name)) {
        return $manifest.author_name
    }

    if ($manifest.author -and -not [string]::IsNullOrWhiteSpace($manifest.author)) {
        return $manifest.author
    }

    return "fierrof"
}

function Ensure-ManifestAuthor($manifest) {
    return $manifest
}

function Get-PluginDependencies([string]$pluginsRoot, [string]$selfAuthor, [string]$selfName) {
    if (-not (Test-Path $pluginsRoot)) {
        Write-Warning "PluginsRoot not found: $pluginsRoot"
        return @()
    }

    $dependencies = @()
    $pluginDirs = Get-ChildItem $pluginsRoot -Directory

    foreach ($dir in $pluginDirs) {
        $folder = $dir.Name
        if ($folder -eq "$selfAuthor-$selfName") {
            continue
        }

        $parts = $folder -split '-', 2
        $fallbackAuthor = if ($parts.Length -ge 1) { $parts[0] } else { "" }
        $fallbackName = if ($parts.Length -ge 2) { $parts[1] } else { $folder }

        $manifestCandidate = Join-Path $dir.FullName "manifest.json"
        $author = ""
        $name = ""
        $version = ""

        if (Test-Path $manifestCandidate) {
            try {
                $pluginManifest = Get-Content $manifestCandidate -Raw | ConvertFrom-Json

                if ($pluginManifest.PSObject.Properties.Name -contains "author" -and $pluginManifest.author) {
                    $author = [string]$pluginManifest.author
                } elseif ($pluginManifest.PSObject.Properties.Name -contains "author_name" -and $pluginManifest.author_name) {
                    $author = [string]$pluginManifest.author_name
                }

                if ($pluginManifest.PSObject.Properties.Name -contains "name" -and $pluginManifest.name) {
                    $name = [string]$pluginManifest.name
                }

                if ($pluginManifest.PSObject.Properties.Name -contains "version_number" -and $pluginManifest.version_number) {
                    $version = [string]$pluginManifest.version_number
                }
            } catch {
                Write-Warning "Failed to parse plugin manifest: $manifestCandidate"
            }
        }

        if ([string]::IsNullOrWhiteSpace($name)) {
            $name = $fallbackName
        }

        if ([string]::IsNullOrWhiteSpace($author)) {
            if (-not [string]::IsNullOrWhiteSpace($name) -and $folder.EndsWith("-$name")) {
                $author = $folder.Substring(0, $folder.Length - $name.Length - 1)
            } else {
                $author = $fallbackAuthor
            }
        }

        if ([string]::IsNullOrWhiteSpace($version)) {
            Write-Warning "Skipping plugin '$folder' (missing version_number)"
            continue
        }

        $dependencies += "$author-$name-$version"
    }

    $dependencies = $dependencies | Sort-Object -Unique
    return @($dependencies)
}

function Sync-ManifestDependencies($manifest) {
    $manifest = Ensure-ManifestAuthor -manifest $manifest

    $resolvedBepInExCorePath = Resolve-BepInExCorePath -value $BepInExCorePath
    $resolvedPluginsRoot = Resolve-PluginsRoot -value $PluginsRoot -resolvedBepInExCorePath $resolvedBepInExCorePath

    $selfAuthor = Get-Author -manifest $manifest
    $selfName = [string]$manifest.name

    $dependencies = Get-PluginDependencies -pluginsRoot $resolvedPluginsRoot -selfAuthor $selfAuthor -selfName $selfName

    if (-not ($dependencies -contains "BepInEx-BepInExPack_Outward-5.4.19")) {
        $dependencies = @("BepInEx-BepInExPack_Outward-5.4.19") + $dependencies
    }

    $manifest.dependencies = @($dependencies)
    Save-Manifest -manifest $manifest

    Write-Host "Synced manifest dependencies from: $resolvedPluginsRoot"
    Write-Host "Dependency count: $($manifest.dependencies.Count)"

    return Get-Manifest
}

function Get-ManifestForBuild($manifest) {
    if ($SkipDependencySync) {
        return $manifest
    }

    return Sync-ManifestDependencies -manifest $manifest
}

function Invoke-PackageBuild($manifest, [string]$channel) {
    $manifest = Ensure-ManifestAuthor -manifest $manifest

    $resolvedBepInExCorePath = Resolve-BepInExCorePath -value $BepInExCorePath
    $resolvedOutwardManagedPath = Resolve-OutwardManagedPath -value $OutwardManagedPath

    $releaseVersion = $manifest.version_number
    [void](Get-SemVerParts -value $releaseVersion)

    $packageVersion = if ($channel -eq "dev") {
        Get-NextDevVersion -releaseVersion $releaseVersion
    } else {
        $releaseVersion
    }

    $modName = $manifest.name
    $author = Get-Author -manifest $manifest
    $zipName = "$author-$modName-$packageVersion.zip"
    $channelBinDir = Join-Path $binDir $channel

    Write-Host "Build channel: $channel"
    Write-Host "Release version: $releaseVersion"
    Write-Host "Package version: $packageVersion"
    Write-Host "BepInEx core: $resolvedBepInExCorePath"
    Write-Host "Outward managed: $resolvedOutwardManagedPath"

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
        dotnet publish $project.ProjectFile -c Debug -o "$publishDir" -p:OutwardManagedPath="$resolvedOutwardManagedPath" -p:BepInExCorePath="$resolvedBepInExCorePath"
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
    $json = $buildManifest | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText("$publishDir\manifest.json", $json, (New-Object System.Text.UTF8Encoding $false))

    Copy-Item "$solutionDir\README.md" -Destination $publishDir -Force
    Copy-Item "$solutionDir\CHANGELOG.md" -Destination $publishDir -Force
    $iconSource = "$solutionDir\icon.png"
    if (Test-Path $iconSource) {
        Copy-Item $iconSource -Destination $publishDir -Force
    } else {
        Write-Warning "icon.png not found at $iconSource"
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
    $packageRootDir = "$binDir\temp\package"
    $packagePluginsDir = "$packageRootDir\plugins"

    if (Test-Path $packageRootDir) {
        Remove-Item $packageRootDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    New-Item -ItemType Directory -Path $packagePluginsDir -Force | Out-Null

    $metadataFiles = @("manifest.json", "README.md", "CHANGELOG.md", "icon.png")
    $excludeExtensions = @(".pdb")

    foreach ($metadata in $metadataFiles) {
        $metadataSource = Join-Path $publishDir $metadata
        if (Test-Path $metadataSource) {
            Copy-Item $metadataSource -Destination (Join-Path $packageRootDir $metadata) -Force
        }
    }

    foreach ($item in Get-ChildItem -Path $publishDir -Force) {
        if ($metadataFiles -contains $item.Name) {
            continue
        }

        if (-not $item.PSIsContainer -and $excludeExtensions -contains $item.Extension) {
            continue
        }

        $destination = Join-Path $packagePluginsDir $item.Name
        if ($item.PSIsContainer) {
            Copy-Item $item.FullName -Destination $destination -Recurse -Force
        } else {
            Copy-Item $item.FullName -Destination $destination -Force
        }
    }

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    Compress-Archive -Path "$packageRootDir\*" -DestinationPath $zipPath

    Write-Host "Build complete: $zipPath"
}

$manifest = Get-Manifest
$manifest = Ensure-ManifestAuthor -manifest $manifest

switch ($Action) {
    "build" {
        $manifest = Get-ManifestForBuild -manifest $manifest
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
            $manifest = Get-ManifestForBuild -manifest $manifest
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
            $manifest = Get-ManifestForBuild -manifest $manifest
            Invoke-PackageBuild -manifest $manifest -channel $Channel
        }
    }

    "sync-deps" {
        [void](Sync-ManifestDependencies -manifest $manifest)
    }
}
