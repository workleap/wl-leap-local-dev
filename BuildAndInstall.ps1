#Requires -Version 7.0

Begin {
    $ErrorActionPreference = "stop"
}

Process {
    function Exec([scriptblock]$Command) {
        & $Command
        if ($LASTEXITCODE -ne 0) {
            throw ("An error occurred while executing command: {0}" -f $Command)
        }
    }

    $packageName = "Workleap.Leap"

    # Build the project and create the nupkg file
    Exec { & "$(Join-Path $PSScriptRoot "Build.ps1")" }

    Exec { & "$(Join-Path $PSScriptRoot "Install.ps1")" }
}
