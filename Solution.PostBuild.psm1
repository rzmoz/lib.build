Function Invoke-Solution.PostBuild {
    [CmdletBinding()]
    Param (
        [Parameter(Mandatory = $true)]
        [string]$slnDir,
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$releaseArtifactsDir,
        [Parameter(Mandatory = $true)]
        [object[]]$releaseProjects,
        [Parameter(Mandatory = $true)]
        [string]$configuration
    )

    Begin {
        Write-Host "Solution.PostBuild started" -ForegroundColor Gray -BackgroundColor Black
        $ErrorActionPreference = "Stop"
    }

    Process {
        Import-Module "$PSScriptRoot\Project.PublishReleaseArtifacts.psm1" -Force
        
        $releaseProjects | ForEach-Object {
            Invoke-Project.PublishReleaseArtifacts -projectFilePath $_.FullName -configuration $configuration -releaseArtifactsDir $releaseArtifactsDir
        }
    }

    End {        
        #Robocopy exit code
        if ($LASTEXITCODE -lt 8) {
            $LASTEXITCODE = 0 #ok            
        }

        Write-Host "Solution.PostBuild finished with $LASTEXITCODE" -ForegroundColor Gray -BackgroundColor Black        
        
        if ($LASTEXITCODE -ne 0) {
            EXIT $LASTEXITCODE
        }
    }
}