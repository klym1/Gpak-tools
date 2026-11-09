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

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Works on Windows, Linux and macOS.

```
dotnet build src/GPExtractor.sln
dotnet test src/GPExtractor.sln
```

Convert every `.gp` file in a folder to PNG (any OS):

```
dotnet run --project src/ConsoleBatchConverter -- <gp-folder> <png-output-folder> [palette-file]
```

The palette defaults to `palette/0/agew_1.pal`.

| Project | Platforms |
|---|---|
| `Types`, `GPExtractor` (parsing), `ImageRenderer` (rendering + PNG output), `ConsoleBatchConverter`, `UnitTests` | Windows, Linux, macOS |
| `WindowsFormsTestClient` (image viewer), `Shared` (GDI+ bitmap helpers) | Windows only (they still compile on other OSes) |

The core library has no native or imaging dependencies: images are rendered into an in-memory pixel buffer and written by a small built-in PNG encoder. The test suite includes a pixel-exact regression test against output of the original GDI+ renderer.

NuGet dependencies (NUnit, FluentAssertions, System.Drawing.Common for the Windows-only projects) are restored automatically on build.

## Examples

Samples:

![alt text](https://raw.githubusercontent.com/klym1/Gpak-tools/master/OutputExamples/Example.PNG "Building from Cossacks Game")
![alt text](https://raw.githubusercontent.com/klym1/Gpak-tools/master/OutputExamples/Example2.PNG "Getman unit")
