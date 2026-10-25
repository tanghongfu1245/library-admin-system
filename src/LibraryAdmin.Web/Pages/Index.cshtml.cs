using Microsoft.AspNetCore.Mvc.RazorPages;
using LibraryAdmin.Web.Models;
using LibraryAdmin.Web.Services;

namespace LibraryAdmin.Web.Pages;

public class IndexModel : PageModel
{
    private readonly LibraryService _libraryService;

    public IndexModel(LibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public DashboardStats Stats { get; private set; } = new(0, 0, 0, 0, 0, 0);
    public IReadOnlyList<Book> RecentBooks { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Stats = await _libraryService.GetDashboardStatsAsync();
        RecentBooks = (await _libraryService.SearchBooksAsync(null, false, 1, 5)).Items;
    }
}
