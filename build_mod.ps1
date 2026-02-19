$ErrorActionPreference = "Stop"

$solutionDir = "$PSScriptRoot"
$projectDir = "$solutionDir\FFTweaks.TrueHardcore.Plugin"
$binDir = "$solutionDir\bin"
$publishDir = "$projectDir\bin\Debug\publish"
$outwardManagedPath = "D:\Games\Steam\steamapps\common\Outward\Outward_Data\Managed"

# Read version from manifest
$manifestPath = "$projectDir\manifest.json"
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

Write-Host "Building and Publishing..."
dotnet publish "$projectDir\FFTweaks.TrueHardcore.Plugin.csproj" -c Debug -o "$publishDir" -p:OutwardManagedPath="$outwardManagedPath"

Write-Host "Copying Assets..."
if (-not (Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
}
Copy-Item "$projectDir\manifest.json" -Destination $publishDir -Force
Copy-Item "$solutionDir\README.md" -Destination $publishDir -Force
Copy-Item "$solutionDir\CHANGELOG.md" -Destination $publishDir -Force

# Verify DLL exists
$dllPath = "$publishDir\FFTweaks.TrueHardcore.Plugin.dll"
if (-not (Test-Path $dllPath)) {
    Write-Host "Contents of publish dir:"
    Get-ChildItem $publishDir | Select-Object Name
    Write-Error "Main DLL not found at $dllPath"
} else {
    Write-Host "Found Main DLL at $dllPath"
}

Write-Host "Preparing output directory..."
if (-not (Test-Path $binDir)) {
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null
}

Write-Host "Zipping to $zipName ..."
$zipPath = "$binDir\$zipName"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath

Write-Host "Build Complete: $zipPath"
