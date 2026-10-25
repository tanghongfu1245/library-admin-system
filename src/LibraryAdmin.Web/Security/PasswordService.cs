using LibraryAdmin.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace LibraryAdmin.Web.Security;

public sealed class PasswordService
{
    private readonly IPasswordHasher<UserAccount> _hasher;

    public PasswordService(IPasswordHasher<UserAccount> hasher)
    {
        _hasher = hasher;
    }

    public string Hash(string password) =>
        _hasher.HashPassword(new UserAccount(), password);

    public PasswordVerificationResult Verify(UserAccount user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
}
