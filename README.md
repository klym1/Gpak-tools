# GPAK tools

[![Join the chat at https://gitter.im/klym1/Gpak-tools](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/klym1/Gpak-tools?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

*this project is still under development*

[![Build Status](https://travis-ci.org/klym1/Gpak-tools.svg)](https://travis-ci.org/klym1/Gpak-tools)

## Description

C# library for reading **GPAK** files (have *.gp* extension) which were utilized for storing graphical data in some old games (such as **Cossacks** and **American Conquest** from **GSC Game World** company). Library produces Bitmap objects which can be saved using ordinary image format (e.g. *PNG*).

Format specs were reverse engineered completely from scratch. There is still room for improvements since not all available GP files can be successfully recovered. 

## Goals

This project is rather for "prototyping" purposes then for real world usage. 

Main goal is to recover (or reverse engineer) image format specs and implement some sort of converter to common image format. And I'm happy, because this goal is almost achieved. 

Next goal is to improve code quality, improve performance and make usable for real world applications (maybe for HTML5 games).

## Dev/Run

Since it's still in development it should be run from Visual Studio (>= 2012). You need .NET 4.0 (Windows 7 or later) or Mono 4.0 (i suppose older version of mono might be working too but haven't tested yet) to build the project. All dependencies should be successfully restored by NuGet on first build (in fact only **UnitTests** project uses external dependencies: NUnit and Fluent Assertions).

## Examples

Samples:

![alt text](https://raw.githubusercontent.com/klym1/Gpak-tools/master/OutputExamples/Example.PNG "Building from Cossacks Game")
![alt text](https://raw.githubusercontent.com/klym1/Gpak-tools/master/OutputExamples/Example2.PNG "Getman unit")
