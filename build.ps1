param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    throw "csc.exe not found. Install the .NET Framework 4.x developer pack."
}

$icon = Join-Path $root "assets\icon.ico"
if (-not (Test-Path $icon)) {
    & "$root\assets\generate-icon.ps1"
}

$exe = Join-Path $root "src\ThemeToggle.exe"
& $csc /nologo /target:winexe /optimize+ /win32icon:$icon /out:$exe "$root\src\ThemeToggle.cs"
if ($LASTEXITCODE -ne 0) { throw "App compile failed." }
Write-Host "Built $exe"

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 7\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host "Inno Setup not found. Skipping installer."
    return
}

New-Item -ItemType Directory -Path "$root\dist" -Force | Out-Null
& $iscc "/DMyAppVersion=$Version" "$root\installer\setup.iss"
if ($LASTEXITCODE -ne 0) { throw "Installer compile failed." }
Write-Host "Built $root\dist\ThemeToggle-Setup.exe"
