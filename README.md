# Theme Toggle

Tray icon for Windows. Click it to switch dark mode and light mode.

## Install

Download `ThemeToggle-Setup.exe` from [Releases](https://github.com/awabkb/theme-toggle/releases).

It installs to `%LOCALAPPDATA%\ThemeToggle`, adds a Start Menu shortcut, and starts at sign-in. No admin required. Remove it from Settings → Apps.

## Usage

- Left click: switch theme
- Right click → Exit: quit

If you don't see the icon, check the hidden icons arrow next to the clock.

## Build

Windows 10/11 and `csc.exe` (comes with .NET Framework). [Inno Setup](https://jrsoftware.org/isinfo.php) is only needed if you want the installer.

```powershell
.\build.ps1
```

That produces `src\ThemeToggle.exe`. With Inno Setup installed it also produces `dist\ThemeToggle-Setup.exe`.

## License

MIT
