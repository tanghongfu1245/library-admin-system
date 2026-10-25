using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Security;
using LibraryAdmin.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryAdmin.Web.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    private readonly AccountService _accountService;

    public LoginModel(AccountService accountService)
    {
        _accountService = accountService;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true
        ? RedirectToPage("/Index")
        : Page();

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (account, error) = await _accountService.AuthenticateAsync(Input.Username, Input.Password);
        if (account is null)
        {
            ErrorMessage = error;
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimsFactory.Create(account),
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Index");
    }
}
