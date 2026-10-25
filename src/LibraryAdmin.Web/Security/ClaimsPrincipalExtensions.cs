using System.Security.Claims;
using LibraryAdmin.Web.Models;

namespace LibraryAdmin.Web.Security;

public static class ClaimsPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("当前登录用户缺少有效编号。");
    }

    public static string GetDisplayName(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.GivenName) ?? principal.Identity?.Name ?? "用户";

    public static bool IsAdmin(this ClaimsPrincipal principal) =>
        principal.IsInRole(AccountRole.Admin.ToString());

    public static bool IsBootstrapAdmin(this ClaimsPrincipal principal) =>
        principal.HasClaim("bootstrap_admin", "true");

    public static bool MustChangePassword(this ClaimsPrincipal principal) =>
        principal.HasClaim("must_change_password", "true");
}
