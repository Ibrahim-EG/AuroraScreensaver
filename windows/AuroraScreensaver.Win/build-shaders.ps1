# Compiles Shaders/aurora.hlsl into .cso blobs using the Windows SDK fxc.exe.
# Optional: dotnet build works without this (the app falls back to runtime
# compilation via SharpDX.D3DCompiler). Run on a Windows 10 machine with:
#   powershell -ExecutionPolicy Bypass -File .\build-shaders.ps1
$ErrorActionPreference = "Stop"

$shaderDir = Join-Path $PSScriptRoot "Shaders"
$hlsl = Join-Path $shaderDir "aurora.hlsl"

# Locate fxc.exe from the newest installed Windows 10/11 SDK.
$fxc = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\fxc.exe" -ErrorAction SilentlyContinue |
       Sort-Object FullName -Descending | Select-Object -First 1

if ($null -eq $fxc) {
    Write-Warning "fxc.exe not found in the Windows SDK. The app will compile shaders at runtime instead."
    exit 0
}

Write-Host "Using $($fxc.FullName)"

Push-Location $shaderDir
try {
    & $fxc.FullName /nologo /T vs_5_0 /E VSFullscreen   /Fo aurora_fullscreen_vs.cso $hlsl
    if ($LASTEXITCODE -ne 0) { throw "VSFullscreen failed" }

    & $fxc.FullName /nologo /T ps_5_0 /E PSMain         /Fo aurora_scene_ps.cso      $hlsl
    if ($LASTEXITCODE -ne 0) { throw "PSMain failed" }

    & $fxc.FullName /nologo /T ps_5_0 /E PSComposite    /Fo aurora_composite_ps.cso  $hlsl
    if ($LASTEXITCODE -ne 0) { throw "PSComposite failed" }
}
finally {
    Pop-Location
}

Write-Host "Shader blobs written to $shaderDir"
