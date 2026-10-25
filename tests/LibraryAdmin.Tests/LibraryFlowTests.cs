using LibraryAdmin.Web.Data;
using LibraryAdmin.Web.Formatting;
using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Security;
using LibraryAdmin.Web.Services;
using LibraryAdmin.Web.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;

namespace LibraryAdmin.Tests;

public sealed class LibraryFlowTests : IAsyncLifetime
{
    private MySqlConnectionFactory _factory = null!;
    private PasswordService _passwords = null!;
    private AccountService _accounts = null!;
    private LibraryService _library = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__LibraryAdmin");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("集成测试需要 ConnectionStrings__LibraryAdmin。请通过 scripts/run-tests.ps1 执行。");
        }

        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (!builder.Database.EndsWith("_test", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("测试连接必须使用独立的 _test 数据库。");
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LibraryAdmin"] = builder.ConnectionString
            })
            .Build();

        _factory = new MySqlConnectionFactory(configuration);
        _passwords = new PasswordService(new PasswordHasher<UserAccount>());
        _accounts = new AccountService(_factory, _passwords);
        _library = new LibraryService(_factory);

        await using var connection = await _factory.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SET FOREIGN_KEY_CHECKS = 0;
            DROP TABLE IF EXISTS loans;
            DROP TABLE IF EXISTS admin_applications;
            DROP TABLE IF EXISTS book_copies;
            DROP TABLE IF EXISTS books;
            DROP TABLE IF EXISTS user_accounts;
            DROP TABLE IF EXISTS schema_versions;
            SET FOREIGN_KEY_CHECKS = 1;
            """;
        await command.ExecuteNonQueryAsync();

        var initializer = new DatabaseInitializer(
            _factory,
            _passwords,
            NullLogger<DatabaseInitializer>.Instance);
        await initializer.InitializeAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task BootstrapAdmin_IsHashed_AndCanAuthenticate()
    {
        var (account, error) = await _accounts.AuthenticateAsync("admin", "123456");

        Assert.Null(error);
        Assert.NotNull(account);
        Assert.True(account.IsBootstrapAdmin);
        Assert.True(account.MustChangePassword);
        Assert.NotEqual("123456", account.PasswordHash);
    }

    [Fact]
    public async Task Borrowing_EnforcesOneActiveLoan_AndPreservesHistory()
    {
        var user = await RegisterReaderAsync("reader1", "读者一");
        var bookId = await CreateBookAsync("978000000001", "测试图书");
        var (copyResult, copyId) = await _library.SaveCopyAsync(
            null, bookId, new CopyInput { CopyCode = "COPY-001", ShelfLocation = "A-01" });
        Assert.True(copyResult.Succeeded, copyResult.Message);
        var otherBookId = await CreateBookAsync("978000000002", "另一本测试图书");
        var wrongBookUpdate = await _library.SaveCopyAsync(
            copyId,
            otherBookId,
            new CopyInput { CopyCode = "COPY-WRONG", ShelfLocation = "Z-01" });
        Assert.False(wrongBookUpdate.Result.Succeeded);

        var (firstBorrow, _) = await _library.BorrowAsync(user.Id, copyId, 30, "首次借阅");
        Assert.True(firstBorrow.Succeeded, firstBorrow.Message);

        var (secondBorrow, _) = await _library.BorrowAsync(user.Id, copyId, 14, null);
        Assert.False(secondBorrow.Succeeded);

        var book = await _library.GetBookAsync(bookId);
        Assert.Equal(0, book!.AvailableCount);

        var loans = await _library.SearchLoansAsync(user.Id, null, "active", 1);
        var loan = Assert.Single(loans.Items);
        var returned = await _library.ReturnAsync(loan.Id, user.Id, requesterIsAdmin: false);
        Assert.True(returned.Succeeded, returned.Message);

        var returnedAgain = await _library.ReturnAsync(loan.Id, user.Id, requesterIsAdmin: false);
        Assert.True(returnedAgain.Succeeded);

        var copyDeletion = await _library.DeleteCopyAsync(copyId);
        Assert.False(copyDeletion.Succeeded);

        var bootstrap = Assert.Single(
            (await _accounts.SearchUsersAsync("admin", 1, 20)).Items,
            account => account.IsBootstrapAdmin);
        var userDeletion = await _accounts.DeleteUserAsync(
            bootstrap.Id, actorIsBootstrap: true, targetId: user.Id);
        Assert.False(userDeletion.Succeeded);

        var disableAccount = await _accounts.SetDisabledAsync(
            bootstrap.Id, actorIsBootstrap: true, targetId: user.Id, disabled: true);
        Assert.True(disableAccount.Succeeded, disableAccount.Message);
    }

    [Fact]
    public async Task AdminApproval_AllowsAtMostFourActiveAdmins_AndOnlyBootstrapCanApprove()
    {
        var bootstrap = Assert.Single(
            (await _accounts.SearchUsersAsync("admin", 1, 20)).Items,
            account => account.IsBootstrapAdmin);

        var candidates = new List<UserAccount>();
        for (var index = 1; index <= 4; index++)
        {
            var candidate = await RegisterReaderAsync($"candidate{index}", $"候选管理员{index}", requestAdmin: true);
            candidates.Add(candidate);
        }

        var applications = await _accounts.GetAdminApplicationsAsync();
        var pending = applications.Where(application => application.Status == ApplicationStatus.Pending)
            .OrderBy(application => application.Id)
            .ToList();
        Assert.Equal(4, pending.Count);

        var adminApproval = await _accounts.ReviewAdminApplicationAsync(
            pending[0].Id,
            candidates[1].Id,
            reviewerIsBootstrap: false,
            approve: true);
        Assert.False(adminApproval.Succeeded);

        for (var index = 0; index < 3; index++)
        {
            var approval = await _accounts.ReviewAdminApplicationAsync(
                pending[index].Id,
                bootstrap.Id,
                reviewerIsBootstrap: true,
                approve: true);
            Assert.True(approval.Succeeded, approval.Message);
        }

        var fourthApproval = await _accounts.ReviewAdminApplicationAsync(
            pending[3].Id,
            bootstrap.Id,
            reviewerIsBootstrap: true,
            approve: true);
        Assert.False(fourthApproval.Succeeded);

        var overCapacityRegistration = await _accounts.RegisterAsync(new RegistrationInput
        {
            Username = "candidate5",
            DisplayName = "第五管理员",
            Password = "password8",
            ConfirmPassword = "password8",
            RegistrationType = "Admin"
        }, requestAdmin: true);
        Assert.True(overCapacityRegistration.Succeeded, overCapacityRegistration.Message);
        Assert.Contains("名额已满", overCapacityRegistration.Message);
        var downgraded = Assert.Single(
            (await _accounts.SearchUsersAsync("candidate5", 1, 20)).Items,
            account => account.Username == "candidate5");
        Assert.Equal(AccountRole.Reader, downgraded.Role);
        Assert.Equal(2, (await _accounts.GetAdminApplicationsAsync()).Count(application =>
            application.Status == ApplicationStatus.Pending));
    }

    [Fact]
    public async Task AdminApproval_DoesNotEnableDisabledApplicant()
    {
        var bootstrap = Assert.Single(
            (await _accounts.SearchUsersAsync("admin", 1, 20)).Items,
            account => account.IsBootstrapAdmin);
        var applicant = await RegisterReaderAsync("disabled_admin", "停用申请人", requestAdmin: true);
        var application = Assert.Single(
            await _accounts.GetAdminApplicationsAsync(),
            item => item.UserId == applicant.Id && item.Status == ApplicationStatus.Pending);

        var disabled = await _accounts.SetDisabledAsync(
            bootstrap.Id,
            actorIsBootstrap: true,
            applicant.Id,
            disabled: true);
        Assert.True(disabled.Succeeded, disabled.Message);

        var approval = await _accounts.ReviewAdminApplicationAsync(
            application.Id,
            bootstrap.Id,
            reviewerIsBootstrap: true,
            approve: true);
        Assert.False(approval.Succeeded);

        var unchanged = await _accounts.GetByIdAsync(applicant.Id);
        Assert.NotNull(unchanged);
        Assert.True(unchanged.IsDisabled);
        Assert.Equal(AccountRole.Reader, unchanged.Role);
    }

    [Fact]
    public async Task Database_UsesUtcTimestamps_AndAppliesVersionedMigrations()
    {
        await RegisterReaderAsync("utc_reader", "UTC 读者");
        var account = Assert.Single(
            (await _accounts.SearchUsersAsync("utc_reader", 1, 20)).Items,
            item => item.Username == "utc_reader");

        Assert.Equal(DateTimeKind.Utc, account.CreatedAt.Kind);
        Assert.InRange((DateTime.UtcNow - account.CreatedAt).Duration(), TimeSpan.Zero, TimeSpan.FromMinutes(2));

        await using var connection = await _factory.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_versions ORDER BY version;";
        var versions = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            versions.Add(reader.GetInt32(0));
        }
        Assert.Contains(1, versions);
        Assert.Contains(2, versions);
    }

    [Fact]
    public void LoanPolicy_RejectsPeriodsOutsideOneToThirtyDays()
    {
        var borrowedDate = new DateOnly(2026, 10, 23);
        Assert.Equal(new DateOnly(2026, 11, 22), LoanPolicy.CalculateDueDate(borrowedDate, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanPolicy.CalculateDueDate(borrowedDate, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanPolicy.CalculateDueDate(borrowedDate, 31));
    }

    [Theory]
    [InlineData("121212", true)]
    [InlineData("读者_01", true)]
    [InlineData("reader-01", true)]
    [InlineData("12", false)]
    [InlineData("reader name", false)]
    [InlineData("user@example", false)]
    public void UsernameRules_AcceptNumbersAndRejectInvalidCharacters(string username, bool expected)
    {
        Assert.Equal(expected, UsernameRules.IsValid(username));
    }

    [Fact]
    public void LocalTimeDisplay_ConvertsUtcExactlyOnce()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        var utc = new DateTime(2026, 10, 23, 10, 30, 0, DateTimeKind.Utc);
        Assert.Equal("2026-10-23 18:30", LocalTimeDisplay.Format(utc, zone));
    }

    private async Task<UserAccount> RegisterReaderAsync(
        string username,
        string displayName,
        bool requestAdmin = false)
    {
        var result = await _accounts.RegisterAsync(new RegistrationInput
        {
            Username = username,
            DisplayName = displayName,
            Password = "password8",
            ConfirmPassword = "password8",
            RegistrationType = requestAdmin ? "Admin" : "Reader"
        }, requestAdmin);
        Assert.True(result.Succeeded, result.Message);

        var page = await _accounts.SearchUsersAsync(username, 1, 20);
        return Assert.Single(page.Items, account => account.Username == username);
    }

    private async Task<long> CreateBookAsync(string isbn, string title)
    {
        var (result, id) = await _library.SaveBookAsync(null, new BookInput
        {
            Isbn = isbn,
            Title = title,
            Author = "测试作者",
            Category = "测试",
            Publisher = "测试出版社",
            PublicationYear = 2026
        });
        Assert.True(result.Succeeded, result.Message);
        return id;
    }
}
