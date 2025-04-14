#Requires -Version 7.0

param(
  [switch]$SkipTests
)

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

        # Install GitVersion which is specified in the .config/dotnet-tools.json
        # https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use
        # We install it as a local tool so developers don't have to install it globally
        Exec { & dotnet tool restore }

        # We use "SemVer" because that's the default behavior of the GitVersion.MsBuild targets
        # https://github.com/GitTools/GitVersion/blob/5.12.0/src/GitVersion.MsBuild/msbuild/tools/GitVersion.MsBuild.targets#L55
        $version = Exec { & dotnet dotnet-gitversion /output json /showvariable SemVer }

        # Change the build number in Azure DevOps
        Write-Host "##vso[build.updatebuildnumber]$version"

        Exec { & dotnet clean -c Release }
        Exec { & dotnet build -c Release /p:Version=$version }
        if (-not $SkipTests) {
            Exec { & dotnet test  -c Release --no-build --results-directory "$outputDir" --no-restore --logger "trx" --logger "console;verbosity=detailed" --blame-hang-timeout 10m }
        }
        Exec { & dotnet pack  -c Release --no-build --output "$outputDir" /p:Version=$version }

        if (($null -ne $env:NUGET_SOURCE ) -and ($null -ne $env:NUGET_API_KEY)) {
            Exec { & dotnet nuget push "$nupkgsPath" -s $env:NUGET_SOURCE -k $env:NUGET_API_KEY --skip-duplicate }
        }
    }
    finally {
        Pop-Location
    }
}
