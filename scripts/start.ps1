. (Join-Path $PSScriptRoot 'common.ps1')

$mutex = New-Object System.Threading.Mutex($false, 'Local\LibraryAdmin.Launcher')
if (-not $mutex.WaitOne(0)) {
    if (Test-LibraryHealth) {
        Open-LibraryBrowser
    }
    else {
        Write-Host '图书管理员系统正在启动，请稍后刷新浏览器。' -ForegroundColor Yellow
    }
    exit 0
}

$appProcess = $null
$startedMysql = $false
try {
    Set-ProjectDotnetEnvironment
    $database = Initialize-MySqlDatabase
    $startedMysql = $database.Started
    $env:ConnectionStrings__LibraryAdmin = Get-LibraryConnectionString -Config $database.Config
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:ASPNETCORE_URLS = 'http://127.0.0.1:8080'

    $publishedDll = Join-Path $ProjectRoot '.publish\LibraryAdmin.Web.dll'
    $publishRequired = -not (Test-Path -LiteralPath $publishedDll)
    if (-not $publishRequired) {
        $publishTime = (Get-Item -LiteralPath $publishedDll).LastWriteTimeUtc
        $sourceRoot = Join-Path $ProjectRoot 'src\LibraryAdmin.Web'
        $newestSource = Get-ChildItem -LiteralPath $sourceRoot -File -Recurse |
            Where-Object {
                $_.FullName -notmatch '\\(bin|obj)\\' -and
                $_.Extension -in @('.cs', '.cshtml', '.csproj', '.json', '.sql', '.css')
            } |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        $publishRequired = $null -ne $newestSource -and $newestSource.LastWriteTimeUtc -gt $publishTime
    }

    if ($publishRequired) {
        & (Join-Path $PSScriptRoot 'build.ps1')
    }

    if ((Test-TcpPort -Port 8080) -and -not (Test-LibraryHealth)) {
        throw '端口 8080 已被其他程序占用。'
    }

    if (-not (Test-LibraryHealth)) {
        $dotnet = Join-Path $DotnetHome 'dotnet.exe'
        $publishDirectory = Join-Path $ProjectRoot '.publish'
        $logPath = Join-Path $LogRoot ("app-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))
        $errorLogPath = Join-Path $LogRoot ("app-error-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))
        $appProcess = Start-Process -FilePath $dotnet `
            -ArgumentList $publishedDll `
            -WorkingDirectory $publishDirectory `
            -NoNewWindow `
            -RedirectStandardOutput $logPath `
            -RedirectStandardError $errorLogPath `
            -PassThru
        $appProcess.Id | Set-Content -LiteralPath $AppPidFile -Encoding ascii

        $deadline = (Get-Date).AddSeconds(60)
        while ((Get-Date) -lt $deadline -and -not (Test-LibraryHealth)) {
            if ($appProcess.HasExited) {
                throw "应用启动失败，退出码 $($appProcess.ExitCode)。请检查 .logs 中的 MySQL 日志。"
            }
            Start-Sleep -Milliseconds 500
        }
        if (-not (Test-LibraryHealth)) {
            throw '应用未能在 60 秒内通过健康检查。'
        }
    }

    Write-Host '' 
    Write-Host '图书管理员系统已启动：http://localhost:8080' -ForegroundColor Green
    Write-Host '初始管理员：admin  初始密码：123456（首次登录后会提示修改）' -ForegroundColor Yellow
    Write-Host '关闭本窗口或按 Ctrl+C 可停止本次启动的进程。' -ForegroundColor DarkGray
    Open-LibraryBrowser

    if ($appProcess) {
        Wait-Process -Id $appProcess.Id
    }
    else {
        while (Test-TcpPort -Port 8080) {
            Start-Sleep -Seconds 2
        }
    }
}
finally {
    if ($appProcess -and -not $appProcess.HasExited) {
        Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
    }
    Remove-FileIfExists -Path $AppPidFile
    if ($startedMysql) {
        Stop-MySqlServer
    }
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
