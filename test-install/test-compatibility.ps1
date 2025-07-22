# AutoMapper Analyzer Multi-Framework Compatibility Test
# Tests analyzer compatibility across .NET Framework, .NET Core, and .NET 5+

param(
    [switch]$SkipBuild = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Continue"

Write-Host "🔍 AutoMapper Analyzer - Multi-Framework Compatibility Test" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green

# First build the analyzer
if (-not $SkipBuild) {
    Write-Host "`n📦 Building AutoMapper Analyzer..." -ForegroundColor Yellow
    $buildResult = dotnet build ../src/AutoMapperAnalyzer.Analyzers/AutoMapperAnalyzer.Analyzers.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to build analyzer. Exiting." -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Analyzer built successfully" -ForegroundColor Green
}

# Define test frameworks
$testFrameworks = @(
    @{
        Name = ".NET Framework 4.8"
        Path = "NetFrameworkTest"
        Framework = "net48"
        Description = "Legacy .NET Framework support"
    },
    @{
        Name = ".NET Core 3.1"
        Path = "NetCoreTest"
        Framework = "netcoreapp3.1"
        Description = "LTS .NET Core version"
    },
    @{
        Name = ".NET 9.0"
        Path = "TestPackage"
        Framework = "net9.0"
        Description = "Latest .NET version"
    }
)

$results = @()

foreach ($test in $testFrameworks) {
    Write-Host "`n🧪 Testing: $($test.Name) ($($test.Description))" -ForegroundColor Cyan
    Write-Host "   Framework: $($test.Framework)" -ForegroundColor Gray
    Write-Host "   Path: $($test.Path)" -ForegroundColor Gray

    Push-Location $test.Path

    try {
        # Test restore
        Write-Host "   📥 Restoring packages..." -ForegroundColor Yellow
        $restoreOutput = dotnet restore 2>&1
        $restoreSuccess = $LASTEXITCODE -eq 0

        if ($restoreSuccess) {
            Write-Host "   ✅ Restore successful" -ForegroundColor Green
        } else {
            Write-Host "   ❌ Restore failed" -ForegroundColor Red
            if ($Verbose) {
                Write-Host "   Output: $restoreOutput" -ForegroundColor Gray
            }
        }

        # Test build (expect warnings/errors from analyzer)
        Write-Host "   🔨 Building project..." -ForegroundColor Yellow
        $buildOutput = dotnet build --no-restore --verbosity minimal 2>&1
        $buildExitCode = $LASTEXITCODE

        # Check for analyzer warnings/errors
        $hasAM001 = $buildOutput -match "AM001"
        $hasAM004 = $buildOutput -match "AM004"
        $analyzerWorking = $hasAM001 -or $hasAM004

        $result = @{
            Framework = $test.Name
            Target = $test.Framework
            RestoreSuccess = $restoreSuccess
            BuildExitCode = $buildExitCode
            AnalyzerWorking = $analyzerWorking
            HasAM001 = $hasAM001
            HasAM004 = $hasAM004
            Output = $buildOutput
        }

        if ($analyzerWorking) {
            Write-Host "   ✅ Analyzer working correctly" -ForegroundColor Green
            if ($hasAM001) { Write-Host "      • AM001 detected (Type Mismatch)" -ForegroundColor Cyan }
            if ($hasAM004) { Write-Host "      • AM004 detected (Missing Property)" -ForegroundColor Cyan }
        } else {
            Write-Host "   ⚠️  Analyzer not detecting expected issues" -ForegroundColor Yellow
        }

        # Check if build failed due to analyzer errors (expected behavior)
        if ($buildExitCode -ne 0 -and $analyzerWorking) {
            Write-Host "   ✅ Build failed due to analyzer errors (expected)" -ForegroundColor Green
        } elseif ($buildExitCode -eq 0 -and -not $analyzerWorking) {
            Write-Host "   ⚠️  Build succeeded but analyzer not working" -ForegroundColor Yellow
        }

        $results += $result

    } catch {
        Write-Host "   ❌ Exception occurred: $($_.Exception.Message)" -ForegroundColor Red
        $results += @{
            Framework = $test.Name
            Target = $test.Framework
            RestoreSuccess = $false
            BuildExitCode = -1
            AnalyzerWorking = $false
            HasAM001 = $false
            HasAM004 = $false
            Output = $_.Exception.Message
        }
    } finally {
        Pop-Location
    }
}

# Summary report
Write-Host "`n📊 COMPATIBILITY TEST SUMMARY" -ForegroundColor Green
Write-Host "==============================" -ForegroundColor Green

$successCount = 0
$totalCount = $results.Count

foreach ($result in $results) {
    $status = if ($result.RestoreSuccess -and $result.AnalyzerWorking) { "✅ PASS" } else { "❌ FAIL" }
    $isWorking = $result.RestoreSuccess -and $result.AnalyzerWorking
    if ($isWorking) { $successCount++ }

    Write-Host "$status $($result.Framework) ($($result.Target))" -ForegroundColor $(if ($isWorking) { "Green" } else { "Red" })
    Write-Host "      Restore: $(if ($result.RestoreSuccess) { "✅" } else { "❌" })" -ForegroundColor Gray
    Write-Host "      Analyzer: $(if ($result.AnalyzerWorking) { "✅" } else { "❌" })" -ForegroundColor Gray
    Write-Host "      AM001: $(if ($result.HasAM001) { "✅" } else { "❌" })" -ForegroundColor Gray
    Write-Host "      AM004: $(if ($result.HasAM004) { "✅" } else { "❌" })" -ForegroundColor Gray
}

Write-Host "`n📈 RESULTS: $successCount/$totalCount frameworks compatible" -ForegroundColor $(if ($successCount -eq $totalCount) { "Green" } else { "Yellow" })

if ($successCount -eq $totalCount) {
    Write-Host "🎉 All frameworks are fully compatible!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "⚠️  Some frameworks have compatibility issues" -ForegroundColor Yellow
    exit 1
}
