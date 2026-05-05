param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "0.1.0",
    [string]$HostPublishDir = "",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

function Assert-WorkspacePath {
    param([string]$Path, [string]$WorkspaceRoot)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullRoot = [System.IO.Path]::GetFullPath($WorkspaceRoot)

    if (-not $fullPath.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside the workspace: $fullPath"
    }

    return $fullPath
}

function Reset-Directory {
    param([string]$Path, [string]$WorkspaceRoot)

    $fullPath = Assert-WorkspacePath -Path $Path -WorkspaceRoot $WorkspaceRoot

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $fullPath | Out-Null
}

function Invoke-DotNet {
    param([string[]]$Arguments)

    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed: dotnet $($Arguments -join ' ')"
    }
}

$repoRoot = Assert-WorkspacePath -Path (Join-Path $PSScriptRoot "..") -WorkspaceRoot (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $repoRoot "artifacts"
$appRoot = Join-Path $artifactsRoot "installer\app"
$hostOutput = Join-Path $appRoot "host"
$trayTempOutput = Join-Path $artifactsRoot "installer\tray-temp"
$installerOutput = Join-Path $artifactsRoot "installer\output"
$installerScript = Join-Path $repoRoot "installer\LocalScanAgent.iss"

Reset-Directory -Path $appRoot -WorkspaceRoot $repoRoot
Reset-Directory -Path $trayTempOutput -WorkspaceRoot $repoRoot
Reset-Directory -Path $installerOutput -WorkspaceRoot $repoRoot

if ([string]::IsNullOrWhiteSpace($HostPublishDir)) {
    Write-Host "Publishing host..." -ForegroundColor Cyan

    try {
        Invoke-DotNet @(
            "publish",
            (Join-Path $repoRoot "src\LocalScanAgent.Host\LocalScanAgent.Host.csproj"),
            "-c", $Configuration,
            "-r", $Runtime,
            "--self-contained", "true",
            "-p:PublishSingleFile=false",
            "-p:Version=$Version",
            "-o", $hostOutput
        )
    }
    catch {
        throw "Host publish failed. If your local SDK still hits MSB4276, rerun with -HostPublishDir .\publish\LocalScanAgent-win-x64. Original error: $($_.Exception.Message)"
    }
}
else {
    $resolvedHostPublishDir = Assert-WorkspacePath -Path $HostPublishDir -WorkspaceRoot $repoRoot

    if (-not (Test-Path -LiteralPath (Join-Path $resolvedHostPublishDir "LocalScanAgent.Host.exe"))) {
        throw "The provided -HostPublishDir does not contain LocalScanAgent.Host.exe: $resolvedHostPublishDir"
    }

    Write-Host "Copying prebuilt host from $resolvedHostPublishDir..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $hostOutput | Out-Null
    Get-ChildItem -LiteralPath $resolvedHostPublishDir -Force |
        Where-Object { $_.Name -ne "logs" } |
        Copy-Item -Destination $hostOutput -Recurse -Force
}

$hostLogsDir = Join-Path $hostOutput "logs"
New-Item -ItemType Directory -Path $hostLogsDir | Out-Null

Write-Host "Publishing tray..." -ForegroundColor Cyan
Invoke-DotNet @(
    "publish",
    (Join-Path $repoRoot "src\LocalScanAgent.Tray\LocalScanAgent.Tray.csproj"),
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:Version=$Version",
    "-o", $trayTempOutput
)

Copy-Item -Path (Join-Path $trayTempOutput "*") -Destination $appRoot -Recurse -Force
Copy-Item -Path (Join-Path $repoRoot "README.md") -Destination (Join-Path $appRoot "README.md")

if ($SkipInstaller) {
    Write-Host ""
    Write-Host "Installer build skipped. Staged files are available in: $appRoot" -ForegroundColor Yellow
    exit 0
}

$isccCommand = Get-Command "iscc" -ErrorAction SilentlyContinue
$isccPath = $isccCommand.Source

if (-not $isccPath) {
    $commonIsccPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    $isccPath = $commonIsccPaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $isccPath) {
    Write-Host ""
    Write-Warning "Inno Setup Compiler (iscc.exe) not found. Install Inno Setup, then rerun this script."
    Write-Host "Staged files are available in: $appRoot" -ForegroundColor Yellow
    exit 0
}

Write-Host "Building installer..." -ForegroundColor Cyan
& $isccPath `
    "/DAppVersion=$Version" `
    "/DSourceDir=$appRoot" `
    "/DOutputDir=$installerOutput" `
    $installerScript

Write-Host ""
Write-Host "Installer generated in: $installerOutput" -ForegroundColor Green
