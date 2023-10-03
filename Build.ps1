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
    
    $workingDir = Join-Path $PSScriptRoot "src"
    $outputDir = Join-Path $PSScriptRoot ".output"

    try {
        Push-Location $workingDir
        Remove-Item $outputDir -Force -Recurse -ErrorAction SilentlyContinue
    
        Exec { & dotnet clean -c Release }
        Exec { & dotnet build -c Release }
        Exec { & dotnet test  -c Release --no-build --results-directory "$outputDir" --no-restore -l "trx" -l "console;verbosity=detailed" }
        Exec { & dotnet pack  -c Release -o "$outputDir" }

        # Cross-platform system tests
        Exec { & dotnet tool update Workleap.Leap --global --add-source "$outputDir" --verbosity minimal --no-cache --interactive }
        Exec { & leap run }
        Exec { & dotnet tool uninstall Workleap.Leap --global }
    }
    finally {
        Pop-Location
    }
}