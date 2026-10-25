using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryAdmin.Web.Pages.Account;

[AllowAnonymous]
public sealed class RegisterModel : PageModel
{
    private readonly AccountService _accountService;

    public RegisterModel(AccountService accountService)
    {
        _accountService = accountService;
    }

    [BindProperty]
    public RegistrationInput Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.RegistrationType is not ("Reader" or "Admin"))
        {
            ModelState.AddModelError(nameof(Input.RegistrationType), "请选择账号类型。");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _accountService.RegisterAsync(Input, Input.RegistrationType == "Admin");
        if (!result.Succeeded)
        {
            ErrorMessage = result.Message;
            return Page();
        }

        TempData["LoginMessage"] = result.Message;
        return RedirectToPage("/Account/Login");
    }
}
