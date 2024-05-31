#Requires -Version 7.0

function Start-Leap {
  param(
    [Parameter(Mandatory = $true)]
    [int]$timeoutInMinutes
  )

  # Since "leap run" would hang waiting for Ctrl+C, this allows us to
  # keep executing our script while leap is running.
  $job = Start-Job -Name LeapJob -ScriptBlock {
    leap run --skip-version-check --verbosity diagnostic
  }

  Write-Host "Executing leap run"

  $timeout = New-TimeSpan -Minutes $timeoutInMinutes
  $startTime = Get-Date

  do {
    Start-Sleep -Seconds 1

    $jobOutput = (Receive-Job -Job $job) | Out-String
    if (![string]::IsNullOrEmpty($jobOutput)) {
      Write-Host $jobOutput
    }

    if ($jobOutput -match "Press Ctrl\+C to stop Leap") {
      # Aspire takes a moment to start up
      Start-Sleep -Seconds 5
      return $job
    }

    $now = Get-Date
    $elapsed = $now - $startTime

    if ($elapsed -ge $timeout) {
      Stop-Job -Job $job
      throw "Leap couldn't start within the $($timeout.TotalMinutes) minutes timeout"
    }

  } while ($true)
}

function Assert-UrlReturnsOk {
  param(
    [Parameter(Mandatory = $true)]
    [string]$url,
    [Parameter(Mandatory = $true)]
    [string]$description
  )

  Write-Host "Testing URL $url..."

  $timeout = New-TimeSpan -Minutes 1
  $startTime = Get-Date

  while ($true) {
    $now = Get-Date
    $elapsed = $now - $startTime
    $lastStatusCode = $null

    if ($elapsed -ge $timeout) {
      throw "$description timed out waiting for 200 OK, last status code was $lastStatusCode"
    }

    $response = Invoke-WebRequest -Uri $url -Method GET -SkipHttpErrorCheck

    if ($response.StatusCode -eq 200) {
      Write-Host "$description returned 200 OK"
      break
    }
    else {
      $lastStatusCode = $response.StatusCode
      Start-Sleep -Seconds 1
    }
  }
}

function Reset-AspNetCoreWebApiSample {
  Remove-Item -Recurse -Force .\aspnetcorewebapi -ErrorAction SilentlyContinue
  dotnet new webapi --name aspnetcorewebapi --output aspnetcorewebapi
}

function Assert-DockerContainer {
  param(
    [Parameter(Mandatory = $true)]
    [string]$containerName
  )

  $containerRunning = [bool](docker ps --quiet --filter name="$containerName")
  if ($containerRunning) {
    Write-Host "Docker container $containerName is running."
  }
  else {
    throw "Docker container $containerName is not running."
  }
}
