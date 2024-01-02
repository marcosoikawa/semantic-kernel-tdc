Param(
    [string]$ProjectName = ""
)

# Generate a timestamp for the current date and time
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

# Define paths
$scriptPath = Get-Item -Path $PSScriptRoot
$coverageOutputPath = Join-Path $scriptPath "TestResults\Coverage\$timestamp"
$reportOutputPath = Join-Path $scriptPath "TestResults\Reports\$timestamp"

# Create output directories
New-Item -ItemType Directory -Force -Path $coverageOutputPath
New-Item -ItemType Directory -Force -Path $reportOutputPath

# Run build
dotnet build

# Find and run tests for projects ending with 'UnitTests.csproj'
$testProjects = Get-ChildItem $scriptPath -Filter "*UnitTests.csproj" -Recurse

if ($ProjectName -ne "") {
    $testProjects = $testProjects | Where-Object { $_.Name -like "*$ProjectName*" }
}

foreach ($project in $testProjects) {
    $testProjectPath = $project.FullName
    Write-Host "Running tests for project: $($testProjectPath)"
    
    dotnet test $testProjectPath `
        --collect:"XPlat Code Coverage" `
        --results-directory:$coverageOutputPath `
        --no-build `
        --no-restore `
}

# Install required tools
& dotnet tool install -g coverlet.console
& dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
& reportgenerator -reports:"$coverageOutputPath/**/coverage.cobertura.xml" -targetdir:$reportOutputPath -reporttypes:Html

Write-Host "Code coverage report generated at: $reportOutputPath"
