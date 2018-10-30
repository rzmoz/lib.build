Function Invoke-Project.PublishReleaseArtifacts {
    [CmdletBinding()]
    Param (        
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$projectFilePath,
        [Parameter(Mandatory = $true)]
        [string]$configuration,
        [Parameter(Mandatory = $true)]
        [string]$releaseArtifactsDir
    )

    Begin {
        Write-Host "Project.PublishReleaseArtifacts for $($projectFilePath.Name) started" -ForegroundColor Gray -BackgroundColor Black
        $ErrorActionPreference = "Stop"
    }

    Process {
        $nameWoExtension = [System.IO.Path]::GetFileNameWithoutExtension($projectFilePath.Name)
        $projectTargetDir = Get-ChildItem "$($projectFilePath.Directory)\bin\$configuration\**"
        $targetFramework = $projectTargetDir.Name
        $releaseArtifactsTargetDir = "$releaseArtifactsDir/$nameWoExtension"
        $targetFrameworkIsDotNetFramework = $targetFramework -match "^net[0-9]+$"
        Write-Host "Processing $nameWoExtension..." -ForegroundColor Gray
        Write-Host "Project Target Dir: $projectTargetDir" -ForegroundColor DarkGray
        Write-Host "Release Artifacts Target Dir: $releaseArtifactsTargetDir" -ForegroundColor DarkGray
        Write-Host "Target Framework: $targetFramework" -ForegroundColor DarkGray

        New-Item $releaseArtifactsTargetDir -ItemType Directory -Force | Out-Null   

        if ($targetFrameworkIsDotNetFramework) {
            Write-Host "Target Framework is .NET Framework: $targetFramework"
            New-Item "$releaseArtifactsTargetDir\bin" -ItemType Directory -Force | Out-Null
        }
        else {
            Write-Host "Target Framework is .NET Core or netstandard: $targetFramework" -ForegroundColor DarkGray
        }

        Write-Host "Copying release artifacts for $($projectFilePath.FullName)"
        $copyOutput = Robocopy "$projectTargetDir" "$releaseArtifactsTargetDir"  /E /NS /NC /NFL /NDL
        Write-Host $copyOutput -ForegroundColor DarkGray

        if ($targetFrameworkIsDotNetFramework) {
            Robocopy "$projectTargetDir" "$releaseArtifactsTargetDir/bin" *.dll  /NS /NC /NFL /NDL
            Robocopy "$projectTargetDir" "$releaseArtifactsTargetDir/bin" *.pdb  /NS /NC /NFL /NDL
            Remove-Item "$releaseArtifactsTargetDir\*.dll"
            Remove-Item "$releaseArtifactsTargetDir\*.pdb"        
        }
    }

    End {
        Write-Host "Project.PublishReleaseArtifacts finished" -ForegroundColor Gray -BackgroundColor Black

        #Robocopy exit code
        if ($LASTEXITCODE -lt 8) {
            $LASTEXITCODE = 0 #ok            
        }
        else {
            EXIT $LASTEXITCODE
        }
    }
}