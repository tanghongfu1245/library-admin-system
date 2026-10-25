using System.Reflection;
using System.Text.RegularExpressions;
using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Security;
using MySqlConnector;

namespace LibraryAdmin.Web.Data;

public sealed class DatabaseInitializer
{
    private readonly MySqlConnectionFactory _connectionFactory;
    private readonly PasswordService _passwordService;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        MySqlConnectionFactory connectionFactory,
        PasswordService passwordService,
        ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _passwordService = passwordService;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        var assembly = Assembly.GetExecutingAssembly();
        var schemaScript = ReadEmbeddedScript(assembly, "Database.Schema.sql");
        await ExecuteStatementsAsync(connection, schemaScript, null, cancellationToken);
        await ApplyMigrationsAsync(connection, assembly, cancellationToken);

        await EnsureBootstrapAdminAsync(connection, cancellationToken);
        _logger.LogInformation("数据库结构检查完成。");
    }

    private async Task ApplyMigrationsAsync(
        MySqlConnection connection,
        Assembly assembly,
        CancellationToken cancellationToken)
    {
        var appliedVersions = new HashSet<int>();
        await using (var versionsCommand = connection.CreateCommand())
        {
            versionsCommand.CommandText = "SELECT version FROM schema_versions;";
            await using var reader = await versionsCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                appliedVersions.Add(reader.GetInt32(0));
            }
        }

        var migrations = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Database.Migrations.", StringComparison.Ordinal))
            .Select(name => new
            {
                ResourceName = name,
                Version = ParseMigrationVersion(name)
            })
            .Where(item => item.Version > 0)
            .OrderBy(item => item.Version);

        foreach (var migration in migrations)
        {
            if (appliedVersions.Contains(migration.Version))
            {
                continue;
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var script = ReadEmbeddedScript(assembly, migration.ResourceName);
            await ExecuteStatementsAsync(connection, script, transaction, cancellationToken);

            await using var recordCommand = connection.CreateCommand();
            recordCommand.Transaction = transaction;
            recordCommand.CommandText = "INSERT INTO schema_versions (version) VALUES (@version);";
            recordCommand.Parameters.AddWithValue("@version", migration.Version);
            await recordCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("已应用数据库迁移版本 {Version}。", migration.Version);
        }
    }

    private static string ReadEmbeddedScript(Assembly assembly, string suffix)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"找不到嵌入式脚本：{suffix}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static async Task ExecuteStatementsAsync(
        MySqlConnection connection,
        string script,
        MySqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        foreach (var statement in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static int ParseMigrationVersion(string resourceName)
    {
        var match = Regex.Match(resourceName, @"Migration[s]?\.(\d+)_", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var version)
            ? version
            : 0;
    }

    private async Task EnsureBootstrapAdminAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = """
            SELECT COUNT(*)
            FROM user_accounts
            WHERE is_bootstrap_admin = TRUE;
            """;
        var count = Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken));
        if (count > 0)
        {
            return;
        }

        await using var usernameCheck = connection.CreateCommand();
        usernameCheck.CommandText = "SELECT COUNT(*) FROM user_accounts WHERE username = 'admin';";
        if (Convert.ToInt32(await usernameCheck.ExecuteScalarAsync(cancellationToken)) > 0)
        {
            throw new InvalidOperationException("数据库中已存在非初始管理员的 admin 账号，无法安全创建引导管理员。");
        }

        var seedUser = new UserAccount { Username = "admin" };
        var passwordHash = _passwordService.Hash("123456");

        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO user_accounts
                (username, display_name, password_hash, role, is_disabled,
                 must_change_password, is_bootstrap_admin)
            VALUES
                ('admin', '系统管理员', @passwordHash, 'Admin', FALSE, TRUE, TRUE);
            """;
        insert.Parameters.AddWithValue("@passwordHash", passwordHash);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogWarning("已创建初始管理员 admin，请首次登录后修改默认密码。");
    }
}
