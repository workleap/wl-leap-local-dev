#Requires -Version 7.0

Begin {
  $ErrorActionPreference = "stop"
}

Process {
  Import-Module (Join-Path $PSScriptRoot "shared.psm1") -Force

  Reset-AspNetCoreWebApiSample

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
    - type: mongo
    - type: redis
    - type: postgres
    - type: sqlserver
    - type: eventgrid
    - type: azurite
      containers:
        - mycontainer
      tables:
        - mytable
      queues:
        - myqueue
"@ | Set-Content -Path leap.yaml -Force

  $job = Start-Leap -timeoutInMinutes 5

  try {
    Assert-UrlReturnsOk -url "https://localhost:18888" -description "Aspire dashboard"

    Assert-UrlReturnsOk -url "https://containerapp.workleap.localhost:1347" -description "Containerized app"
    Assert-UrlReturnsOk -url "https://aspnetcorewebapi.workleap.localhost:1347/weatherforecast" -description ".NET app"

    Assert-DockerContainer -containerName "leap-redis"
    Assert-DockerContainer -containerName "leap-mongo"
    Assert-DockerContainer -containerName "leap-postgres"
    Assert-DockerContainer -containerName "leap-sqlserver"
    Assert-DockerContainer -containerName "leap-eventgrid"
    Assert-DockerContainer -containerName "leap-azurite"
  }
  finally {
    Stop-Job -Job $job
  }
}
