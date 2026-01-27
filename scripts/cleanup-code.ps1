#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Code cleanup and quality check script for ERP System

.DESCRIPTION
    This script performs the following actions:
    1. Formats code using dotnet format
    2. Removes unused usings
    3. Builds the solution
    4. Generates a warning report

.EXAMPLE
    .\scripts\cleanup-code.ps1
#>

param(
    [switch]$SkipBuild,
    [switch]$FixOnly,
    [string]$Project = ""
)

$ErrorActionPreference = "Continue"
$WarningPreference = "Continue"

# Colors
$ColorCyan = "Cyan"
$ColorYellow = "Yellow"
$ColorGreen = "Green"
$ColorRed = "Red"

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor $ColorCyan
Write-Host "║          ERP System - Code Cleanup & Quality Check        ║" -ForegroundColor $ColorCyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor $ColorCyan
Write-Host ""

# Step 1: Format code
Write-Host "📝 Step 1: Formatting code..." -ForegroundColor $ColorYellow
Write-Host "   Running dotnet format..." -ForegroundColor Gray

if ($Project) {
    dotnet format $Project --verbosity diagnostic
} else {
    dotnet format --verbosity diagnostic
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ Code formatting complete" -ForegroundColor $ColorGreen
} else {
    Write-Host "   ⚠️  Code formatting completed with warnings" -ForegroundColor $ColorYellow
}

Write-Host ""

# Step 2: Remove unused usings
Write-Host "🗑️  Step 2: Removing unused usings..." -ForegroundColor $ColorYellow

if ($Project) {
    dotnet format $Project analyzers --severity info
} else {
    dotnet format analyzers --severity info
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ Unused usings removed" -ForegroundColor $ColorGreen
} else {
    Write-Host "   ⚠️  Some usings could not be removed" -ForegroundColor $ColorYellow
}

Write-Host ""

if ($FixOnly) {
    Write-Host "✅ Fix-only mode complete. Skipping build." -ForegroundColor $ColorGreen
    exit 0
}

# Step 3: Build solution
if (-not $SkipBuild) {
    Write-Host "🔨 Step 3: Building solution..." -ForegroundColor $ColorYellow
    
    if ($Project) {
        dotnet build $Project --no-incremental /p:TreatWarningsAsErrors=false
    } else {
        dotnet build --no-incremental /p:TreatWarningsAsErrors=false
    }
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Build successful" -ForegroundColor $ColorGreen
    } else {
        Write-Host "   ❌ Build failed" -ForegroundColor $ColorRed
        exit 1
    }
    
    Write-Host ""
}

# Step 4: Generate warning report
Write-Host "📊 Step 4: Generating warning report..." -ForegroundColor $ColorYellow

$reportPath = "build-warnings-report.txt"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

if ($Project) {
    dotnet build $Project --no-incremental /p:TreatWarningsAsErrors=false > $reportPath 2>&1
} else {
    dotnet build --no-incremental /p:TreatWarningsAsErrors=false > $reportPath 2>&1
}

# Parse warnings
$warnings = Get-Content $reportPath | Select-String "warning CS"
$warningCount = ($warnings | Measure-Object).Count

# Parse errors
$errors = Get-Content $reportPath | Select-String "error CS"
$errorCount = ($errors | Measure-Object).Count

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor $ColorCyan
Write-Host "║                    Quality Report                          ║" -ForegroundColor $ColorCyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor $ColorCyan
Write-Host ""
Write-Host "   Timestamp: $timestamp" -ForegroundColor Gray
Write-Host "   Report saved to: $reportPath" -ForegroundColor Gray
Write-Host ""

if ($errorCount -gt 0) {
    Write-Host "   ❌ Errors: $errorCount" -ForegroundColor $ColorRed
} else {
    Write-Host "   ✅ Errors: $errorCount" -ForegroundColor $ColorGreen
}

if ($warningCount -gt 50) {
    Write-Host "   ⚠️  Warnings: $warningCount" -ForegroundColor $ColorRed
} elseif ($warningCount -gt 10) {
    Write-Host "   ⚠️  Warnings: $warningCount" -ForegroundColor $ColorYellow
} else {
    Write-Host "   ✅ Warnings: $warningCount" -ForegroundColor $ColorGreen
}

Write-Host ""

# Show top warning types
Write-Host "   Top Warning Types:" -ForegroundColor $ColorYellow
$warningTypes = $warnings | ForEach-Object {
    if ($_ -match "warning (CS\d+):") {
        $matches[1]
    }
} | Group-Object | Sort-Object Count -Descending | Select-Object -First 5

foreach ($type in $warningTypes) {
    Write-Host "      $($type.Name): $($type.Count)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor $ColorCyan
Write-Host "║                    Cleanup Complete!                       ║" -ForegroundColor $ColorCyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor $ColorCyan
Write-Host ""

if ($errorCount -eq 0 -and $warningCount -lt 10) {
    Write-Host "🎉 Excellent! Your code quality is great!" -ForegroundColor $ColorGreen
} elseif ($errorCount -eq 0 -and $warningCount -lt 50) {
    Write-Host "👍 Good job! Consider fixing remaining warnings." -ForegroundColor $ColorYellow
} elseif ($errorCount -eq 0) {
    Write-Host "⚠️  Build successful but many warnings remain. Please review." -ForegroundColor $ColorYellow
} else {
    Write-Host "❌ Build has errors. Please fix before committing." -ForegroundColor $ColorRed
    exit 1
}

Write-Host ""

