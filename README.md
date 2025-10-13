# Discord Screenshare Loopback Shutup

Stop Discord from looping its own audio into your stream.
This tiny Windows utility automatically mutes Discord on all output devices except the ones you allow, so your viewers
don’t hear Discord twice (or hear themselves) when you share audio.

<img width="414" height="336" alt="image" src="https://github.com/user-attachments/assets/191e29d4-82d9-4961-a379-0d38d82cb3c1" />

## Use case

E.g. If you use Voicemeeter, route discord audio to one of voicemeeter's virtual outputs, and inside voicemeeter route
it to several actual audio devices.  
Then, when you start streaming your desktop with audio, your watcher will hear themselves in your stream, since discord
thinks that your actual audio devices is part of your "desktop audio".

## What it does

- Watches your Windows audio outputs for the Discord process.
- Automatically mutes Discord on every output device except:
    - Your current Windows default output device, and
    - The output device you explicitly select in the app (your discord output device).
- Reacts instantly when you change the default device, plug/unplug devices, or when Discord opens a new audio session.

Typical use: you use a virtual cable/secondary device for screen share audio capture. This app prevents Discord’s own
output from being captured on the “share/capture” device, eliminating echo/loopback for your viewers.

## Installation

1. Download the app’s EXE from [releases page](https://github.com/SKProCH/DiscordScreenshareLoopbackShutup/releases) (or
   build from source; see below).
2. Run the EXE.
    - It self-installs to: `%LocalAppData%\DiscordScreenshareLoopbackShutup`
    - Creates a Start Menu shortcut: “Discord Screenshare Loopback Shutup”
    - Registers a current user autostart entry via Windows Task Scheduler
3. The window appears once to let you choose a device, then hides when you click elsewhere.

Notes

- If you run it again while it’s already running, the existing instance’s window will pop up (single‑instance).
- No tray icon: re-open the window by launching the shortcut or EXE again.

## Usage

1. Open the app (Start Menu → Discord Screenshare Loopback Shutup).
2. In the list, select the output device you want Discord to be allowed to use (typically the same device you set in
   Discord’s Voice & Video → Output Device).
3. That’s it. From now on, Discord will be muted on all other outputs; only the Windows default device and your selected
   device remain unmuted for Discord.

Legend

- Speaker icon = Valid output (Discord is allowed on this device)
- Mute icon = Discord is currently detected and muted on this device
- No icon = Discord not detected on this device at the moment

Changing devices

- Change your selection at any time: open the app again and pick a different device.
- Your choice is saved to `%LocalAppData%\DiscordScreenshareLoopbackShutup\config.toml` and applied automatically next
  time.
- If you change the Windows default output device, the app updates immediately.

## Uninstall / Disable autostart

- To stop autostart only:
    - Delete the scheduled task named: `Discord Screenshare Loopback Shutup` (Task Scheduler → Task Scheduler Library).
    - Or from a terminal (PowerShell/CMD):
        - `schtasks /Delete /TN "Discord Screenshare Loopback Shutup" /F`
- To fully uninstall:
    1. Close the app (end the “DiscordScreenshareLoopbackShutup” process from Task Manager, or sign out).
    2. Remove the folder: `%LocalAppData%\DiscordScreenshareLoopbackShutup`
    3. Remove the Start Menu shortcut if present:
       `%ProgramData%\Microsoft\Windows\Start Menu\Programs\Discord Screenshare Loopback Shutup.lnk`
    4. Delete the scheduled task (see above) if it still exists.

## Troubleshooting

- The window disappeared:
    - It hides when it loses focus. Launch the app again to bring it back.
- Reset settings:
    - Exit the app and delete `%LocalAppData%\DiscordScreenshareLoopbackShutup\config.toml`.

## How it works (technical overview)

- Uses NAudio CoreAudio to enumerate render endpoints and observe audio sessions.
- Subscribes to device add/remove events, default device changes, and session creation.
- When a Discord session appears on any output that is not your selected device and not the Windows default device, the
  app programmatically mutes that session only.
- Stores your selection in a small TOML config next to the app in LocalAppData.
- Single‑instance behavior with a tiny IPC signal to re‑show the window.
- Self‑install logic copies the EXE to LocalAppData, creates a Start Menu shortcut, and registers a per‑user logon
  scheduled task.

## Build from source

Prerequisites

- .NET SDK 9.0
- Windows 10/11 for running/testing

Build and publish (single‑file, self‑contained):

```cmd
dotnet publish
```

The published single‑file EXE will be under `DiscordScreenshareLoopbackShutup\bin\Release\net9.0\win-x64\publish`. Run
that EXE (not the Debug build). The app’s self‑installer expects a single‑file publish.
