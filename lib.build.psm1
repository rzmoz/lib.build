Function New-DotNet.Build {
    [CmdletBinding()]
    Param (
        [Parameter(Position = 0, Mandatory = $true)]
        [string]$solutionDir,
        [string]$releaseFilter = "*.csproj",
        [string]$configuration = "release"
    )

    Begin {
        $ErrorActionPreference = "Stop"
        $slnDir = Resolve-Path $solutionDir
        Write-Host "Lib.Build starting in $slnDir..." -ForegroundColor Gray -BackgroundColor Black
        $releaseArtifactsDir = [System.IO.Path]::Combine($slnDir, ".releaseArtifacts").TrimEnd('\')
    }

    Process {
        Write-Host "Scanning for sln file in $slnDir"  -ForegroundColor DarkGray
        $slnPath = Get-ChildItem $slnDir -Filter "*.sln"

        if (-NOT($slnPath)) {
            Write-Host "Solution file not found in: $slnDir. Aborting..." -ForegroundColor DarkRed -BackgroundColor Black
            Exit 1
        }

        if ($slnPath.GetType().FullName -ne "System.IO.FileInfo") {
            Write-Host "More than one solution file  found in: $slnDir. Aborting..." -ForegroundColor DarkRed -BackgroundColor Black
            Exit 1
        }

        Write-Host "Solution found: $($slnPath.FullName)" -ForegroundColor Gray

        Import-Module "$PSScriptRoot\Solution.PreBuild.psm1" -Force
        Invoke-Solution.PreBuild -slnDir $slnDir -releaseArtifactsDir $releaseArtifactsDir
        
        Import-Module "$PSScriptRoot\Solution.Build.psm1" -Force
        Invoke-Solution.Build -slnPath $slnPath.FullName -configuration $configuration

        Import-Module "$PSScriptRoot\Solution.PostBuild.psm1" -Force
        Invoke-Solution.PostBuild -slnDir $slnDir -releaseArtifactsDir $releaseArtifactsDir -releaseFilter $releaseFilter -configuration $configuration
    }

    End {
        Write-Host "Lib.Build finished" -ForegroundColor Gray -BackgroundColor Black        
    }
}