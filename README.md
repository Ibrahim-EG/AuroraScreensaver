# Aurora Screensaver

A procedural aurora night-sky screensaver: animated curtains of light over a
star field, a moon, a mountain ridge and a rippling water reflection. The
original was an Android Daydream; this repo now contains both versions.

## Windows 10 version (`windows/AuroraScreensaver.Win`) — recommended

Rebuilt for Windows 10/11 machines with Direct3D 11 (via SharpDX):

- **Looks better than before**: the scene renders into a half-resolution HDR
  (R16G16B16A16F) buffer, then a composite pass applies gamma + dithering and
  bilinear upscaling — soft, filmic gradients with no banding on dark skies,
  and ~4x cheaper on integrated GPUs (typical office Windows 10 laptops).
- **Native HUD**: clock + battery/date drawn with Direct2D/DirectWrite in
  Segoe UI Light, matching the Windows 10 look.
- **Power friendly**: frame loop is capped at 60 fps; vsync via flip-discard
  swap chain.
- Works down to Feature Level 10_0 drivers and degrades gracefully if the GPU
  is reset while running.

### Build & run (on a Windows 10 machine)

Requires the .NET SDK (8.0+) and the .NET Framework 4.8 targeting pack
(included with Visual Studio 2022 "Windows Forms" workload, or installable
standalone).

```powershell
cd windows\AuroraScreensaver.Win
dotnet build -c Release
bin\Release\net48\AuroraScreensaver.exe      # fullscreen preview; Esc or click exits
```

Optional: precompile the shaders so no runtime compilation is needed
(uses `fxc.exe` from the Windows SDK):

```powershell
.\build-shaders.ps1
```

### Install as the actual screensaver

Copy the whole `net48` output folder contents to `%SystemRoot%\System32`,
renaming `AuroraScreensaver.exe` to `AuroraScreensaver.scr`. Then pick
"AuroraScreensaver" under Settings -> Personalization -> Lock screen ->
Screen saver. Windows launches it with `/s`; `/c` shows a stub config dialog.

## Android version (`app/`)

Unchanged GLES 3.0 Daydream app. Build with Gradle:

```bash
gradle assembleDebug
```

CI builds both: `.github/workflows/android-build.yml` (APK artifact) and
`.github/workflows/windows-build.yml` (zip artifact containing the .scr).
