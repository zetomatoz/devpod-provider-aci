# File: hack/build.ps1

param(
    [string]$Version = "0.1.0",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
$BuildDir = Join-Path $ProjectDir "dist"

Write-Host "Building DevPod ACI Provider v$Version" -ForegroundColor Green

# Clean build directory if requested
if ($Clean -or !(Test-Path $BuildDir)) {
    if (Test-Path $BuildDir) {
        Remove-Item -Path $BuildDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $BuildDir | Out-Null
}

# Define platforms
$Platforms = @(
    @{ Runtime = "linux-x64"; Extension = "" },
    @{ Runtime = "linux-arm64"; Extension = "" },
    @{ Runtime = "osx-x64"; Extension = "" },
    @{ Runtime = "osx-arm64"; Extension = "" },
    @{ Runtime = "win-x64"; Extension = ".exe" }
)

# Build for each platform
foreach ($Platform in $Platforms) {
    $Runtime = $Platform.Runtime
    $Extension = $Platform.Extension
    
    Write-Host "Building for $Runtime..." -ForegroundColor Yellow
    
    $OutputDir = Join-Path $BuildDir $Runtime
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    
    # Build the project
    dotnet publish `
        "$ProjectDir\src\DevPod.Provider.ACI\DevPod.Provider.ACI.csproj" `
        -c Release `
        -r $Runtime `
        -p:PublishSingleFile=true `
        -p:SelfContained=true `
        -p:PublishTrimmed=true `
        -p:PublishReadyToRun=true `
        -p:Version=$Version `
        -o $OutputDir
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $Runtime"
        exit 1
    }
    
    # Rename output file
    $SourceFile = Join-Path $OutputDir "devpod-provider-aci$Extension"
    $TargetFile = Join-Path $BuildDir "devpod-provider-aci-$Runtime$Extension"
    
    if (Test-Path $SourceFile) {
        Move-Item -Path $SourceFile -Destination $TargetFile -Force
    }
    
    # Clean up
    Remove-Item -Path $OutputDir -Recurse -Force
}

# Generate checksums
Write-Host "Generating checksums..." -ForegroundColor Yellow
Get-ChildItem -Path $BuildDir -Filter "devpod-provider-aci-*" | ForEach-Object {
    $Hash = Get-FileHash -Path $_.FullName -Algorithm SHA256
    $HashFile = "$($_.FullName).sha256"
    "$($Hash.Hash)  $($_.Name)" | Out-File -FilePath $HashFile -Encoding UTF8
}

Write-Host "Build complete! Artifacts in $BuildDir" -ForegroundColor Green
Get-ChildItem -Path $BuildDir | Format-Table Name, Length, LastWriteTime