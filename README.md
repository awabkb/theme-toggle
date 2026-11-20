# Theme Toggle

**Switch Windows between dark and light mode in one click, or with `Win+Shift+D`.**

A 13 KB tray app for Windows 10 and 11. No .NET to install, no settings window, no admin rights, nothing running except one tray icon.

<!-- TODO: add a demo GIF here: assets/demo.gif, a click on the tray icon flipping the desktop -->

[![Download](https://img.shields.io/github/v/release/awabkb/theme-toggle?label=download)](https://github.com/awabkb/theme-toggle/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

## Features

- **One click.** Left-click the sun/moon icon to flip apps and the taskbar between dark and light.
- **Global shortcut.** Press `Win+Shift+D` from anywhere.
- **Always in sync.** If you change the theme in Settings or with another tool, the icon updates too.
- **Sharp at any scale.** The icon is drawn at your display's real size (100%, 150%, 200%…).
- **Tiny.** A single 13 KB exe built on the .NET Framework that ships with Windows.

## Install

Download from [Releases](https://github.com/awabkb/theme-toggle/releases/latest):

- **`ThemeToggle-Setup.exe`**: installs to `%LOCALAPPDATA%\ThemeToggle`, adds a Start Menu shortcut, and starts at sign-in. No admin needed. Uninstall from Settings → Apps.
- **`ThemeToggle.exe`**: the portable version. Run it from anywhere.

> Windows SmartScreen may warn you because the exe isn't code-signed yet. Click **More info → Run anyway**, or build it yourself from source (below).

## Usage

| Action | What it does |
| --- | --- |
| Left-click the icon | Switch theme |
| `Win+Shift+D` | Switch theme |
| Right-click → Exit | Quit |

If you don't see the icon, click the **^** arrow next to the clock and drag it onto the taskbar.

If another app already uses `Win+Shift+D`, Theme Toggle tells you on startup and keeps working with clicks.

## Why not Auto Dark Mode?

[Auto Dark Mode](https://github.com/AutoDarkMode/Windows-Auto-Night-Mode) is great if you want schedules, wallpaper switching and lots of options. Theme Toggle is for people who just want a switch: one tiny exe and nothing to configure.

## Build from source

You need Windows 10/11 and `csc.exe`, which comes with the .NET Framework 4.x. [Inno Setup](https://jrsoftware.org/isinfo.php) is only needed for the installer.

```powershell
.\build.ps1
```

This produces `src\ThemeToggle.exe`. If Inno Setup is installed, it also produces `dist\ThemeToggle-Setup.exe`.

## How it works

It sets `AppsUseLightTheme` and `SystemUsesLightTheme` under `HKCU\...\Themes\Personalize` and then tells other windows the theme changed (`WM_SETTINGCHANGE` with `ImmersiveColorSet`). That's the same thing the Settings app does. All the code is in one file: [`src/ThemeToggle.cs`](src/ThemeToggle.cs).

## License

[MIT](LICENSE)
