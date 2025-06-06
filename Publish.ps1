# AutoMapper Analyzer Auto-Publish Script (PowerShell)
# Automatically increments version and publishes to NuGet.org

param(
    [string]$ApiKey = $env:NUGET_API_KEY
)

$ErrorActionPreference = "Stop"

# Validate API key
if (-not $ApiKey) {
    Write-Host "❌ Error: NuGet API key not provided!" -ForegroundColor Red
    Write-Host "   Set environment variable: `$env:NUGET_API_KEY = 'your-api-key'" -ForegroundColor Yellow
    Write-Host "   Or pass as parameter: ./Publish.ps1 -ApiKey 'your-api-key'" -ForegroundColor Yellow
    exit 1
}

# Configuration
$ProjectPath = "src/AutoMapperAnalyzer.Analyzers/AutoMapperAnalyzer.Analyzers.csproj"
$OutputDir = "./nupkg"

# Generate version: 1.5.MMDD.HHMM (use month/day to stay under 65535)
$Major = 1
$Minor = 5  # Increment minor to indicate auto-versioning
$BuildNumber = [int](Get-Date -Format "MMdd")    # e.g., 0606 for June 6
$Revision = [int](Get-Date -Format "HHmm")       # e.g., 0941 for 09:41
$Version = "$Major.$Minor.$BuildNumber.$Revision"

Write-Host "🚀 AutoMapper Analyzer Auto-Publish" -ForegroundColor Green
Write-Host "📦 Building version: $Version" -ForegroundColor Yellow

# Clean previous builds
Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Cyan
dotnet clean $ProjectPath --configuration Release

# Build with auto-generated version
Write-Host "🔨 Building package..." -ForegroundColor Cyan
dotnet pack $ProjectPath `
  --configuration Release `
  --output $OutputDir `
  /p:PackageVersion=$Version `
  /p:AssemblyVersion=$Version `
  /p:FileVersion=$Version

# Check if package was created
$PackageFile = "$OutputDir/AutoMapperAnalyzer.Analyzers.$Version.nupkg"
if (-not (Test-Path $PackageFile)) {
    Write-Host "❌ Package file not found: $PackageFile" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Package created: $PackageFile" -ForegroundColor Green

# Get package size
$PackageSize = [math]::Round((Get-Item $PackageFile).Length / 1KB, 1)
Write-Host "📊 Package size: $PackageSize KB" -ForegroundColor Blue

# Publish to NuGet.org
Write-Host "📤 Publishing to NuGet.org..." -ForegroundColor Cyan
try {
    dotnet nuget push $PackageFile `
      --source https://api.nuget.org/v3/index.json `
      --api-key $ApiKey
    
    Write-Host "🎉 Successfully published AutoMapperAnalyzer.Analyzers $Version to NuGet.org!" -ForegroundColor Green
    Write-Host "🔗 Package URL: https://www.nuget.org/packages/AutoMapperAnalyzer.Analyzers/$Version" -ForegroundColor Blue
}
catch {
    Write-Host "❌ Failed to publish package: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Clean up old packages (keep last 3)
Write-Host "🧹 Cleaning up old packages..." -ForegroundColor Cyan
$OldPackages = Get-ChildItem "$OutputDir/AutoMapperAnalyzer.Analyzers.*.nupkg" | 
               Sort-Object LastWriteTime -Descending | 
               Select-Object -Skip 3

if ($OldPackages) {
    $OldPackages | Remove-Item -Force
    Write-Host "🗑️ Removed $($OldPackages.Count) old package(s)" -ForegroundColor Yellow
}

Write-Host "✨ Auto-publish complete!" -ForegroundColor Green 