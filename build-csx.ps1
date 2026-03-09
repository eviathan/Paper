#!/usr/bin/env pwsh

# Paper CSX Compiler Build Script
# Compiles all .csx files to C# source files

$CSXCliPath = "Paper.CSX.Cli/bin/Debug/net10.0/Paper.CSX.Cli.exe"
$CSXFiles = Get-ChildItem -Path . -Recurse -Filter "*.csx" | Select-Object -ExpandProperty FullName

# Build the CSX CLI if it doesn't exist
if (-not (Test-Path $CSXCliPath)) {
    Write-Host "Building CSX CLI..." -ForegroundColor Yellow
    dotnet build Paper.CSX.Cli -c Debug | Out-Default
    if (-not (Test-Path $CSXCliPath)) {
        Write-Error "Failed to build CSX CLI"
        exit 1
    }
}

# Compile all CSX files
Write-Host "Compiling CSX files..." -ForegroundColor Yellow
$CSXFiles | ForEach-Object {
    Write-Host "Compiling: $_" -ForegroundColor Cyan
    $OutputPath = [System.IO.Path]::ChangeExtension($_, "generated.cs")
    dotnet run --project Paper.CSX.Cli -- parse $_
}

Write-Host "CSX compilation completed!" -ForegroundColor Green
