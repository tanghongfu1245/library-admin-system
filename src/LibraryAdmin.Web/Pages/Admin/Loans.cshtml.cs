using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Security;
using LibraryAdmin.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryAdmin.Web.Pages.Admin;

[Authorize(Roles = "Admin")]
public sealed class LoansModel : PageModel
{
    private readonly LibraryService _libraryService;

    public LoansModel(LibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public PagedResult<Loan> Loans { get; private set; } = new([], 1, 20, 0);
    public PaginationView Pagination { get; private set; } = new("/Admin/Loans", 1, 1);
    public IReadOnlyList<BorrowerOption> Borrowers { get; private set; } = [];
    public IReadOnlyList<AvailableCopyOption> AvailableCopies { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Status { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty]
    public long BorrowerId { get; set; }

    [BindProperty]
    public BorrowInput BorrowForm { get; set; } = new();

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostBorrowAsync()
    {
        if (BorrowerId <= 0)
        {
            ModelState.AddModelError(nameof(BorrowerId), "请选择借阅人。");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var (result, _) = await _libraryService.BorrowAsync(
            BorrowerId,
            BorrowForm.CopyId,
            BorrowForm.DueDays,
            BorrowForm.Notes);
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Admin/Loans");
    }

    public async Task<IActionResult> OnPostReturnAsync(long loanId)
    {
        var result = await _libraryService.ReturnAsync(
            loanId, User.GetUserId(), requesterIsAdmin: true);
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Admin/Loans");
    }

    public async Task<IActionResult> OnPostVoidAsync(long loanId)
    {
        var result = await _libraryService.VoidActiveLoanAsync(loanId);
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Admin/Loans");
    }

    private async Task LoadAsync()
    {
        Loans = await _libraryService.SearchLoansAsync(null, Query, Status, PageNumber);
        Pagination = new PaginationView(
            "/Admin/Loans", Loans.Page, Loans.TotalPages, Query, Status);
        Borrowers = await _libraryService.GetBorrowersAsync();
        AvailableCopies = await _libraryService.GetAvailableCopiesAsync();
    }
}
