# CleanupScript.ps1
# Script to recursively search for and delete 'obj' and 'bin' folders

param(
    [Parameter(Mandatory=$true)]
    [string]$RootPath,
    
    [Parameter(Mandatory=$false)]
    [switch]$WhatIf = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$Confirm = $true
)

# Function to display script information
function Show-ScriptInfo {
    Write-Host "==================================================" -ForegroundColor Cyan
    Write-Host "Cleanup Script - Delete obj and bin folders" -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Cyan
    Write-Host "This script will search for and delete all 'obj' and 'bin' folders"
    Write-Host "found under the specified root path."
    Write-Host "Root Path: $RootPath"
    Write-Host "WhatIf Mode: $WhatIf"
    Write-Host "Confirm Mode: $Confirm"
    Write-Host "==================================================" -ForegroundColor Cyan
    Write-Host ""
}

# Function to count the number of folders that would be deleted
function Count-FoldersToDelete {
    $objFolders = Get-ChildItem -Path $RootPath -Directory -Recurse -Filter "obj" | Select-Object -ExpandProperty FullName
    $binFolders = Get-ChildItem -Path $RootPath -Directory -Recurse -Filter "bin" | Select-Object -ExpandProperty FullName
    
    $totalCount = $objFolders.Count + $binFolders.Count
    
    return @{
        ObjCount = $objFolders.Count
        BinCount = $binFolders.Count
        TotalCount = $totalCount
    }
}

# Main execution starts here
if (-not (Test-Path $RootPath)) {
    Write-Error "The specified path does not exist: $RootPath"
    exit 1
}

Show-ScriptInfo

# Count folders before deletion
$folderCounts = Count-FoldersToDelete
Write-Host "Found $($folderCounts.ObjCount) 'obj' folders and $($folderCounts.BinCount) 'bin' folders." -ForegroundColor Yellow
Write-Host "Total folders to process: $($folderCounts.TotalCount)" -ForegroundColor Yellow
Write-Host ""

# If WhatIf is specified, just show what would be deleted
if ($WhatIf) {
    Write-Host "WhatIf mode is enabled. No folders will be deleted." -ForegroundColor Green
    Write-Host "The following folders would be deleted:" -ForegroundColor Green
    
    Get-ChildItem -Path $RootPath -Directory -Recurse -Include "obj","bin" | 
        ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    
    exit 0
}

# If Confirm is specified, ask for confirmation
if ($Confirm) {
    $confirmation = Read-Host "Are you sure you want to delete these folders? (y/n)"
    if ($confirmation -ne "y") {
        Write-Host "Operation cancelled by user." -ForegroundColor Yellow
        exit 0
    }
}

# Perform the deletion
$deletedCount = 0
$errorCount = 0

Write-Host "Starting deletion process..." -ForegroundColor Cyan

Get-ChildItem -Path $RootPath -Directory -Recurse -Include "obj","bin" | 
    ForEach-Object {
        $folder = $_
        try {
            Write-Host "Deleting: $($folder.FullName)" -ForegroundColor Gray
            Remove-Item -Path $folder.FullName -Recurse -Force
            $deletedCount++
        }
        catch {
            Write-Host "Error deleting $($folder.FullName): $_" -ForegroundColor Red
            $errorCount++
        }
    }

# Display summary
Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Operation Complete" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Folders successfully deleted: $deletedCount" -ForegroundColor Green
if ($errorCount -gt 0) {
    Write-Host "Folders with errors: $errorCount" -ForegroundColor Red
}
Write-Host "==================================================" -ForegroundColor Cyan