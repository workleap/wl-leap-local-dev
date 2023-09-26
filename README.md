[![nuget](https://feeds.dev.azure.com/gsoft/_apis/public/Packaging/Feeds/gsoft/Packages/40cf5f6f-f4e2-457c-9aa6-bd5d74c19ff9/Badge)](https://dev.azure.com/gsoft/Shared-Assets/_artifacts/feed/gsoft/NuGet/leap)
[![build](https://dev.azure.com/gsoft/Shared-Assets/_apis/build/status/Leap/Leap%20NuGet%20push?branchName=main)](https://dev.azure.com/gsoft/Shared-Assets/_build/latest?definitionId=145&branchName=main)

# Introduction 
LEAP, which stands for _Local Environment Application Proxy_, is a custom made command-line which streamline our local development process by introducing service discovery, better dependency management, efficient resource allocation and a standardized configuration. 

# Getting Started
You first need to install the LEAP CLI. This can be done by running the following command in a terminal:
```powershell
dotnet tool install --global leap
```

# Build and Test
The project can be built by running Build.ps1.

A new patch version NuGet package is automatically published on any new commit on the main branch. This means that by completing a pull request, you automatically get a new NuGet package.

To release a new minor or major version, you need to manually edit the version number in the publish.yml pipeline. Then once the pull request containing the version change is merged, a new NuGet package will be published.