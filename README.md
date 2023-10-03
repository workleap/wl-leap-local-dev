# LEAP

[![nuget](https://feeds.dev.azure.com/gsoft/_apis/public/Packaging/Feeds/gsoft/Packages/TODO/Badge)](https://dev.azure.com/gsoft/Shared-Assets/_artifacts/feed/gsoft/NuGet/Workleap.Leap)
[![build](https://dev.azure.com/gsoft/Shared-Assets/_apis/build/status%2FLeap%2FLeap%20Publish?branchName=main)](https://dev.azure.com/gsoft/Shared-Assets/_build/latest?definitionId=219&branchName=main)

## Introduction

LEAP, which stands for _Local Environment Application Proxy_, is a custom made command-line which streamline our local development process by introducing service discovery, better dependency management, efficient resource allocation and a standardized configuration. 

## Getting Started

You first need to install the LEAP CLI. This can be done by running the following command in a terminal:

```powershell
dotnet tool update Workleap.Leap --global --interactive --add-source "https://pkgs.dev.azure.com/gsoft/_packaging/gsoft/nuget/v3/index.json" --verbosity minimal --no-cache
```

# Build and Test

The project can be built by running Build.ps1.

A new patch version NuGet package is automatically published on any new commit on the main branch. This means that by completing a pull request, you automatically get a new NuGet package.

To release a new minor or major version, you need to manually edit the version number in the publish.yml pipeline. Then once the pull request containing the version change is merged, a new NuGet package will be published.