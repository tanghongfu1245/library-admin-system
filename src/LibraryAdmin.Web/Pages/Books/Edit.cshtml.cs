using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryAdmin.Web.Pages.Books;

[Authorize(Roles = "Admin")]
public sealed class EditModel : PageModel
{
    private readonly LibraryService _libraryService;

    public EditModel(LibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    [BindProperty]
    public BookInput Input { get; set; } = new();

    public long? BookId { get; private set; }
    public bool IsNew => BookId is null;

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        if (id is null)
        {
            return Page();
        }

        var book = await _libraryService.GetBookAsync(id.Value);
        if (book is null)
        {
            return NotFound();
        }

        BookId = book.Id;
        Input = new BookInput
        {
            Isbn = book.Isbn,
            Title = book.Title,
            Author = book.Author,
            Category = book.Category,
            Publisher = book.Publisher,
            PublicationYear = book.PublicationYear,
            Description = book.Description
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long? id)
    {
        BookId = id;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (result, bookId) = await _libraryService.SaveBookAsync(id, Input);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return Page();
        }

        TempData["StatusMessage"] = result.Message;
        return RedirectToPage("/Books/Details", new { id = bookId });
    }
}
