. (Join-Path $PSScriptRoot 'common.ps1')

$powershell = (Get-Command powershell.exe).Source
$backupScript = Join-Path $PSScriptRoot 'backup.ps1'
$restoreTestScript = Join-Path $PSScriptRoot 'test-backup.ps1'
$backupTaskName = 'LibraryAdmin-DailyBackup'
$restoreTaskName = 'LibraryAdmin-WeeklyRestoreTest'

$backupAction = New-ScheduledTaskAction `
    -Execute $powershell `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$backupScript`"" `
    -WorkingDirectory $ProjectRoot
$restoreAction = New-ScheduledTaskAction `
    -Execute $powershell `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$restoreTestScript`"" `
    -WorkingDirectory $ProjectRoot
$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Hours 1)
$principal = New-ScheduledTaskPrincipal `
    -UserId "$env:USERDOMAIN\$env:USERNAME" `
    -LogonType Interactive `
    -RunLevel Highest

$backupTrigger = New-ScheduledTaskTrigger -Daily -At '02:00'
$restoreTrigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At '03:00'
Register-ScheduledTask `
    -TaskName $backupTaskName `
    -Action $backupAction `
    -Trigger $backupTrigger `
    -Settings $settings `
    -Principal $principal `
    -Description '图书管理员系统每日 MySQL 备份' `
    -Force | Out-Null
Register-ScheduledTask `
    -TaskName $restoreTaskName `
    -Action $restoreAction `
    -Trigger $restoreTrigger `
    -Settings $settings `
    -Principal $principal `
    -Description '图书管理员系统每周备份恢复演练' `
    -Force | Out-Null

Get-ScheduledTask -TaskName $backupTaskName, $restoreTaskName |
    Select-Object TaskName, State
