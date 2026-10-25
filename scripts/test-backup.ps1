param(
    [string]$BackupFile
)

. (Join-Path $PSScriptRoot 'common.ps1')

$backupRoot = [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot '.backups'))
if (-not $BackupFile) {
    $latest = Get-ChildItem -LiteralPath $backupRoot -Filter 'library_admin_*.sql' -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if (-not $latest) {
        throw '尚无备份文件，请先运行 backup.ps1。'
    }
    $BackupFile = $latest.FullName
}

$resolvedBackup = (Resolve-Path -LiteralPath $BackupFile).Path
$allowedPrefix = $backupRoot.TrimEnd('\') + '\'
if (-not $resolvedBackup.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw '恢复演练只允许使用 .backups 目录中的备份文件。'
}

$databaseState = Initialize-MySqlDatabase
try {
    $restoreDatabase = 'library_admin_restore_test'
    $setupSql = @"
DROP DATABASE IF EXISTS ``$restoreDatabase``;
CREATE DATABASE ``$restoreDatabase`` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
"@
    $null = Invoke-MySql -Sql $setupSql -OptionFile $RootOptionFile
    $null = Invoke-MySqlFile -InputFile $resolvedBackup -OptionFile $RootOptionFile -Database $restoreDatabase

    $tables = @('user_accounts', 'books', 'book_copies', 'loans', 'admin_applications', 'schema_versions')
    $queries = foreach ($table in $tables) {
        "(SELECT '$table', COUNT(*) FROM ``$DatabaseName``.``$table``) UNION ALL"
    }
    $query = ($queries -join "`n") + "`n(SELECT 'END', 0) LIMIT $($tables.Count);"
    $sourceCounts = Invoke-MySql -Sql $query -OptionFile $RootOptionFile

    $restoreQueries = foreach ($table in $tables) {
        "(SELECT '$table', COUNT(*) FROM ``$restoreDatabase``.``$table``) UNION ALL"
    }
    $restoreQuery = ($restoreQueries -join "`n") + "`n(SELECT 'END', 0) LIMIT $($tables.Count);"
    $restoreCounts = Invoke-MySql -Sql $restoreQuery -OptionFile $RootOptionFile

    if ($sourceCounts -ne $restoreCounts) {
        throw "恢复后的行数不一致。源库：`n$sourceCounts`n恢复库：`n$restoreCounts"
    }

    $null = Invoke-MySql -Sql "DROP DATABASE ``$restoreDatabase``;" -OptionFile $RootOptionFile
    $message = "[{0:yyyy-MM-dd HH:mm:ss}] 恢复演练成功：{1}" -f (Get-Date), [System.IO.Path]::GetFileName($resolvedBackup)
    Write-Host $message -ForegroundColor Green
    Add-Content -LiteralPath (Join-Path $LogRoot 'backup-restore-test.log') -Value $message -Encoding UTF8
}
finally {
    if ($databaseState.Started) {
        Stop-MySqlServer
    }
}
