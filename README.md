# GPAK tools

[![Join the chat at https://gitter.im/klym1/Gpak-tools](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/klym1/Gpak-tools?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

*this project is still under development*

[![build](https://github.com/klym1/Gpak-tools/actions/workflows/build.yml/badge.svg)](https://github.com/klym1/Gpak-tools/actions/workflows/build.yml)

## Description

C# library for reading **GPAK** files (have *.gp* extension) which were utilized for storing graphical data in some old games (such as **Cossacks** and **American Conquest** from **GSC Game World** company). Library produces Bitmap objects which can be saved using ordinary image format (e.g. *PNG*).

Format specs were reverse engineered completely from scratch. There is still room for improvements since not all available GP files can be successfully recovered. 

## Goals

This project is rather for "prototyping" purposes then for real world usage. 

Main goal is to recover (or reverse engineer) image format specs and implement some sort of converter to common image format. And I'm happy, because this goal is almost achieved. 

Next goal is to improve code quality, improve performance and make usable for real world applications (maybe for HTML5 games).

## Dev/Run

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Everything works on Windows, Linux and macOS.

```
dotnet build src/GPExtractor.sln
dotnet test src/GPExtractor.sln
```

View all images of a `.gp` file (defaults to `gp/test.gp`):

```
dotnet run --project src/Viewer -- [gp-file]
```

Convert every `.gp` file in a folder to PNG:

```
dotnet run --project src/ConsoleBatchConverter -- <gp-folder> <png-output-folder> [palette-file]
```

The palette defaults to `palette/0/agew_1.pal`.

| Project | Purpose |
|---|---|
| `Types`, `GPExtractor` | GP file parsing |
| `ImageRenderer` | Rendering to an in-memory pixel buffer, built-in PNG encoder |
| `ConsoleBatchConverter` | Batch `.gp` → PNG conversion |
| `Viewer` | Desktop image viewer ([Avalonia](https://avaloniaui.net/)) |
| `UnitTests`, `ViewerTests` | Tests; `ViewerTests` run the viewer headlessly |

The core library and converter have no native or imaging dependencies. The tests include pixel-exact regression checks against the output of the original GDI+ renderer.

On Linux the viewer (and `ViewerTests`) need fontconfig (`libfontconfig1`), which desktop distributions already include.

NuGet dependencies (NUnit, FluentAssertions, Avalonia) are restored automatically on build.

## Examples

Samples:

![alt text](https://raw.githubusercontent.com/klym1/Gpak-tools/master/OutputExamples/Example.PNG "Building from Cossacks Game")
![alt text](https://raw.githubusercontent.com/klym1/Gpak-tools/master/OutputExamples/Example2.PNG "Getman unit")
