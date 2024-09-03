# Leap local dev

[![nuget](https://feeds.dev.azure.com/gsoft/_apis/public/Packaging/Feeds/gsoft/Packages/7e26c0cd-3179-49c3-a3a7-e92df061eee5/Badge)](https://dev.azure.com/gsoft/Shared-Assets/_artifacts/feed/gsoft/NuGet/Workleap.Leap)
[![build](https://dev.azure.com/gsoft/Shared-Assets/_apis/build/status%2FLeap%2FLeap%20Publish?branchName=main)](https://dev.azure.com/gsoft/Shared-Assets/_build/latest?definitionId=219&branchName=main)

User documentation is [available on Confluence](https://gsoftdev.atlassian.net/wiki/x/dIDh4Q). This README is for developers working on Leap local dev.

## Requirements

Install the .NET SDK specified in the `global.json` file.

Leap local dev uses [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) for orchestration and its dashboard. Install the required .NET Aspire workload version with the command below. It matches the [Aspire.Hosting.AppHost](https://www.nuget.org/packages/Aspire.Hosting.AppHost) package version used in the project.

```bash
dotnet workload update --from-rollback-file ./rollback.json
```

We use a [.NET workload rollback file](https://github.com/dotnet/aspire/discussions/2230#discussioncomment-8496035) to ensure the correct version is installed. The `rollback.json` is maintained by Renovate. We do not use [workload sets](https://github.com/dotnet/aspire/issues/5501) as they do not clearly specify the installed .NET Aspire workload version.

## Build and test

- Run `Build.ps1` to build the project and generate a NuGet package in the `.output` folder. [GitVersion](https://gitversion.net/) determines the NuGet package version based on commit history and tags.

- Run `BuildAndInstall.ps1` to build and install the package locally from your current directory. It will replace any existing version of the tool.

- Preview packages are published on the main branch ([gsoft feed](https://dev.azure.com/gsoft/Shared-Assets/_artifacts/feed/gsoft/NuGet/Workleap.Leap/)) and pull requests ([gsoftdev feed](https://dev.azure.com/gsoft/Shared-Assets/_artifacts/feed/gsoftdev/NuGet/Workleap.Leap/)). Create a tag in the format `x.y.z` (e.g., `1.0.0`) to publish a stable build.
