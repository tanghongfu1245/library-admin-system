using LibraryAdmin.Web.Data;
using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Security;
using LibraryAdmin.Web.Validation;
using Microsoft.AspNetCore.Identity;
using MySqlConnector;

namespace LibraryAdmin.Web.Services;

public sealed class AccountService
{
    private const int MaximumActiveAdmins = 4;
    private readonly MySqlConnectionFactory _connectionFactory;
    private readonly PasswordService _passwordService;

    public AccountService(MySqlConnectionFactory connectionFactory, PasswordService passwordService)
    {
        _connectionFactory = connectionFactory;
        _passwordService = passwordService;
    }

    public async Task<UserAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{AccountSelectSql} WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapAccount(reader) : null;
    }

    public async Task<(UserAccount? Account, string? Error)> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{AccountSelectSql} WHERE username = @username";
        command.Parameters.AddWithValue("@username", username.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (null, "用户名或密码不正确。");
        }

        var account = MapAccount(reader);
        if (account.IsDisabled)
        {
            return (null, "该账号已停用，请联系管理员。");
        }

        var verification = _passwordService.Verify(account, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return (null, "用户名或密码不正确。");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            await reader.DisposeAsync();
            await UpdatePasswordHashAsync(account.Id, _passwordService.Hash(password), cancellationToken);
        }

        return (account, null);
    }

    public async Task<OperationResult> RegisterAsync(
        RegistrationInput input,
        bool requestAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!UsernameRules.IsValid(input.Username))
        {
            return OperationResult.Fail(UsernameRules.Message + "。");
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var adminCapacityFull = false;

        try
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO user_accounts
                    (username, display_name, email, phone, password_hash, role,
                     is_disabled, must_change_password, is_bootstrap_admin)
                VALUES
                    (@username, @displayName, NULLIF(@email, ''), NULLIF(@phone, ''),
                     @passwordHash, 'Reader', FALSE, FALSE, FALSE);
                """;
            insert.Parameters.AddWithValue("@username", input.Username.Trim());
            insert.Parameters.AddWithValue("@displayName", input.DisplayName.Trim());
            insert.Parameters.AddWithValue("@email", input.Email?.Trim() ?? "");
            insert.Parameters.AddWithValue("@phone", input.Phone?.Trim() ?? "");
            insert.Parameters.AddWithValue("@passwordHash", _passwordService.Hash(input.Password));
            await insert.ExecuteNonQueryAsync(cancellationToken);

            await using var idCommand = connection.CreateCommand();
            idCommand.Transaction = transaction;
            idCommand.CommandText = "SELECT LAST_INSERT_ID();";
            var userId = Convert.ToInt64(await idCommand.ExecuteScalarAsync(cancellationToken));

            if (requestAdmin)
            {
                await using var countCommand = connection.CreateCommand();
                countCommand.Transaction = transaction;
                countCommand.CommandText = """
                    SELECT COUNT(*)
                    FROM user_accounts
                    WHERE role = 'Admin' AND is_disabled = FALSE;
                    """;
                adminCapacityFull = Convert.ToInt32(
                    await countCommand.ExecuteScalarAsync(cancellationToken)) >= MaximumActiveAdmins;

                await using var application = connection.CreateCommand();
                application.Transaction = transaction;
                application.CommandText = """
                    INSERT INTO admin_applications (user_id, status)
                    VALUES (@userId, 'Pending');
                    """;
                application.Parameters.AddWithValue("@userId", userId);
                await application.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            if (requestAdmin && adminCapacityFull)
            {
                return OperationResult.Ok(
                    "管理员名额已满，账号已作为普通读者创建；管理员申请保留为待审批，名额空出后可批准。");
            }

            return OperationResult.Ok(requestAdmin
                ? "注册申请已提交，初始 admin 审批后才具备管理员权限。"
                : "注册成功，请登录。");
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail("用户名或邮箱已被使用。");
        }
    }

    public async Task<OperationResult> CreateReaderAsync(
        RegistrationInput input,
        CancellationToken cancellationToken = default)
    {
        input.RegistrationType = "Reader";
        return await RegisterAsync(input, false, cancellationToken);
    }

    public async Task<OperationResult> UpdateProfileAsync(
        long userId,
        ProfileInput input,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE user_accounts
                SET display_name = @displayName,
                    email = NULLIF(@email, ''),
                    phone = NULLIF(@phone, '')
                WHERE id = @id AND is_disabled = FALSE;
                """;
            command.Parameters.AddWithValue("@displayName", input.DisplayName.Trim());
            command.Parameters.AddWithValue("@email", input.Email?.Trim() ?? "");
            command.Parameters.AddWithValue("@phone", input.Phone?.Trim() ?? "");
            command.Parameters.AddWithValue("@id", userId);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1
                ? OperationResult.Ok("个人资料已更新。")
                : OperationResult.Fail("账号不存在或已停用。");
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return OperationResult.Fail("该邮箱已被其他账号使用。");
        }
    }

    public async Task<OperationResult> ChangePasswordAsync(
        long userId,
        ChangePasswordInput input,
        CancellationToken cancellationToken = default)
    {
        var account = await GetByIdAsync(userId, cancellationToken);
        if (account is null)
        {
            return OperationResult.Fail("账号不存在。");
        }

        if (_passwordService.Verify(account, input.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            return OperationResult.Fail("当前密码不正确。");
        }

        await UpdatePasswordHashAsync(userId, _passwordService.Hash(input.NewPassword), cancellationToken);
        return OperationResult.Ok("密码已修改。");
    }

    public async Task DismissPasswordChangeAsync(long userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE user_accounts SET must_change_password = FALSE WHERE id = @id;";
        command.Parameters.AddWithValue("@id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PagedResult<UserAccount>> SearchUsersAsync(
        string? query,
        int page,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        var normalized = query?.Trim() ?? "";
        var where = """
            WHERE (@query = '' OR username LIKE CONCAT('%', @query, '%')
                   OR display_name LIKE CONCAT('%', @query, '%')
                   OR email LIKE CONCAT('%', @query, '%'))
            """;

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var count = connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM user_accounts {where}";
        count.Parameters.AddWithValue("@query", normalized);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken));

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {AccountColumns}
            FROM user_accounts
            {where}
            ORDER BY is_bootstrap_admin DESC, created_at DESC
            LIMIT @pageSize OFFSET @offset;
            """;
        command.Parameters.AddWithValue("@query", normalized);
        command.Parameters.AddWithValue("@pageSize", pageSize);
        command.Parameters.AddWithValue("@offset", (page - 1) * pageSize);

        var users = new List<UserAccount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(MapAccount(reader));
        }

        return new PagedResult<UserAccount>(users, page, pageSize, total);
    }

    public async Task<OperationResult> SetDisabledAsync(
        long actorId,
        bool actorIsBootstrap,
        long targetId,
        bool disabled,
        CancellationToken cancellationToken = default)
    {
        var target = await GetByIdAsync(targetId, cancellationToken);
        if (target is null)
        {
            return OperationResult.Fail("账号不存在。");
        }

        if (target.IsBootstrapAdmin)
        {
            return OperationResult.Fail("初始 admin 不允许停用。");
        }

        if (target.Role == AccountRole.Admin && !actorIsBootstrap)
        {
            return OperationResult.Fail("只有初始 admin 可以调整管理员账号。");
        }

        if (targetId == actorId)
        {
            return OperationResult.Fail("不能停用当前登录账号。");
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE user_accounts SET is_disabled = @disabled WHERE id = @id;";
        command.Parameters.AddWithValue("@disabled", disabled);
        command.Parameters.AddWithValue("@id", targetId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return OperationResult.Ok(disabled ? "账号已停用。" : "账号已启用。");
    }

    public async Task<OperationResult> DeleteUserAsync(
        long actorId,
        bool actorIsBootstrap,
        long targetId,
        CancellationToken cancellationToken = default)
    {
        var target = await GetByIdAsync(targetId, cancellationToken);
        if (target is null)
        {
            return OperationResult.Fail("账号不存在。");
        }

        if (target.IsBootstrapAdmin)
        {
            return OperationResult.Fail("初始 admin 不允许删除。");
        }

        if (target.Role == AccountRole.Admin && !actorIsBootstrap)
        {
            return OperationResult.Fail("只有初始 admin 可以删除管理员账号。");
        }

        if (targetId == actorId)
        {
            return OperationResult.Fail("不能删除当前登录账号。");
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var lockUser = connection.CreateCommand();
        lockUser.Transaction = transaction;
        lockUser.CommandText = "SELECT id FROM user_accounts WHERE id = @id FOR UPDATE;";
        lockUser.Parameters.AddWithValue("@id", targetId);
        if (await lockUser.ExecuteScalarAsync(cancellationToken) is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail("账号不存在。");
        }

        await using var history = connection.CreateCommand();
        history.Transaction = transaction;
        history.CommandText = "SELECT COUNT(*) FROM loans WHERE user_id = @id;";
        history.Parameters.AddWithValue("@id", targetId);
        if (Convert.ToInt32(await history.ExecuteScalarAsync(cancellationToken)) > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail("该账号已有借阅历史，只能停用，不能删除。");
        }

        try
        {
            await using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM user_accounts WHERE id = @id;";
            delete.Parameters.AddWithValue("@id", targetId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult.Ok("账号已永久删除。");
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail("该账号仍被借阅记录引用，只能停用，不能删除。");
        }
    }

    public async Task<IReadOnlyList<AdminApplication>> GetAdminApplicationsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.id, a.user_id, u.username, u.display_name, a.status,
                   a.submitted_at, a.reviewed_at, reviewer.username AS reviewed_by
            FROM admin_applications a
            JOIN user_accounts u ON u.id = a.user_id
            LEFT JOIN user_accounts reviewer ON reviewer.id = a.reviewed_by_user_id
            ORDER BY (a.status = 'Pending') DESC, a.submitted_at DESC;
            """;
        var applications = new List<AdminApplication>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applications.Add(new AdminApplication
            {
                Id = reader.GetInt64("id"),
                UserId = reader.GetInt64("user_id"),
                Username = reader.GetString("username"),
                DisplayName = reader.GetString("display_name"),
                Status = Enum.Parse<ApplicationStatus>(reader.GetString("status")),
                SubmittedAt = reader.GetDateTime("submitted_at"),
                ReviewedAt = reader.GetNullableDateTime("reviewed_at"),
                ReviewedBy = reader.GetNullableString("reviewed_by")
            });
        }

        return applications;
    }

    public async Task<OperationResult> ReviewAdminApplicationAsync(
        long applicationId,
        long reviewerId,
        bool reviewerIsBootstrap,
        bool approve,
        CancellationToken cancellationToken = default)
    {
        if (!reviewerIsBootstrap)
        {
            return OperationResult.Fail("只有初始 admin 可以审批管理员申请。");
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var applicationCommand = connection.CreateCommand();
        applicationCommand.Transaction = transaction;
        applicationCommand.CommandText = """
            SELECT a.user_id, a.status, u.is_disabled
            FROM admin_applications a
            JOIN user_accounts u ON u.id = a.user_id
            WHERE a.id = @id
            FOR UPDATE;
            """;
        applicationCommand.Parameters.AddWithValue("@id", applicationId);
        long userId;
        string status;
        bool userDisabled;
        await using (var reader = await applicationCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult.Fail("申请不存在。");
            }

            userId = reader.GetInt64("user_id");
            status = reader.GetString("status");
            userDisabled = reader.GetBoolean("is_disabled");
        }

        if (status != "Pending")
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail("该申请已经处理。");
        }

        if (approve)
        {
            if (userDisabled)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult.Fail("申请人账号已停用，请先启用账号再审批。");
            }

            await using var adminCount = connection.CreateCommand();
            adminCount.Transaction = transaction;
            adminCount.CommandText = """
                SELECT id
                FROM user_accounts
                WHERE role = 'Admin' AND is_disabled = FALSE
                FOR UPDATE;
                """;
            var activeAdmins = 0;
            await using (var adminReader = await adminCount.ExecuteReaderAsync(cancellationToken))
            {
                while (await adminReader.ReadAsync(cancellationToken))
                {
                    activeAdmins++;
                }
            }
            if (activeAdmins >= MaximumActiveAdmins)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult.Fail("管理员名额已满（最多 4 名）。");
            }

            await using var promote = connection.CreateCommand();
            promote.Transaction = transaction;
            promote.CommandText = """
                UPDATE user_accounts
                SET role = 'Admin'
                WHERE id = @userId;
                """;
            promote.Parameters.AddWithValue("@userId", userId);
            await promote.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var review = connection.CreateCommand();
        review.Transaction = transaction;
        review.CommandText = """
            UPDATE admin_applications
            SET status = @status, reviewed_at = UTC_TIMESTAMP(6), reviewed_by_user_id = @reviewerId
            WHERE id = @id;
            """;
        review.Parameters.AddWithValue("@status", approve ? "Approved" : "Rejected");
        review.Parameters.AddWithValue("@reviewerId", reviewerId);
        review.Parameters.AddWithValue("@id", applicationId);
        await review.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok(approve
            ? "管理员申请已批准，权限会在该用户下一次页面操作时生效。"
            : "管理员申请已拒绝。");
    }

    private async Task UpdatePasswordHashAsync(long userId, string hash, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE user_accounts
            SET password_hash = @hash, must_change_password = FALSE
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@hash", hash);
        command.Parameters.AddWithValue("@id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static UserAccount MapAccount(MySqlDataReader reader) => new()
    {
        Id = reader.GetInt64("id"),
        Username = reader.GetString("username"),
        DisplayName = reader.GetString("display_name"),
        Email = reader.GetNullableString("email"),
        Phone = reader.GetNullableString("phone"),
        PasswordHash = reader.GetString("password_hash"),
        Role = Enum.Parse<AccountRole>(reader.GetString("role")),
        IsDisabled = reader.GetBoolean("is_disabled"),
        MustChangePassword = reader.GetBoolean("must_change_password"),
        IsBootstrapAdmin = reader.GetBoolean("is_bootstrap_admin"),
        CreatedAt = reader.GetDateTime("created_at"),
        UpdatedAt = reader.GetDateTime("updated_at")
    };

    private const string AccountColumns = """
        id, username, display_name, email, phone, password_hash, role,
        is_disabled, must_change_password, is_bootstrap_admin, created_at, updated_at
        """;

    private const string AccountSelectSql = $"""
        SELECT {AccountColumns}
        FROM user_accounts
        """;
}
