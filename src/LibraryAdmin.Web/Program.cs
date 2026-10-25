using LibraryAdmin.Web.Data;
using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Security;
using LibraryAdmin.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["App:Url"] ?? "http://127.0.0.1:8080");

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Register");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AuthorizePage("/Admin/AdminApplications", "BootstrapAdmin");
});

builder.Services.AddSingleton<MySqlConnectionFactory>();
builder.Services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<LibraryService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Error";
        options.Cookie.Name = "LibraryAdmin.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var userIdValue = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!long.TryParse(userIdValue, out var userId))
                {
                    context.RejectPrincipal();
                    return;
                }

                var accountService = context.HttpContext.RequestServices.GetRequiredService<AccountService>();
                var account = await accountService.GetByIdAsync(userId, context.HttpContext.RequestAborted);
                if (account is null || account.IsDisabled)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                var currentRole = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                var currentDisplayName = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value;
                var mustChange = context.Principal?.FindFirst("must_change_password")?.Value == "true";
                if (currentRole != account.Role.ToString() ||
                    currentDisplayName != account.DisplayName ||
                    mustChange != account.MustChangePassword)
                {
                    context.ReplacePrincipal(ClaimsFactory.Create(account));
                    context.ShouldRenew = true;
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser().RequireRole(AccountRole.Admin.ToString()));
    options.AddPolicy("BootstrapAdmin", policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole(AccountRole.Admin.ToString())
            .RequireClaim("bootstrap_admin", "true"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (MySqlConnectionFactory factory, CancellationToken cancellationToken) =>
{
    await using var connection = await factory.OpenAsync(cancellationToken);
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT 1;";
    return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1
        ? Results.Ok(new { status = "ok" })
        : Results.Problem("database unavailable");
}).AllowAnonymous();

app.MapRazorPages();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>()
        .InitializeAsync();
}

await app.RunAsync();

public partial class Program
{
}
