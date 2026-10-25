using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Security;
using LibraryAdmin.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryAdmin.Web.Pages.Admin;

[Authorize(Roles = "Admin")]
public sealed class AccountsModel : PageModel
{
    private readonly AccountService _accountService;

    public AccountsModel(AccountService accountService)
    {
        _accountService = accountService;
    }

    public PagedResult<UserAccount> Users { get; private set; } = new([], 1, 20, 0);
    public PaginationView Pagination { get; private set; } = new("/Admin/Accounts", 1, 1);

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty]
    public RegistrationInput NewReader { get; set; } = new();

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var result = await _accountService.CreateReaderAsync(NewReader);
        TempData["StatusMessage"] = result.Message;
        return result.Succeeded
            ? RedirectToPage("/Admin/Accounts")
            : await ReturnWithErrorAsync(result.Message);
    }

    public async Task<IActionResult> OnPostToggleAsync(long targetId, bool disabled)
    {
        var result = await _accountService.SetDisabledAsync(
            User.GetUserId(),
            User.IsBootstrapAdmin(),
            targetId,
            disabled);
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Admin/Accounts");
    }

    public async Task<IActionResult> OnPostDeleteAsync(long targetId)
    {
        var result = await _accountService.DeleteUserAsync(
            User.GetUserId(), User.IsBootstrapAdmin(), targetId);
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Admin/Accounts");
    }

    private async Task LoadAsync()
    {
        Users = await _accountService.SearchUsersAsync(Query, PageNumber);
        Pagination = new PaginationView("/Admin/Accounts", Users.Page, Users.TotalPages, Query);
    }

    private async Task<IActionResult> ReturnWithErrorAsync(string message)
    {
        ModelState.AddModelError(string.Empty, message);
        await LoadAsync();
        return Page();
    }
}
