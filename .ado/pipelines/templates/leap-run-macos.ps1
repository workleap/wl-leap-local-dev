#Requires -Version 7.0

Begin {
  $ErrorActionPreference = "stop"
}

Process {
  Import-Module (Join-Path $PSScriptRoot "shared.psm1") -Force

  Reset-AspNetCoreWebApiSample

  # We can't start containers on macOS because:
  # - The officiel Docker engine isn't installed on macOS agents by default due to licensing restrictions.
  # - We could use Colima, a free alternative available on macOS agents but starting the Colima VM takes too much time and sometimes times out.
  @"
  services:
    aspnetcorewebapi:
      ingress:
        host: aspnetcorewebapi.workleap.localhost
      runners:
        - type: dotnet
          project: ./aspnetcorewebapi/aspnetcorewebapi.csproj
"@ | Set-Content -Path leap.yaml -Force

  $job = Start-Leap -timeoutInMinutes 8

  try {
    Assert-UrlReturnsOk -url "https://localhost:18888" -description "Aspire dashboard"

    Assert-UrlReturnsOk -url "https://aspnetcorewebapi.workleap.localhost:1347/weatherforecast" -description ".NET app"
  }
  finally {
    Stop-Job -Job $job
  }
}
