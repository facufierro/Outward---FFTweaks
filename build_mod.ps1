$ErrorActionPreference = "Stop"

$solutionDir = "$PSScriptRoot"
$binDir = "$solutionDir\bin"
$publishDir = "$solutionDir\bin\Debug\publish"
$outwardManagedPath = "D:\Games\Steam\steamapps\common\Outward\Outward_Data\Managed"
$bepInExCorePath = "c:\Users\fierr\AppData\Roaming\r2modmanPlus-local\OutwardDe\profiles\Classfixes\BepInEx\core"

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

# Read package version from root manifest
$manifestPath = "$solutionDir\manifest.json"
if (-not (Test-Path $manifestPath)) {
    Write-Error "manifest.json not found at $manifestPath"
}
$manifest = Get-Content $manifestPath | ConvertFrom-Json
$version = $manifest.version_number
$modName = $manifest.name
$author = "fierrof"
$zipName = "$author-$modName-$version.zip"

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
Copy-Item "$solutionDir\manifest.json" -Destination $publishDir -Force
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
