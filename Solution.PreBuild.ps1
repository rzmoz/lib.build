Param (
    [Parameter(Mandatory = $true)]
    [string]$slnDir,
    [Parameter(Mandatory = $true)]
    [string]$releaseArtifactsDir
)

Begin {
    Write-Host "$($MyInvocation.MyCommand.Name) started with $global:lastExitCode" -ForegroundColor Gray -BackgroundColor DarkCyan
    $ErrorActionPreference = "Stop"

    if (-NOT([System.IO.Path]::IsPathRooted($releaseArtifactsDir))) {
        Write-Error "releaseArtifactsDir not rooted: $releaseArtifactsDir"
        Exit 1
    }
}

Process {
    Write-Host "Cleaning: $releaseArtifactsDir"    
    Remove-Item "$($releaseArtifactsDir)/*" -recurse -force -ErrorAction Ignore
    $global:lastExitCode = 0 #reset error code if any
        
    New-Item $releaseArtifactsDir -ItemType Directory -Force | Out-Null

    #clean build artifacts
    Write-Host "Cleaning build artifacts (bin dirs):"
    Get-ChildItem "$slnDir" -Filter "bin" -recurse | ForEach-Object {
        Write-Host "Cleaning: $($_.FullName)" -ForegroundColor DarkGray
        Remove-Item "$($_.FullName)/*" -Recurse -Force
    }
}

End {
    Write-Host "$($MyInvocation.MyCommand.Name) finished with $global:lastExitCode" -ForegroundColor Gray -BackgroundColor DarkCyan
}