Function Invoke-Solution.Build
{
    [CmdletBinding()]
    Param (
        [Parameter(Mandatory=$true)]
        [System.IO.FileInfo]$slnPath,
        [Parameter(Mandatory=$true)]
        [string]$configuration
    )

    Begin{
        Write-Host "Solution.Build started" -ForegroundColor Gray -BackgroundColor Black
        $ErrorActionPreference = "Stop"
    }

    Process {
        if(-NOT(Test-Path $slnPath)){
            Write-host "Solution file not found: $($slnPath.FullName)" -ForegroundColor Red -BackgroundColor Black
            Exit 1
        }

        dotnet build $slnPath -c $configuration --no-incremental
    
        if ($lastExitCode -ne 0) {
            Write-host "Build failed" -ForegroundColor Red -BackgroundColor Black
        }    
    }

    End {
        Write-Host "Solution.Build finished with $LASTEXITCODE" -ForegroundColor Gray -BackgroundColor Black
        $global:lastexitcode = $LASTEXITCODE        
    }
}