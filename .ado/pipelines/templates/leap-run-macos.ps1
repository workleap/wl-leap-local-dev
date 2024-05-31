#Requires -Version 7.0

Begin {
  $ErrorActionPreference = "stop"
}

Process {
  Import-Module (Join-Path $PSScriptRoot "shared.psm1") -Force

  Reset-AspNetCoreWebApiSample

  # We start less containers on macOS because we already spend too much time setting up Docker with Colima
  # and we don't want the CI to time out
  @"
  services:
    aspnetcorewebapi:
      ingress:
        host: aspnetcorewebapi.workleap.localhost
      runners:
        - type: dotnet
          project: ./aspnetcorewebapi/aspnetcorewebapi.csproj

    containerapp:
      ingress:
        host: containerapp.workleap.localhost
      runners:
        - type: docker
          image: mcr.microsoft.com/dotnet/samples:aspnetapp
          containerPort: 8080

  dependencies:
    - type: redis
"@ | Set-Content -Path leap.yaml -Force

  $job = Start-Leap -timeoutInMinutes 8

  try {
    Assert-UrlReturnsOk -url "https://localhost:18888" -description "Aspire dashboard"

    Assert-UrlReturnsOk -url "https://containerapp.workleap.localhost:1347" -description "Containerized app"
    Assert-UrlReturnsOk -url "https://aspnetcorewebapi.workleap.localhost:1347/weatherforecast" -description ".NET app"

    Assert-DockerContainer -containerName "leap-redis"
  }
  finally {
    Stop-Job -Job $job
  }
}
