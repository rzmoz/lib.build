Function New-Build {
    [CmdletBinding()]
    Param (
        [Parameter(Position = 0, Mandatory = $true)]
        [string]$solutionDir,
        [string]$releaseProjectFilter = "*.csproj",
        [string]$testProjectFilter = "*.tests.csproj",
        [string]$configuration = "release"
    )

    Begin {
        $ErrorActionPreference = "Stop"
        $global:lastexitcode = 0
        Write-Host "$($MyInvocation.MyCommand.Name) started with $global:lastExitCode" -ForegroundColor Gray -BackgroundColor DarkCyan

        $slnDir = Resolve-Path $solutionDir
        $releaseArtifactsDir = [System.IO.Path]::Combine($slnDir, ".releaseArtifacts").TrimEnd('\')
        Write-Host "Scanning for sln file in $slnDir"  -ForegroundColor DarkGray
        $slnPath = Get-ChildItem $slnDir -Filter "*.sln"

        if (-NOT($slnPath)) {
            Write-Host "Solution file not found in: $slnDir. Aborting..." -ForegroundColor DarkRed -BackgroundColor Black
            Exit 1
        }

        if ($slnPath.GetType().FullName -ne "System.IO.FileInfo") {
            Write-Host "More than one solution file found in: $slnDir. Aborting..." -ForegroundColor DarkRed -BackgroundColor Black
            Exit 1
        }

        Write-Host "Solution found: $($slnPath.FullName)" -ForegroundColor Gray
    }
    Process {
        [System.Collections.ArrayList]$releaseProjects = @(Get-ChildItem $slnDir -Filter $releaseProjectFilter -Recurse)
        [System.Collections.ArrayList]$testProjects = @(Get-ChildItem $slnDir -Filter $testProjectFilter -Recurse)

        $testProjects | ForEach-Object {
            for ($i = 0; $i -lt $releaseProjects.Count; $i++) {
                if ($releaseProjects[$i].FullName -eq $_.FullName) {
                    $releaseProjects.RemoveAt($i)
                    break
                }
            }
        }
        
        Write-Host "Release projects:"
        $releaseProjects | ForEach-Object {
            Write-Host $_.FullName -ForegroundColor DarkGray
        }
        Write-Host "Test projects:"
        $testProjects | ForEach-Object {
            Write-Host $_.FullName -ForegroundColor DarkGray
        }
        
        if ($global:lastexitcode -eq 0) {
            & "$PSScriptRoot\Solution.PreBuild.ps1" -slnDir $slnDir -releaseArtifactsDir $releaseArtifactsDir
        
            if ($global:lastexitcode -eq 0) {
                & "$PSScriptRoot\Solution.Build.ps1" -slnPath $slnPath.FullName -configuration $configuration
            }
            if ($global:lastexitcode -eq 0) {
                & "$PSScriptRoot\Solution.PostBuild.ps1" -slnDir $slnDir -releaseArtifactsDir $releaseArtifactsDir -releaseProjects $releaseProjects -configuration $configuration                
            }
        }
    }
    End {        
        Write-Host "$($MyInvocation.MyCommand.Name) finished with $global:lastExitCode" -ForegroundColor Gray -BackgroundColor DarkCyan
    }
}
