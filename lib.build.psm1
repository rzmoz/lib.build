Function New-Lib.Build {
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

        $slnDir = Resolve-Path $solutionDir
        Write-Host "Lib.Build starting in $slnDir..." -ForegroundColor Gray -BackgroundColor Black
        $releaseArtifactsDir = [System.IO.Path]::Combine($slnDir, ".releaseArtifacts").TrimEnd('\')
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
            Import-Module "$PSScriptRoot\Solution.PreBuild.psm1" -Force
            Invoke-Solution.PreBuild -slnDir $slnDir -releaseArtifactsDir $releaseArtifactsDir
        }
        if ($global:lastexitcode -eq 0) {
            Import-Module "$PSScriptRoot\Solution.Build.psm1" -Force
            Invoke-Solution.Build -slnPath $slnPath.FullName -configuration $configuration
        }
        if ($global:lastexitcode -eq 0) {
            Import-Module "$PSScriptRoot\Solution.PostBuild.psm1" -Force
            Invoke-Solution.PostBuild -slnDir $slnDir -releaseArtifactsDir $releaseArtifactsDir -releaseProjects $releaseProjects -configuration $configuration                
        }
    }

    End {
        $color = [System.ConsoleColor]::Green
        if ($global:lastexitcode -ne 0) {
            $color = [System.ConsoleColor]::DarkRed
        }
        Write-Host "Lib.Build finished with $LASTEXITCODE" -ForegroundColor $color -BackgroundColor Black
    }
}