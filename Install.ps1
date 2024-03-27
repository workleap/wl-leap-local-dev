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

    # Attempt to uninstall the tool if it's already installed
    try {
        Exec { & dotnet tool uninstall $packageName --global }
    } catch {
        Write-Host -ForegroundColor Green "$packageName might already be uninstalled, continuing..."
    }

    # Find the nupkg file in the output directory
    $outputDir = Join-Path $PSScriptRoot ".output"
    $nupkgFileName = Get-ChildItem -Path $outputDir -Filter "*Workleap.Leap.*.nupkg" -File | Select-Object -First 1 -ExpandProperty Name

    if (-not $nupkgFileName) {
        throw "No nupkg file found in $outputDir"
    }

    # Extract the version from the nupkg file name
    $version = $nupkgFileName -replace "$packageName\.(?<version>.+)\.nupkg", '${version}'
    Write-Host "Installing $packageName version $version from $outputDir"

    # Install the tool with the specified version
    Exec { & dotnet tool update $packageName --global --interactive --no-cache --version $version --add-source $outputDir --verbosity minimal }
}
