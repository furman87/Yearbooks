using Microsoft.AspNetCore.Mvc.RazorPages;
using YearbookViewer.Models;
using YearbookViewer.Services;

namespace YearbookViewer.Pages;

public class SearchModel : PageModel
{
    private readonly YearbookSearchService _searchService;
    private readonly YearbookService _yearbookService;

    public SearchModel(YearbookSearchService searchService, YearbookService yearbookService)
    {
        _searchService = searchService;
        _yearbookService = yearbookService;
    }

    public YearbookSearchResponse Search { get; private set; } = new();
    public YearbookInfo? ScopedYearbook { get; private set; }

    public void OnGet(string? query, int? year)
    {
        if (year.HasValue)
        {
            ScopedYearbook = _yearbookService.GetYearbook(year.Value, includeDimensions: false);
        }

        Search = _searchService.Search(query, year);
    }
}
