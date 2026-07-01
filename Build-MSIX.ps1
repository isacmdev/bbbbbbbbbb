# Build-MSIX.ps1
# Genera MSIX para ControlParental.App.UI
# Uso: .\Build-MSIX.ps1 [-Configuration Release]

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$makeappx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"
$projectPath = "src/ControlParental.App.UI"
$stagingPath = "msix_staging"
$outputPath = "ControlParental.App.msix"

Write-Host "=== Building MSIX for ControlParental.App.UI ===" -ForegroundColor Cyan

# 1. Build del proyecto
Write-Host "[1/4] Building project..." -ForegroundColor Yellow
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
    "$projectPath/ControlParental.App.UI.csproj" `
    /t:Build `
    /p:Configuration=$Configuration `
    /p:Platform=x64 `
    /v:minimal

if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "    Build OK" -ForegroundColor Green

# 2. Limpiar staging
Write-Host "[2/4] Cleaning staging..." -ForegroundColor Yellow
if (Test-Path $stagingPath) { Remove-Item $stagingPath -Recurse -Force }
New-Item -ItemType Directory -Path $stagingPath | Out-Null

# 3. Copiar archivos del build
Write-Host "[3/4] Copying build files..." -ForegroundColor Yellow
$buildPath = "$projectPath/bin/x64/$Configuration/net9.0-windows10.0.19041.0/win-x64"
Copy-Item -Path "$buildPath/*" -Destination "$stagingPath/" -Recurse -Force

# Copiar assets del proyecto (logos reales)
$assetsPath = "$stagingPath/Assets"
if (-not (Test-Path $assetsPath)) { New-Item -ItemType Directory -Path $assetsPath | Out-Null }

$projectAssets = "$projectPath/Assets"
if (Test-Path $projectAssets) {
    Copy-Item -Path "$projectAssets/*.png" -Destination $assetsPath -Force -ErrorAction SilentlyContinue
    Write-Host "    Copied logos from project Assets"
}

# Crear logos placeholder si no se copiaron (GDI+ puede fallar en algunos entornos)
$logos = @(
    "StoreLogo.png", "Square44x44Logo.png", "Square71x71Logo.png",
    "Square150x150Logo.png", "Square310x310Logo.png", "Wide310x150Logo.png"
)
foreach ($logo in $logos) {
    $path = "$assetsPath/$logo"
    if (-not (Test-Path $path) -or (Get-Item $path -ErrorAction SilentlyContinue).Length -lt 100) {
        Write-Host "    WARNING: Missing logo $logo - MSIX may need valid assets"
    }
}

# 4. Escribir AppxManifest.xml
$manifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">

  <Identity
    Name="ControlParental.App"
    Publisher="CN=ControlParentalDev"
    Version="1.0.0.0"
    ProcessorArchitecture="x64" />

  <Properties>
    <DisplayName>Control Parental</DisplayName>
    <PublisherDisplayName>ControlParental Dev</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
    <Description>Aplicacion de control parental para Windows (Development Build)</Description>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>

  <Resources>
    <Resource Language="en-us" />
  </Resources>

  <Applications>
    <Application Id="App"
      Executable="ControlParental.App.UI.exe"
      EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="Control Parental"
        Description="Control parental para Windows"
        BackgroundColor="#0078D4"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" Square71x71Logo="Assets\Square71x71Logo.png" Square310x310Logo="Assets\Square310x310Logo.png">
          <uap:ShowNameOnTiles>
            <uap:ShowOn Tile="square150x150Logo" />
            <uap:ShowOn Tile="wide310x150Logo" />
            <uap:ShowOn Tile="square310x310Logo" />
          </uap:ShowNameOnTiles>
        </uap:DefaultTile>
      </uap:VisualElements>
    </Application>
  </Applications>

  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
    <Capability Name="internetClient" />
  </Capabilities>
</Package>
"@

$manifestContent | Out-File -FilePath "$stagingPath/AppxManifest.xml" -Encoding utf8

# 5. Crear MSIX
Write-Host "[4/4] Creating MSIX package..." -ForegroundColor Yellow
if (Test-Path $outputPath) { Remove-Item $outputPath -Force }
& $makeappx pack /d $stagingPath /p $outputPath /o

if ($LASTEXITCODE -ne 0) { throw "MSIX creation failed" }

$size = (Get-Item $outputPath).Length / 1MB
Write-Host ""
Write-Host "=== SUCCESS ===" -ForegroundColor Green
Write-Host "MSIX created: $outputPath ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
Write-Host ""
Write-Host "NOTE: This MSIX is unsigned (for development only)." -ForegroundColor Yellow
Write-Host "For production, sign with: signtool sign /fd SHA256 /a /f <cert.pfx> /p <password> $outputPath" -ForegroundColor Yellow
