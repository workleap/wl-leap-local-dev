#Requires -Version 7.0

Begin {
    $ErrorActionPreference = "stop"
}

Process {
    @"
    services:
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

    # Since leap run would hang waiting for ctrl+c, this allows us to
    # keep executing our script while leap is running.
    $job = Start-Job -ScriptBlock {
      leap run --feature-flags LeapPhase2
    } -Name LeapJob

    Write-Host "Executing leap run"

    $jobOutput = @()
    $timeout = New-TimeSpan -Minutes 2
    $startTime = Get-Date

    try {
      # There's no easy way for us to know when Leap is done setting up everything we asked
      # We know waiting for Ctrl+c is the lasting Leap prints in the console.
      # So we periodically look for it or stop after 2 minutes.
      do {
        Start-Sleep -Seconds 1

        $jobOutput += Receive-Job -Job $job
        if ($jobOutput -match "Press Ctrl\+C to stop Leap") {
          # Sleep for a small amount of time to make sure the service had time to bootup
          Start-Sleep -Seconds 5
          break
        }

        $now = Get-Date
        $elapsed = $now - $startTime

        if ($elapsed -ge $timeout) {
          throw "Aspire couldn't start within the 2 minutes timeout"
        }

      } while ($true)

      # We do an http GET on an endpoint to make sure Aspire was able to start our service
      $response = Invoke-WebRequest -Uri "https://containerapp.workleap.localhost:1347" -Method GET

      if ($response.StatusCode -ne 200) {
        throw "Could not contact the app container"
      }

      Write-Host "Success, Aspire started the container app"
    } catch {

      # If something happened, let's print leap's output and rethrow the exception
      $jobOutput | Out-String | Write-Host
      throw
    } finally {

      # Signals leap to stop
      Stop-Job -Job $job
    }

    dotnet tool uninstall Workleap.Leap --global
}
