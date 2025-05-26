# TorrentFlow

A cross-platform torrent client built with Avalonia UI and .NET 9, featuring a modern GUI interface for managing torrent downloads.

## Features

- Cross-platform support (Windows & Linux)
- Modern UI built with Avalonia
- Desktop notifications
- Single-file portable executable

## Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 or any compatible IDE (optional)

## Building from Source

### Quick Build
```bash
dotnet build -c Release
```

### Portable Single-File Executable

#### Windows (x64)
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false
```

#### Linux (x64)
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false
```

The compiled executable will be located in:
```
bin/Release/net9.0/{runtime}/publish/TorrentFlow.exe
```

## Dependencies

- **Avalonia 11.3.0** - Cross-platform UI framework
- **MonoTorrent 3.0.2** - Torrent protocol implementation
- **CommunityToolkit.Mvvm** - MVVM helpers
- **DesktopNotifications.Avalonia** - Desktop notification support
- **MessageBox.Avalonia** - Message dialog support

## Configuration

The application uses self-contained deployment, meaning no additional .NET runtime installation is required on target machines.
