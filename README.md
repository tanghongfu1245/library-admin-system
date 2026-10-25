# 图书管理员系统

一个本地运行的图书管理 Web 应用，使用 ASP.NET Core Razor Pages + MySQL，实现图书、实体副本、读者账号、借阅/归还和管理员审批。

适合作为课程作业、个人练习或小型图书角管理工具。应用默认只监听本机 `127.0.0.1`，不对外网开放。

仓库地址：<https://github.com/tanghongfu1245/library-admin-system>。

## 功能

- 读者注册、登录、修改密码
- 图书查询、图书详情、分类浏览
- 实体副本管理（同一本书可以有多个副本）
- 借书、还书、借阅历史
- 借阅期限 1～30 天，默认 30 天，借出后不可修改
- 管理员审批读者提交的管理员申请
- 管理员管理图书、副本、读者账号
- 账号/副本有借阅历史时只能停用；图书有历史副本时只能归档
- 初始管理员 `admin`，首次登录提示修改密码
- 数据库结构自动初始化，版本化迁移
- 可选：每日自动备份、每周恢复演练（Windows 脚本）

## 技术栈

| 组件 | 版本/说明 |
|---|---|
| .NET | `net10.0` |
| Web 框架 | ASP.NET Core Razor Pages |
| 数据库 | MySQL 8.0+（开发验证使用 8.4.9） |
| 数据访问 | MySqlConnector 2.6.2（原生 SQL，无 EF Core） |
| 认证 | Cookie Authentication |
| 密码 | ASP.NET Core `PasswordHasher` |
| 测试 | xUnit + Microsoft.AspNetCore.Mvc.Testing |
| 前端 | Razor Pages + Bootstrap + jQuery（本地静态文件） |

## 目录结构

```text
.
├─ src/
│  └─ LibraryAdmin.Web/
│     ├─ Data/                 # 连接工厂、数据库初始化、Schema、迁移
│     ├─ Models/               # 实体模型
│     ├─ Pages/                # Razor Pages
│     ├─ Security/             # 登录、Cookie、Claims
│     ├─ Services/             # 账号、图书、借阅业务
│     ├─ Validation/           # 表单校验
│     ├─ wwwroot/              # 静态资源
│     ├─ Program.cs
│     └─ appsettings.json
├─ tests/
│  └─ LibraryAdmin.Tests/      # 集成测试
├─ scripts/                    # Windows 便捷脚本（可选）
├─ LibraryAdmin.slnx
├─ global.json                 # 固定 .NET SDK 版本
└─ README.md
```

## 环境要求

### 必需

- **.NET SDK 10.0.402**
  - `global.json` 固定了 `10.0.402`，且 `rollForward: disable`。
  - 其他 10.0.x 版本会提示找不到兼容 SDK；可安装 exact 版本，或把 `global.json` 的 `rollForward` 改为 `latestFeature`。
- **MySQL 8.0 或更高版本**
  - Schema 使用 `utf8mb4_0900_ai_ci`，MySQL 5.7 不支持。
  - 需要有一个可创建数据库/表的账号；本地学习可以直接用 `root`。
- **可访问 NuGet 的网络**（首次还原依赖）
  - 仓库不包含 `.packages` 缓存。

### 可选

- Windows PowerShell 5.1+：仅 `scripts/` 下的便捷脚本需要；直接 `dotnet run` 不需要。
- 7-Zip：仅制作/解压运行包时需要。
- Visual Studio 2022/2026 或 VS Code：可选 IDE。

## 快速开始

### 1. 克隆仓库

```bash
git clone https://github.com/tanghongfu1245/library-admin-system.git
cd library-admin-system
```

### 2. 创建数据库

应用**不会自动创建数据库**，但会自动建表和执行迁移。先创建一个空库：

```bash
mysql -u root -p -e "CREATE DATABASE IF NOT EXISTS library_admin CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;"
```

如果使用专用账号，给该账号授予 `library_admin` 库的权限：

```sql
CREATE USER 'library_app'@'127.0.0.1' IDENTIFIED BY '替换成你的密码';
GRANT ALL PRIVILEGES ON library_admin.* TO 'library_app'@'127.0.0.1';
FLUSH PRIVILEGES;
```

### 3. 配置连接字符串

应用从 `ConnectionStrings:LibraryAdmin` 读取连接字符串。**推荐用环境变量**，不要把密码提交到 Git。

Windows PowerShell：

```powershell
$env:ConnectionStrings__LibraryAdmin = "Server=127.0.0.1;Port=3306;Database=library_admin;User ID=root;Password=你的密码;CharSet=utf8mb4;"
```

macOS / Linux：

```bash
export ConnectionStrings__LibraryAdmin="Server=127.0.0.1;Port=3306;Database=library_admin;User ID=root;Password=你的密码;CharSet=utf8mb4;"
```

> 注意：当前 `Program.cs` 没有加载 `appsettings.Local.json`，只写这个文件不会生效。请使用环境变量，或把连接字符串写到 `appsettings.Development.json`（不要提交真实密码）。

### 4. 还原并运行

```bash
dotnet restore
dotnet run --project ./src/LibraryAdmin.Web
```

Windows 路径：

```powershell
dotnet run --project .\src\LibraryAdmin.Web
```

启动后访问：

**http://127.0.0.1:8080**

默认绑定地址在 `Program.cs` 中为 `http://127.0.0.1:8080`，不是 `launchSettings.json` 里的 5149。

初始账号：

- 用户名：`admin`
- 密码：`123456`
- 首次登录会提示修改密码，也可以选择稍后修改；建议立即修改。

### 5. 停止

在运行窗口按 `Ctrl+C`。

## 数据库说明

- 应用启动时自动执行 `src/LibraryAdmin.Web/Database/Schema.sql`。
- 迁移脚本位于 `src/LibraryAdmin.Web/Database/Migrations/`，按版本号执行，记录在 `schema_versions` 表。
- 首次启动会创建默认管理员 `admin`。
- 连接字符串为空时，应用会抛出“数据库连接字符串为空”；数据库不存在时会报 `Unknown database`。
- 应用连接会设置 `SET SESSION time_zone = '+00:00'`，数据库时间统一按 UTC 存储，页面按本地时区显示。

## 测试

测试是集成测试，需要 MySQL。先创建测试库：

```bash
mysql -u root -p -e "CREATE DATABASE IF NOT EXISTS library_admin_test CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;"
```

设置测试连接字符串（数据库名必须以 `_test` 结尾）：

Windows PowerShell：

```powershell
$env:ConnectionStrings__LibraryAdmin = "Server=127.0.0.1;Port=3306;Database=library_admin_test;User ID=root;Password=你的密码;CharSet=utf8mb4;"
dotnet test
```

macOS / Linux：

```bash
export ConnectionStrings__LibraryAdmin="Server=127.0.0.1;Port=3306;Database=library_admin_test;User ID=root;Password=你的密码;CharSet=utf8mb4;"
dotnet test
```

说明：

- 测试库账号需要有建表、改表、索引、外键和 `DROP` 权限；用 `root` 最省事。
- 测试会重建/清理测试库中的表，不要指向正式库 `library_admin`。
- 仓库里的 `scripts/run-tests.ps1` 是 Windows + 本地 `.tools` 的便捷脚本，不是 GitHub clone 后的必需步骤。


## 常见问题

### 1. `A compatible .NET SDK was not found`

`global.json` 要求 `10.0.402`。安装该版本，或修改：

```json
{
  "sdk": {
    "version": "10.0.402",
    "rollForward": "latestFeature"
  }
}
```

### 2. `Unknown database 'library_admin'`

还没有建库。执行：

```sql
CREATE DATABASE library_admin CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
```

### 3. `Access denied for user ...`

连接字符串里的用户名/密码不对，或该账号没有 `library_admin` 库的权限。检查：

```sql
SHOW GRANTS FOR 'library_app'@'127.0.0.1';
```

### 4. 端口 8080 被占用

关闭占用 8080 的程序，或设置：

```powershell
$env:App__Url = "http://127.0.0.1:8081"
dotnet run --project .\src\LibraryAdmin.Web
```

### 5. MySQL 5.7 建表失败

Schema 使用 `utf8mb4_0900_ai_ci`，需要 MySQL 8.0+。升级 MySQL，或把 Schema/迁移中的排序规则统一改为 `utf8mb4_unicode_ci`（不推荐，需自行验证）。

### 6. 页面打开是 5149，不是 8080

`launchSettings.json` 里的 5149 只是 Visual Studio 的启动配置；`Program.cs` 使用 `UseUrls` 默认绑定 8080。以控制台输出为准。



## 安全建议

- 首次登录后立即修改 `admin` 密码。
- 不要把 `ConnectionStrings__LibraryAdmin`、`.runtime/`、`.backups/` 提交到 Git。
- 应用默认只监听 `127.0.0.1`；如需局域网访问，请自行评估风险并配置反向代理/防火墙。
- 公开仓库前检查 Git 历史中是否曾出现过密码或真实数据；如有，需重写历史并更换数据库密码。


