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
    $ErrorActionPreference = "Stop"
    Write-Host "$($MyInvocation.MyCommand.Name) started with $global:lastExitCode" -ForegroundColor Gray -BackgroundColor DarkCyan
}

Process {
    $releaseProjects | ForEach-Object {
        & "$PSScriptRoot\Project.PublishReleaseArtifacts.ps1" -projectFilePath "$($_.FullName)" -configuration $configuration -releaseArtifactsDir $releaseArtifactsDir
        if ($global:lastexitcode -ne 0) {
            RETURN
        }
    }
}

End {        
    Write-Host "$($MyInvocation.MyCommand.Name) finished with $global:lastExitCode" -ForegroundColor Gray -BackgroundColor DarkCyan
}