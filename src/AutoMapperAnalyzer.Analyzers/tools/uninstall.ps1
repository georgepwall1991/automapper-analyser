param($installPath, $toolsPath, $package, $project)

Write-Host "Uninstalling AutoMapper Roslyn Analyzer..."

# Get the analyzer assembly path
$analyzersPath = join-path $toolsPath "analyzers"
$analyzerFilePath = join-path $analyzersPath "AutoMapperAnalyzer.Analyzers.dll"

# Remove analyzer from the project
try {
    $project.Object.AnalyzerReferences.Remove($analyzerFilePath)
    Write-Host "AutoMapper Analyzer uninstalled successfully!" -ForegroundColor Green
} catch {
    Write-Warning "Could not remove analyzer reference: $_"
}

Write-Host "AutoMapper configuration issues will no longer be detected at compile-time." -ForegroundColor Yellow
Write-Host "Thank you for using AutoMapper Roslyn Analyzer!" -ForegroundColor Blue 