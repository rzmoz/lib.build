Param (
    [Parameter(Mandatory = $true)]
    [string]$sourceDir,
    [Parameter(Mandatory = $true)]
    [string]$targetDir,
    [Parameter(Mandatory = $true)]
    [string]$options,
    [string]$filesToCopy = "*.*"
)

Begin {
    $ErrorActionPreference = "Stop"
    Write-Host "$($MyInvocation.MyCommand.Name) started with $global:lastExitCode" -ForegroundColor Gray -BackgroundColor DarkCyan

    [HashTable]$robocopyExitCodes = @{}
    $robocopyExitCodes[0] = "No errors occurred, and no copying was done. The source and destination directory trees are completely synchronized."
    $robocopyExitCodes[1] = "All Okay. One or more files were copied successfully (that is, new files have arrived)."
    $robocopyExitCodes[2] = "Some Extra files or directories were detected. No files were copied. Examine the output log for details."
    $robocopyExitCodes[3] = "(2 + 1) Some files were copied. Additional files were present. No failure was encountered."
    $robocopyExitCodes[4] = "Some Mismatched files or directories were detected. Examine the output log. Housekeeping might be required."
    $robocopyExitCodes[5] = "(4 + 1) Some files were copied. Some files were mismatched. No failure was encountered."
    $robocopyExitCodes[6] = "(4 + 2) Additional files and mismatched files exist. No files were copied and no failures were encountered. This means that the files already exist in the destination directory"
    $robocopyExitCodes[7] = "(4 + 1 + 2) Files were copied, a file mismatch was present, and additional files were present."
    $robocopyExitCodes[8] = "ERROR. See log for details. Some files or directories could not be copied."
    $robocopyExitCodes[9] = "ERROR. See log for details"
    $robocopyExitCodes[0] = "ERROR. See log for details"
    $robocopyExitCodes[10] = "ERROR. See log for details"
    $robocopyExitCodes[11] = "ERROR. See log for details"
    $robocopyExitCodes[12] = "ERROR. See log for details"
    $robocopyExitCodes[13] = "ERROR. See log for details"
    $robocopyExitCodes[14] = "ERROR. See log for details"
    $robocopyExitCodes[15] = "ERROR. See log for details"
    $robocopyExitCodes[16] = "Serious error. Robocopy did not copy any files.
    Either a usage error or an error due to insufficient access privileges
    on the source or destination directories."
}

Process {
    $splitOptions = "$options /NS /NC /NFL /NDL /NP"
    Write-Host "Source folder: $sourceDir" -ForegroundColor DarkGray
    Write-Host "Destination folder: $targetDir" -ForegroundColor DarkGray
    Write-Host "Files to copy: $filesToCopy" -ForegroundColor DarkGray
    Write-Host "Options: $splitOptions" -ForegroundColor DarkGray
    ROBOCOPY $sourceDir $targetDir $filesToCopy.Split(' ') $splitOptions.Split(' ') | Write-Host -ForegroundColor DarkCyan
    
    if ($global:lastExitCode -lt 8) {
        Write-Host "Robocopy finished with $($global:lastExitCode): $($robocopyExitCodes[$global:lastExitCode])" -ForegroundColor DarkCyan
        $global:lastExitCode = 0#convert robocopy status codes to console status codes (all okay when less than 8)
    }
    else {
        Write-Host "Robocopy finished with $($global:lastExitCode): $($robocopyExitCodes[$global:lastExitCode])" -ForegroundColor DarkRed -BackgroundColor Black
    }        
}

End {
    Write-Host "$($MyInvocation.MyCommand.Name) finished with $global:lastExitCode" -ForegroundColor Gray -BackgroundColor DarkCyan
}