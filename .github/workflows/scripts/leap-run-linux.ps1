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
          protocol: https

    containerapp:
      ingress:
        host: containerapp.workleap.localhost
      runners:
        - type: docker
          image: mcr.microsoft.com/dotnet/samples:aspnetapp
          containerPort: 8080
          protocol: https

  dependencies:
    - type: mongo
    - type: fusionauth
    - type: redis
    - type: postgres
    - type: sqlserver
    - type: eventgrid
      topics:
        orders:
          - https://aspnetcorewebapi.workleap.localhost:1347/eventgrid/domainevents
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
    Assert-DockerContainer -containerName "leap-fa-app"
    Assert-DockerContainer -containerName "leap-fa-db"
    Assert-DockerContainer -containerName "leap-fa-proxy"

    # Assert Event Grid settings generation from leap.yaml file and user settings
    $userEventGridSettingsFilePath = Join-Path "~" ".leap" "eventgridsettings.json"
    $generatedEventGridSettingsFilePath = Join-Path "~" ".leap" "generated" "eventgridsettings.json"

    Assert-FileContains -path $generatedEventGridSettingsFilePath "https://aspnetcorewebapi.workleap.localhost:1347/eventgrid/domainevents"
    Assert-FileDoesNotContain -path $generatedEventGridSettingsFilePath "https://somethingnew.workleap.localhost:1347/eventgrid/domainevents"

@"
{
  "Topics": {
    "orders": ["https://somethingnew.workleap.localhost:1347/eventgrid/domainevents"]
  }
}
"@ | Set-Content -Path $userEventGridSettingsFilePath -Force

    # Wait for leap to merge the user settings into the generated settings
    Start-Sleep -Seconds 1

    Assert-FileContains -path $generatedEventGridSettingsFilePath "https://aspnetcorewebapi.workleap.localhost:1347/eventgrid/domainevents"
    Assert-FileContains -path $generatedEventGridSettingsFilePath "https://somethingnew.workleap.localhost:1347/eventgrid/domainevents"

  }
  finally {
    Stop-Job -Job $job
  }
}
