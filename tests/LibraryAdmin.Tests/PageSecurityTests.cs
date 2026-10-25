using System.Net;
using System.Text.RegularExpressions;
using LibraryAdmin.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryAdmin.Tests;

public sealed class PageSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PageSecurityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    [Fact]
    public async Task ProtectedPages_RedirectAnonymousUsersToLogin()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/Books/Index");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task LoginPost_WithoutAntiforgeryToken_IsRejected()
    {
        using var client = CreateClient();
        var response = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Username"] = "admin",
                ["Input.Password"] = "123456"
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reader_IsDeniedFromAdminPages()
    {
        using var client = CreateClient();
        var username = "reader_sec_" + Guid.NewGuid().ToString("N")[..8];

        var registerPage = await client.GetAsync("/Account/Register");
        var registerToken = await ReadAntiforgeryTokenAsync(registerPage);
        var registerResponse = await client.PostAsync(
            "/Account/Register",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Username"] = username,
                ["Input.DisplayName"] = "权限测试读者",
                ["Input.Email"] = "",
                ["Input.Phone"] = "",
                ["Input.Password"] = "readerpass8",
                ["Input.ConfirmPassword"] = "readerpass8",
                ["Input.RegistrationType"] = "Reader",
                ["__RequestVerificationToken"] = registerToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);

        var loginPage = await client.GetAsync("/Account/Login");
        var loginToken = await ReadAntiforgeryTokenAsync(loginPage);
        var loginResponse = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Username"] = username,
                ["Input.Password"] = "readerpass8",
                ["__RequestVerificationToken"] = loginToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var adminResponse = await client.GetAsync("/Admin/Loans");
        Assert.Equal(HttpStatusCode.Redirect, adminResponse.StatusCode);
        Assert.StartsWith("/Error", adminResponse.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task ExistingSession_PicksUpRoleChangeOnNextRequest()
    {
        using var client = CreateClient();
        var username = "role_refresh_" + Guid.NewGuid().ToString("N")[..8];

        var registerPage = await client.GetAsync("/Account/Register");
        var registerToken = await ReadAntiforgeryTokenAsync(registerPage);
        var registerResponse = await client.PostAsync(
            "/Account/Register",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Username"] = username,
                ["Input.DisplayName"] = "角色刷新测试",
                ["Input.Email"] = "",
                ["Input.Phone"] = "",
                ["Input.Password"] = "readerpass8",
                ["Input.ConfirmPassword"] = "readerpass8",
                ["Input.RegistrationType"] = "Reader",
                ["__RequestVerificationToken"] = registerToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);

        var loginPage = await client.GetAsync("/Account/Login");
        var loginToken = await ReadAntiforgeryTokenAsync(loginPage);
        var loginResponse = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Username"] = username,
                ["Input.Password"] = "readerpass8",
                ["__RequestVerificationToken"] = loginToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var factory = _factory.Services.GetRequiredService<MySqlConnectionFactory>();
        await using (var connection = await factory.OpenAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE user_accounts SET role = 'Admin' WHERE username = @username;";
            command.Parameters.AddWithValue("@username", username);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        var adminResponse = await client.GetAsync("/Admin/Loans");
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<string> ReadAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(match.Success, "页面未生成防伪令牌。");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
