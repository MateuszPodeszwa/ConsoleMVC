# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ConsoleMVC is a C# console application framework that applies the ASP.NET Core MVC Controller-ViewModel pattern to console apps. The goal is to bring structured, maintainable architecture (models, controllers, views per "page") to console applications, rather than relying on ad-hoc UI libraries like Spectre.Console.
Although with this ConsoleMVC concept it is still possible to use Spectre Console library purely for tables, and user-based UI.

This is an early-stage concept.

## Intended Project Structure and Behaviour

This is a replica and adaptaption of much more popular ASP.NET Core MVC idea but designed for console application. 
It is meant to be something reusable, just like a template. For example, when developer inteeds to start a new MVC application,
they don't do it from scratch, they select create new project in their's IDE, and the core/base MVC implemendation is created automatically - I
inteed to do exactly the same thing.

I want people to use it and share it.

This must work exactly as Controller-ViewModel pattern there must be Models, Controllers, Views folder just as in ASP.NET Core MVC. These needs to be automatically handled by
the buider/project so for example, by adding a page/view a new console view is formed which I can navigate to, without implementing any interfaces on that page, controllers must
inherit the controller and viewmodels are DTOs that specific page must accept, if you need more guidance just look up at the ASP.NET Core MVC.

## Build & Run

```bash
# Build
dotnet build ConsoleMVC.sln

# Run
dotnet run --project ConsoleMVC.csproj

# Build release
dotnet build ConsoleMVC.sln -c Release
```

## Comminiting to GIT on changes

Before you begin with creating, the GIT repository must be created with corresponding name, this should be a public repository.
On every change a new GIT Commit must be performed, this includes even smaller changes such as refactoring, adding new classes etc.. 
Git commits should have professional structure containing short title briefly sumamrising changes, and description with more details, max 4 lines.
The commits must be signed as the Author
 - Author's Name: "Mateusz Podeszwa"
 - Author's Email: "podinatubie@gmail.com"
Never do GIT Pushes unless specifically asked for.

## Other Project Infrormation

Make sure that .gitignore is well-equipped for the project goal.
You can install any package or library you deem to be necesasry and vital for the project to fulfill its goal. You can install any extension you seem to be necessary for the project.

## Tech Stack

- .NET 10.0 (target framework `net10.0`)
- C# with nullable reference types and implicit usings enabled
- Single-project solution: `ConsoleMVC.csproj`
