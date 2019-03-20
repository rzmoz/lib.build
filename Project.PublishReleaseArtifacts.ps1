Param (        
    [Parameter(Mandatory = $true)]
    [System.IO.FileInfo]$projectFilePath,
    [Parameter(Mandatory = $true)]
    [string]$configuration,
    [Parameter(Mandatory = $true)]
    [string]$releaseArtifactsDir
)

Begin {
    $ErrorActionPreference = "Stop"
    Write-Host "$($MyInvocation.MyCommand.Name) started with $global:lastExitCode" -ForegroundColor Gray -BackgroundColor DarkCyan
}

Process {

    [xml]$projectFileXml = Get-Content $projectFilePath
    $targetFrameWork = $projectFileXml.Project.PropertyGroup.TargetFramework
    Write-Host "Target Framework $targetFramework" -ForegroundColor Gray

    $nameWoExtension = [System.IO.Path]::GetFileNameWithoutExtension($projectFilePath.Name)
    $projectTargetDir = "$($projectFilePath.Directory)\bin\$configuration\$targetFrameWork"
    Write-Host "Project Target Dir: $projectTargetDir" -ForegroundColor DarkGray    
    $releaseArtifactsTargetDir = "$releaseArtifactsDir/$nameWoExtension"
    $targetFrameworkIsDotNetFramework = $targetFramework -match "^net[0-9]+$"
    
    Write-Host "Processing $nameWoExtension..." -ForegroundColor Gray
    
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
    $copyOutput = & "$PSScriptRoot\Robocopy.ps1" "$projectTargetDir" "$releaseArtifactsTargetDir" /e
    Write-Host $copyOutput -ForegroundColor DarkGray

    if ($targetFrameworkIsDotNetFramework) {
        & "$PSScriptRoot\Robocopy.ps1" "$projectTargetDir" "$releaseArtifactsTargetDir/bin" -files "*.dll"
        & "$PSScriptRoot\Robocopy.ps1" "$projectTargetDir" "$releaseArtifactsTargetDir/bin" -files "*.pdb"
        Remove-Item "$releaseArtifactsTargetDir\*.dll"
        Remove-Item "$releaseArtifactsTargetDir\*.pdb"        
    }
}

End {
    Write-Host "$($MyInvocation.MyCommand.Name) finished with $global:lastExitCode" -ForegroundColor Gray -BackgroundColor DarkCyan
}
