. (Join-Path $PSScriptRoot 'common.ps1')

if (Test-Path -LiteralPath $AppPidFile) {
    $savedPid = [int](Get-Content -LiteralPath $AppPidFile -Raw)
    $process = Get-Process -Id $savedPid -ErrorAction SilentlyContinue
    if ($process -and $process.ProcessName -eq 'dotnet') {
        Stop-Process -Id $savedPid -Force
        Write-Host '应用已停止。' -ForegroundColor Green
    }
    Remove-FileIfExists -Path $AppPidFile
}

Stop-MySqlServer
Write-Host 'MySQL 已停止。' -ForegroundColor Green
