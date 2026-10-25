$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:ProjectRoot = Split-Path -Parent $PSScriptRoot
$script:ProjectAlias = 'E:\library-admin-runtime'
if (-not (Test-Path -LiteralPath $ProjectAlias)) {
    New-Item -ItemType Junction -Path $ProjectAlias -Target $ProjectRoot | Out-Null
}
$aliasItem = Get-Item -LiteralPath $ProjectAlias -Force
if (-not $aliasItem.Target -or ($aliasItem.Target -notcontains $ProjectRoot)) {
    throw "英文目录别名 $ProjectAlias 未指向当前项目目录。"
}

$script:ToolRoot = Join-Path $ProjectAlias '.tools'
$script:DotnetHome = Join-Path $ToolRoot 'dotnet'
$script:MysqlHome = Join-Path $ToolRoot 'mysql'
$script:DataRoot = Join-Path $ProjectAlias '.data'
$script:MysqlData = Join-Path $DataRoot 'mysql'
$script:RuntimeRoot = Join-Path $ProjectAlias '.runtime'
$script:LogRoot = Join-Path $ProjectAlias '.logs'
$script:CredentialFile = Join-Path $RuntimeRoot 'db-credentials.json'
$script:RootOptionFile = Join-Path $RuntimeRoot 'mysql-root.cnf'
$script:MysqlPidFile = Join-Path $RuntimeRoot 'mysql.pid'
$script:AppPidFile = Join-Path $RuntimeRoot 'app.pid'
$script:DatabaseProvisionedMarker = Join-Path $RuntimeRoot 'database-provisioned'
$script:DatabaseName = 'library_admin'
$script:DatabaseUser = 'library_app'
$script:MySqlPort = 3306

function Initialize-ProjectDirectories {
    $directories = @(
        $DataRoot,
        $RuntimeRoot,
        $LogRoot,
        (Join-Path $ProjectRoot '.publish')
    )
    foreach ($directory in $directories) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $identity,
        [System.Security.AccessControl.FileSystemRights]::FullControl,
        [System.Security.AccessControl.AccessControlType]::Allow)
    $acl.AddAccessRule($rule)
    Set-Acl -LiteralPath $RuntimeRoot -AclObject $acl
    Get-ChildItem -LiteralPath $RuntimeRoot -Force -Recurse -ErrorAction SilentlyContinue |
        ForEach-Object { Set-Acl -LiteralPath $_.FullName -AclObject $acl }
}

function New-RandomPassword {
    $alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    $bytes = New-Object byte[] 32
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $random.GetBytes($bytes)
    }
    finally {
        $random.Dispose()
    }
    $characters = for ($i = 0; $i -lt $bytes.Length; $i++) {
        $alphabet[$bytes[$i] % $alphabet.Length]
    }
    return -join $characters
}

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Content
    )
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Remove-FileIfExists {
    param([string]$Path)
    if ($Path -and (Test-Path -LiteralPath $Path)) {
        Remove-Item -LiteralPath $Path -Force
    }
}

function Get-OrCreateDatabaseConfig {
    Initialize-ProjectDirectories
    if (Test-Path -LiteralPath $CredentialFile) {
        $config = Get-Content -LiteralPath $CredentialFile -Raw | ConvertFrom-Json
        if (-not $config.rootPassword -or -not $config.password) {
            throw "数据库凭据文件不完整：$CredentialFile"
        }
        return $config
    }

    $config = [PSCustomObject]@{
        database = $DatabaseName
        user = $DatabaseUser
        password = New-RandomPassword
        rootPassword = New-RandomPassword
        port = $MySqlPort
    }
    Write-Utf8NoBom -Path $CredentialFile -Content ($config | ConvertTo-Json)
    $optionContent = @"
[client]
user=root
password=$($config.rootPassword)
host=127.0.0.1
port=$MySqlPort
protocol=tcp
default-character-set=utf8mb4
"@
    Write-Utf8NoBom -Path $RootOptionFile -Content $optionContent
    return $config
}

function Get-LibraryConnectionString {
    param($Config)
    return "Server=127.0.0.1;Port=$($Config.port);Database=$($Config.database);User ID=$($Config.user);Password=$($Config.password);SslMode=Preferred;AllowUserVariables=True;DefaultCommandTimeout=30;DateTimeKind=Utc"
}

function Test-TcpPort {
    param(
        [int]$Port,
        [string]$HostName = '127.0.0.1'
    )
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $result = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $result.AsyncWaitHandle.WaitOne(500)) {
            return $false
        }
        $client.EndConnect($result)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Wait-TcpPort {
    param(
        [int]$Port,
        [int]$TimeoutSeconds = 45,
        [string]$Description = '服务'
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-TcpPort -Port $Port) {
            return
        }
        Start-Sleep -Milliseconds 300
    }
    throw "$Description 未能在 $TimeoutSeconds 秒内监听端口 $Port。"
}

function Invoke-MySql {
    param(
        [string]$Sql,
        [string]$OptionFile,
        [switch]$WithoutPassword
    )
    $mysql = Join-Path $MysqlHome 'bin\mysql.exe'
    $argumentParts = @()
    if ($OptionFile) {
        $argumentParts += "--defaults-extra-file=$($OptionFile -replace '\\','/')"
    }
    $argumentParts += @(
        '--protocol=tcp',
        '--host=127.0.0.1',
        "--port=$MySqlPort",
        '--batch',
        '--skip-column-names'
    )
    if ($WithoutPassword) {
        $argumentParts += '--user=root'
    }

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $mysql
    $startInfo.Arguments = $argumentParts -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $inputWriter = New-Object System.IO.StreamWriter($process.StandardInput.BaseStream, $utf8)
    $inputError = $null
    try {
        $inputWriter.Write($Sql)
    }
    catch {
        $inputError = $_
    }
    finally {
        $inputWriter.Close()
    }
    $outputReader = New-Object System.IO.StreamReader($process.StandardOutput.BaseStream, $utf8)
    $errorReader = New-Object System.IO.StreamReader($process.StandardError.BaseStream, $utf8)
    $output = $outputReader.ReadToEnd()
    $errorOutput = $errorReader.ReadToEnd()
    $outputReader.Close()
    $errorReader.Close()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        $detail = if ($errorOutput) { $errorOutput } elseif ($inputError) { $inputError.Exception.Message } else { '未知错误' }
        throw "MySQL 客户端执行失败：$detail"
    }
    return $output.Trim()
}

function Start-MySqlServer {
    if (Test-TcpPort -Port $MySqlPort) {
        throw "端口 $MySqlPort 已被其他程序占用。请先关闭占用程序，避免误连到其他数据库。"
    }

    $mysqld = Join-Path $MysqlHome 'bin\mysqld.exe'
    $errorLog = Join-Path $LogRoot 'mysql-error.log'
    $arguments = @(
        "--basedir=$($MysqlHome -replace '\\','/')",
        "--datadir=$($MysqlData -replace '\\','/')",
        "--port=$MySqlPort",
        '--bind-address=127.0.0.1',
        '--default-time-zone=+00:00',
        '--character-set-server=utf8mb4',
        '--collation-server=utf8mb4_0900_ai_ci',
        "--log-error=$($errorLog -replace '\\','/')",
        "--pid-file=$($MysqlPidFile -replace '\\','/')",
        '--console'
    )
    $process = Start-Process -FilePath $mysqld `
        -ArgumentList $arguments `
        -WorkingDirectory $MysqlHome `
        -WindowStyle Hidden `
        -PassThru
    $process.Id | Set-Content -LiteralPath $MysqlPidFile -Encoding ascii
    Wait-TcpPort -Port $MySqlPort -Description 'MySQL'
    return $process
}

function Initialize-MySqlDatabase {
    $config = Get-OrCreateDatabaseConfig
    $needsProvision = -not (Test-Path -LiteralPath $DatabaseProvisionedMarker)
    $isNew = -not (Test-Path -LiteralPath (Join-Path $MysqlData 'mysql'))

    if ($isNew) {
        New-Item -ItemType Directory -Path $MysqlData -Force | Out-Null
        $initializationLog = Join-Path $LogRoot 'mysql-initialize.log'
        $mysqld = Join-Path $MysqlHome 'bin\mysqld.exe'
        & $mysqld `
            --initialize-insecure `
            "--basedir=$($MysqlHome -replace '\\','/')" `
            "--datadir=$($MysqlData -replace '\\','/')" `
            "--log-error=$($initializationLog -replace '\\','/')"
        if ($LASTEXITCODE -ne 0) {
            throw "MySQL 数据目录初始化失败，请查看 $initializationLog"
        }
    }

    $started = $false
    if (-not (Test-TcpPort -Port $MySqlPort)) {
        $null = Start-MySqlServer
        $started = $true
    }

    try {
        if ($needsProvision) {
            $setupSql = @"
CREATE DATABASE IF NOT EXISTS ``$DatabaseName`` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS '$DatabaseUser'@'127.0.0.1' IDENTIFIED BY '$($config.password)';
ALTER USER '$DatabaseUser'@'127.0.0.1' IDENTIFIED BY '$($config.password)';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES ON ``$DatabaseName``.* TO '$DatabaseUser'@'127.0.0.1';
ALTER USER 'root'@'localhost' IDENTIFIED BY '$($config.rootPassword)';
FLUSH PRIVILEGES;
"@
            try {
                $null = Invoke-MySql -Sql $setupSql -WithoutPassword
            }
            catch {
                $repairSql = @"
CREATE DATABASE IF NOT EXISTS ``$DatabaseName`` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS '$DatabaseUser'@'127.0.0.1' IDENTIFIED BY '$($config.password)';
ALTER USER '$DatabaseUser'@'127.0.0.1' IDENTIFIED BY '$($config.password)';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES ON ``$DatabaseName``.* TO '$DatabaseUser'@'127.0.0.1';
FLUSH PRIVILEGES;
"@
                $null = Invoke-MySql -Sql $repairSql -OptionFile $RootOptionFile
            }
            Set-Content -LiteralPath $DatabaseProvisionedMarker -Value '1' -Encoding ascii
        }
        else {
            try {
                $null = Invoke-MySql -Sql 'SELECT 1;' -OptionFile (New-AppOptionFile -Config $config)
            }
            catch {
                $repairSql = @"
CREATE USER IF NOT EXISTS '$DatabaseUser'@'127.0.0.1' IDENTIFIED BY '$($config.password)';
ALTER USER '$DatabaseUser'@'127.0.0.1' IDENTIFIED BY '$($config.password)';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES ON ``$DatabaseName``.* TO '$DatabaseUser'@'127.0.0.1';
FLUSH PRIVILEGES;
"@
                $null = Invoke-MySql -Sql $repairSql -OptionFile $RootOptionFile
            }
        }

        $ensureSql = @"
CREATE DATABASE IF NOT EXISTS ``$DatabaseName`` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE DATABASE IF NOT EXISTS ``${DatabaseName}_test`` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES ON ``$DatabaseName``.* TO '$DatabaseUser'@'127.0.0.1';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES, DROP ON ``${DatabaseName}_test``.* TO '$DatabaseUser'@'127.0.0.1';
FLUSH PRIVILEGES;
"@
        $null = Invoke-MySql -Sql $ensureSql -OptionFile $RootOptionFile
    }
    catch {
        if ($started) {
            Stop-MySqlServer
        }
        throw
    }

    return [PSCustomObject]@{
        Config = $config
        Started = $started
        WasNew = $isNew
    }
}

function New-AppOptionFile {
    param($Config)
    $appOptionFile = Join-Path $RuntimeRoot 'mysql-app.cnf'
    $content = @"
[client]
user=$($Config.user)
password=$($Config.password)
host=127.0.0.1
port=$($Config.port)
protocol=tcp
default-character-set=utf8mb4
"@
    Write-Utf8NoBom -Path $appOptionFile -Content $content
    return $appOptionFile
}

function Invoke-MySqlFile {
    param(
        [string]$InputFile,
        [string]$OptionFile,
        [string]$Database
    )
    $mysql = Join-Path $MysqlHome 'bin\mysql.exe'
    $argumentParts = @(
        "--defaults-extra-file=$($OptionFile -replace '\\','/')",
        "--database=$Database",
        '--default-character-set=utf8mb4'
    )
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $mysql
    $startInfo.Arguments = $argumentParts -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void]$process.Start()

    $inputStream = [System.IO.File]::OpenRead($InputFile)
    try {
        $inputStream.CopyTo($process.StandardInput.BaseStream)
    }
    finally {
        $inputStream.Dispose()
        $process.StandardInput.Close()
    }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $outputReader = New-Object System.IO.StreamReader($process.StandardOutput.BaseStream, $utf8)
    $errorReader = New-Object System.IO.StreamReader($process.StandardError.BaseStream, $utf8)
    $output = $outputReader.ReadToEnd()
    $errorOutput = $errorReader.ReadToEnd()
    $outputReader.Close()
    $errorReader.Close()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "MySQL 导入失败：$errorOutput"
    }
    return $output.Trim()
}

function Stop-MySqlServer {
    if (-not (Test-TcpPort -Port $MySqlPort)) {
        Remove-FileIfExists -Path $MysqlPidFile
        return
    }

    $mysqladmin = Join-Path $MysqlHome 'bin\mysqladmin.exe'
    if (Test-Path -LiteralPath $RootOptionFile) {
        $shutdownArguments = @(
            "--defaults-extra-file=$($RootOptionFile -replace '\\','/')",
            'shutdown'
        )
        Start-Process -FilePath $mysqladmin `
            -ArgumentList $shutdownArguments `
            -WindowStyle Hidden `
            -Wait | Out-Null
    }

    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline -and (Test-TcpPort -Port $MySqlPort)) {
        Start-Sleep -Milliseconds 250
    }

    if (Test-TcpPort -Port $MySqlPort -and (Test-Path -LiteralPath $MysqlPidFile)) {
        $savedPid = [int](Get-Content -LiteralPath $MysqlPidFile -Raw)
        $process = Get-Process -Id $savedPid -ErrorAction SilentlyContinue
        if ($process -and $process.Path -like "$MysqlHome\*") {
            Stop-Process -Id $savedPid -Force
        }
    }
    Remove-FileIfExists -Path $MysqlPidFile
}

function Set-ProjectDotnetEnvironment {
    $env:DOTNET_ROOT = $DotnetHome
    $env:PATH = "$DotnetHome;$env:PATH"
    $env:NUGET_PACKAGES = Join-Path $ProjectRoot '.packages'
    $env:DOTNET_CLI_HOME = Join-Path $RuntimeRoot 'dotnet-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME -Force | Out-Null
}

function Test-LibraryHealth {
    try {
        $response = Invoke-RestMethod -Uri 'http://127.0.0.1:8080/health' -TimeoutSec 2
        return $response.status -eq 'ok'
    }
    catch {
        return $false
    }
}

function Open-LibraryBrowser {
    Start-Process 'http://localhost:8080'
}
