using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Security;
using LibraryAdmin.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryAdmin.Web.Pages.Admin;

[Authorize(Policy = "BootstrapAdmin")]
public sealed class AdminApplicationsModel : PageModel
{
    private readonly AccountService _accountService;

    public AdminApplicationsModel(AccountService accountService)
    {
        _accountService = accountService;
    }

    public IReadOnlyList<AdminApplication> Applications { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Applications = await _accountService.GetAdminApplicationsAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(long applicationId)
    {
        var result = await _accountService.ReviewAdminApplicationAsync(
            applicationId,
            User.GetUserId(),
            User.IsBootstrapAdmin(),
            approve: true);
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Admin/AdminApplications");
    }

    public async Task<IActionResult> OnPostRejectAsync(long applicationId)
    {
        var result = await _accountService.ReviewAdminApplicationAsync(
            applicationId,
            User.GetUserId(),
            User.IsBootstrapAdmin(),
            approve: false);
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Admin/AdminApplications");
    }
}
