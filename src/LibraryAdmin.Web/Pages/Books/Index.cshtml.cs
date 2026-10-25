using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryAdmin.Web.Pages.Books;

public sealed class IndexModel : PageModel
{
    private readonly LibraryService _libraryService;

    public IndexModel(LibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public PagedResult<Book> Books { get; private set; } = new([], 1, 15, 0);
    public PaginationView Pagination { get; private set; } = new("/Books/Index", 1, 1);

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public bool IncludeArchived { get; set; }

    public async Task OnGetAsync()
    {
        if (!User.IsInRole("Admin"))
        {
            IncludeArchived = false;
        }

        Books = await _libraryService.SearchBooksAsync(Query, IncludeArchived, PageNumber);
        Pagination = new PaginationView(
            "/Books/Index",
            Books.Page,
            Books.TotalPages,
            Query,
            IncludeArchived: IncludeArchived);
    }
}
