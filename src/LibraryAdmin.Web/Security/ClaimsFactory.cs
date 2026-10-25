using System.Security.Claims;
using LibraryAdmin.Web.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LibraryAdmin.Web.Security;

public static class ClaimsFactory
{
    public static ClaimsPrincipal Create(UserAccount account)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.GivenName, account.DisplayName),
            new(ClaimTypes.Role, account.Role.ToString()),
            new("must_change_password", account.MustChangePassword ? "true" : "false"),
            new("bootstrap_admin", account.IsBootstrapAdmin ? "true" : "false")
        };

        if (!string.IsNullOrWhiteSpace(account.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, account.Email));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
