using MySqlConnector;

namespace LibraryAdmin.Web.Data;

public sealed class MySqlConnectionFactory
{
    public MySqlConnectionFactory(IConfiguration configuration)
    {
        var configuredConnectionString = configuration.GetConnectionString("LibraryAdmin")
            ?? throw new InvalidOperationException("缺少数据库连接字符串 ConnectionStrings:LibraryAdmin。");
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException("数据库连接字符串为空，请先运行初始化脚本。");
        }

        var builder = new MySqlConnectionStringBuilder(configuredConnectionString)
        {
            DateTimeKind = MySqlDateTimeKind.Utc
        };
        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }

    public async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SET SESSION time_zone = '+00:00';";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }
}
