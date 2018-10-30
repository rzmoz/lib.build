Function Invoke-Solution.PreBuild {
    [CmdletBinding()]
    Param (
        [Parameter(Mandatory = $true)]
        [string]$slnDir,
        [Parameter(Mandatory = $true)]
        [string]$releaseArtifactsDir
    )

    Begin {
        Write-Host "Solution.PreBuild started" -ForegroundColor Gray -BackgroundColor Black    
        $ErrorActionPreference = "Stop"

        if (-NOT([System.IO.Path]::IsPathRooted($releaseArtifactsDir))) {
            Write-Error "releaseArtifactsDir not rooted: $releaseArtifactsDir"
            Exit 1
        }
    }

    Process {
        Write-Host "Cleaning: $releaseArtifactsDir"    
        Remove-Item "$($releaseArtifactsDir)/*" -recurse -force -ErrorAction Ignore
        $lastExitCode = 0 #reset error code if any
        
        New-Item $releaseArtifactsDir -ItemType Directory -Force | Out-Null

        #clean build artifacts
        Write-Host "Cleaning build artifacts (bin dirs):"
        Get-ChildItem "$slnDir" -Filter "bin" -recurse | ForEach-Object {
            Write-Host "Cleaning: $($_.FullName)" -ForegroundColor DarkGray
            Remove-Item "$($_.FullName)/*" -Recurse -Force
        }
    }

    End {
        Write-Host "Solution.PreBuild finished with $LASTEXITCODE" -ForegroundColor Gray -BackgroundColor Black
        $global:lastexitcode = $LASTEXITCODE
    }
}