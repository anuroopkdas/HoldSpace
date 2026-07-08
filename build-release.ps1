# HoldSpace Release Build Script
# Creates a self-contained release, portable ZIP, and installer.

$Version = "0.1.0-beta"
$PublishDir = "release/HoldSpace-$Version"
$ZipPath = "release/HoldSpace-$Version-portable.zip"
$InstallerScript = "installer.iss"

Write-Host "=================== Building HoldSpace v$Version ===================" -ForegroundColor Cyan

# 1. Clean existing release folder
if (Test-Path "release") {
    Write-Host "Cleaning existing release directory..." -ForegroundColor Yellow
    Remove-Item -Path "release" -Recurse -Force
}

# 2. Publish win-x64 Release configuration (Framework dependent for reliable offline build)
# Note: For self-contained single-file build with internet connection, you can use:
# dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $PublishDir
Write-Host "Publishing Release build..." -ForegroundColor Yellow
dotnet publish -c Release --no-self-contained -o $PublishDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Dotnet publish failed!"
    exit $LASTEXITCODE
}

# 3. Copy documentation files to publish directory
Write-Host "Copying documentation..." -ForegroundColor Yellow
Copy-Item -Path "README_BETA.md" -Destination $PublishDir
Copy-Item -Path "RELEASE_NOTES.md" -Destination $PublishDir

# 4. Create Portable ZIP
Write-Host "Creating Portable ZIP package..." -ForegroundColor Yellow
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -Force
Write-Host "Portable ZIP created at: $ZipPath" -ForegroundColor Green

# 5. Build Installer using Inno Setup (if available)
$IsccPath = Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $IsccPath) {
    # Check default Inno Setup path
    $DefaultIscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path $DefaultIscc) {
        $IsccPath = $DefaultIscc
    }
}

if ($IsccPath) {
    Write-Host "Found Inno Setup at: $IsccPath" -ForegroundColor Yellow
    Write-Host "Compiling installer..." -ForegroundColor Yellow
    & $IsccPath $InstallerScript
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Inno Setup compilation failed!"
    } else {
        Write-Host "Installer created successfully in release/ folder!" -ForegroundColor Green
    }
} else {
    Write-Warning "Inno Setup (iscc.exe) not found in PATH or standard installation folder. Skipping installer generation."
    Write-Host "You can manually open '$InstallerScript' inside Inno Setup to compile the setup package later." -ForegroundColor Yellow
}

Write-Host "=================== Release build completed! ===================" -ForegroundColor Green
