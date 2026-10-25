using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Security;
using LibraryAdmin.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryAdmin.Web.Pages.Account;

public sealed class ProfileModel : PageModel
{
    private readonly AccountService _accountService;

    public ProfileModel(AccountService accountService)
    {
        _accountService = accountService;
    }

    [BindProperty]
    public ProfileInput Input { get; set; } = new();

    public string Username { get; private set; } = "";
    public string RoleText { get; private set; } = "";
    public string? StatusMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        return await LoadLabelsAsync() ? Page() : Challenge();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadLabelsAsync();
            return Page();
        }

        var result = await _accountService.UpdateProfileAsync(User.GetUserId(), Input);
        StatusMessage = result.Message;
        if (result.Succeeded)
        {
            var account = await _accountService.GetByIdAsync(User.GetUserId());
            if (account is not null)
            {
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    ClaimsFactory.Create(account));
            }
        }

        await LoadLabelsAsync();
        return Page();
    }

    private async Task<bool> LoadLabelsAsync()
    {
        var account = await _accountService.GetByIdAsync(User.GetUserId());
        if (account is null)
        {
            return false;
        }

        Username = account.Username;
        RoleText = account.Role == AccountRole.Admin ? "管理员" : "读者";
        Input = new ProfileInput
        {
            DisplayName = Input.DisplayName.Length > 0 ? Input.DisplayName : account.DisplayName,
            Email = Input.Email ?? account.Email,
            Phone = Input.Phone ?? account.Phone
        };
        return true;
    }
}
