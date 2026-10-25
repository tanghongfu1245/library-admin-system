using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Security;
using LibraryAdmin.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryAdmin.Web.Pages.Loans;

public sealed class MineModel : PageModel
{
    private readonly LibraryService _libraryService;

    public MineModel(LibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public PagedResult<Loan> Loans { get; private set; } = new([], 1, 20, 0);
    public PaginationView Pagination { get; private set; } = new("/Loans/Mine", 1, 1);

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Status { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public async Task OnGetAsync()
    {
        Loans = await _libraryService.SearchLoansAsync(
            User.GetUserId(), Query, Status, PageNumber);
        Pagination = new PaginationView(
            "/Loans/Mine", Loans.Page, Loans.TotalPages, Query, Status);
    }

    public async Task<IActionResult> OnPostReturnAsync(long loanId)
    {
        var result = await _libraryService.ReturnAsync(
            loanId, User.GetUserId(), User.IsAdmin());
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Loans/Mine");
    }
}
