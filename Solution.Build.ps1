Param (
    [Parameter(Mandatory = $true)]
    [System.IO.FileInfo]$slnPath,
    [Parameter(Mandatory = $true)]
    [string]$configuration
)

Begin {        
    $ErrorActionPreference = "Stop"
    Write-Host "$($MyInvocation.MyCommand.Name) started with $global:lastExitCode" -ForegroundColor Gray -BackgroundColor DarkCyan
}

Process {
    if (-NOT(Test-Path $slnPath)) {
        Write-host "Solution file not found: $($slnPath.FullName)" -ForegroundColor Red -BackgroundColor Black
        Exit 1
    }

    dotnet build $slnPath -c $configuration --no-incremental
    
    if ($global:lastExitCode -ne 0) {
        Write-host "Build failed. See log for details" -ForegroundColor Red -BackgroundColor Black
    }    
}

End {
    Write-Host "$($MyInvocation.MyCommand.Name) finished with $global:lastExitCode" -ForegroundColor Gray -BackgroundColor DarkCyan
}
