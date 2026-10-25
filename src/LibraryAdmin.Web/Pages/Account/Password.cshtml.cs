using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Security;
using LibraryAdmin.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryAdmin.Web.Pages.Account;

public sealed class PasswordModel : PageModel
{
    private readonly AccountService _accountService;

    public PasswordModel(AccountService accountService)
    {
        _accountService = accountService;
    }

    [BindProperty]
    public ChangePasswordInput Input { get; set; } = new();

    public string? StatusMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _accountService.ChangePasswordAsync(User.GetUserId(), Input);
        StatusMessage = result.Message;
        if (!result.Succeeded)
        {
            return Page();
        }

        await RefreshSignInAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSkipAsync()
    {
        await _accountService.DismissPasswordChangeAsync(User.GetUserId());
        await RefreshSignInAsync();
        TempData["StatusMessage"] = "已选择稍后修改默认密码。";
        return RedirectToPage("/Index");
    }

    private async Task RefreshSignInAsync()
    {
        var account = await _accountService.GetByIdAsync(User.GetUserId());
        if (account is not null)
        {
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                ClaimsFactory.Create(account));
        }
    }
}
