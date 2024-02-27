# Leap

[![nuget](https://feeds.dev.azure.com/gsoft/_apis/public/Packaging/Feeds/gsoft/Packages/7e26c0cd-3179-49c3-a3a7-e92df061eee5/Badge)](https://dev.azure.com/gsoft/Shared-Assets/_artifacts/feed/gsoft/NuGet/Workleap.Leap)
[![build](https://dev.azure.com/gsoft/Shared-Assets/_apis/build/status%2FLeap%2FLeap%20Publish?branchName=main)](https://dev.azure.com/gsoft/Shared-Assets/_build/latest?definitionId=219&branchName=main)

## Introduction

Leap, which stands for _Local Environment Application Proxy_, is a custom made command-line which streamline our local development process by introducing service discovery, better dependency management, efficient resource allocation and a standardized configuration.

## Getting Started

You first need to install the Leap CLI. This can be done by running the following command in a terminal:

```powershell
dotnet tool update Workleap.Leap --global --interactive --add-source "https://pkgs.dev.azure.com/gsoft/_packaging/gsoft/nuget/v3/index.json" --verbosity minimal --no-cache
```

# Build and Test

The project can be built by running `Build.ps1`, which will produce a NuGet package in the `.output` folder.

We use [GitVersion](https://gitversion.net/) to determine the version number of the NuGet package. This means that the version number is automatically determined based on the commit history and tags.

Preview packages are published on the main branch as well as on pull requests. Stable builds can be published by creating a tag with the format `x.y.z` (e.g. `1.0.0`).

Run `Install.ps1` to install the package locally from your current working directory.
