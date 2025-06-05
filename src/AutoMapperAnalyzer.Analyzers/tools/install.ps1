param($installPath, $toolsPath, $package, $project)

Write-Host "Installing AutoMapper Roslyn Analyzer..."

# Get the analyzer assembly
$analyzersPath = join-path $toolsPath "analyzers"
$analyzerFilePath = join-path $analyzersPath "AutoMapperAnalyzer.Analyzers.dll"

# Add analyzer to the project if it exists
if (Test-Path $analyzerFilePath) {
    $project.Object.AnalyzerReferences.Add($analyzerFilePath)
    Write-Host "AutoMapper Analyzer installed successfully!" -ForegroundColor Green
    Write-Host "The analyzer will now detect AutoMapper configuration issues at compile-time." -ForegroundColor Yellow
} else {
    Write-Warning "Analyzer assembly not found at: $analyzerFilePath"
}

Write-Host ""
Write-Host "🔍 AutoMapper Analyzer Features:" -ForegroundColor Cyan
Write-Host "  • Type safety validation (AM001-AM003)" 
Write-Host "  • Missing property detection (AM010-AM012)"
Write-Host "  • Configuration validation (AM040-AM042)"
Write-Host "  • Performance best practices (AM050-AM052)"
Write-Host ""
Write-Host "For more information, visit: https://github.com/georgepwall1991/automapper-analyser" -ForegroundColor Blue 