using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Security;
using LibraryAdmin.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryAdmin.Web.Pages.Books;

public sealed class DetailsModel : PageModel
{
    private readonly LibraryService _libraryService;

    public DetailsModel(LibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public Book BookDetail { get; private set; } = new();
    public IReadOnlyList<BookCopy> Copies { get; private set; } = [];
    public string? StatusMessage { get; private set; }

    [BindProperty]
    public BorrowInput Borrow { get; set; } = new();

    [BindProperty]
    public CopyInput NewCopy { get; set; } = new();

    [BindProperty]
    public CopyInput EditCopy { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(long id)
    {
        return await LoadAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostBorrowAsync(long id)
    {
        ModelState.Clear();
        if (!TryValidateModel(Borrow, nameof(Borrow)))
        {
            await LoadAsync(id);
            return Page();
        }

        var (result, _) = await _libraryService.BorrowAsync(
            User.GetUserId(),
            Borrow.CopyId,
            Borrow.DueDays,
            Borrow.Notes);
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Books/Details", new { id });
    }

    public async Task<IActionResult> OnPostAddCopyAsync(long id)
    {
        if (!User.IsInRole("Admin"))
        {
            return Forbid();
        }

        ModelState.Clear();
        if (!TryValidateModel(NewCopy, nameof(NewCopy)))
        {
            await LoadAsync(id);
            return Page();
        }

        var (result, _) = await _libraryService.SaveCopyAsync(null, id, NewCopy);
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Books/Details", new { id });
    }

    public async Task<IActionResult> OnPostUpdateCopyAsync(long id, long copyId)
    {
        if (!User.IsInRole("Admin"))
        {
            return Forbid();
        }

        ModelState.Clear();
        if (!TryValidateModel(EditCopy, nameof(EditCopy)))
        {
            TempData["StatusMessage"] = "馆藏编号是必填项，且不能超过 40 个字符。";
            return RedirectToPage("/Books/Details", new { id });
        }

        var result = await _libraryService.SaveCopyAsync(copyId, id, EditCopy);
        TempData["StatusMessage"] = result.Result.Message;
        return RedirectToPage("/Books/Details", new { id });
    }

    public async Task<IActionResult> OnPostToggleCopyAsync(long id, long copyId, bool disabled)
    {
        if (!User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var result = await _libraryService.SetCopyDisabledAsync(copyId, disabled);
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Books/Details", new { id });
    }

    public async Task<IActionResult> OnPostDeleteCopyAsync(long id, long copyId)
    {
        if (!User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var result = await _libraryService.DeleteCopyAsync(copyId);
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Books/Details", new { id });
    }

    public async Task<IActionResult> OnPostArchiveAsync(long id, bool archived)
    {
        if (!User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var result = await _libraryService.SetBookArchivedAsync(id, archived);
        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Books/Details", new { id });
    }

    public async Task<IActionResult> OnPostDeleteBookAsync(long id)
    {
        if (!User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var result = await _libraryService.DeleteBookAsync(id);
        TempData["StatusMessage"] = result.Message;
        return result.Succeeded
            ? RedirectToPage("/Books/Index")
            : RedirectToPage("/Books/Details", new { id });
    }

    private async Task<bool> LoadAsync(long id)
    {
        var book = await _libraryService.GetBookAsync(id);
        if (book is null)
        {
            return false;
        }

        BookDetail = book;
        Copies = await _libraryService.GetCopiesAsync(id);
        return true;
    }
}
