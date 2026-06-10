using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YearbookViewer.Services;
using YearbookViewer.Models;
using System.Globalization;

namespace YearbookViewer.Pages;

public class BrowseModel : PageModel
{
    private readonly YearbookService _yearbookService;
    private readonly ILogger<BrowseModel> _logger;
    private readonly IConfiguration _configuration;

    public BrowseModel(YearbookService yearbookService, ILogger<BrowseModel> logger, IConfiguration configuration)
    {
        _yearbookService = yearbookService;
        _logger = logger;
        _configuration = configuration;
    }

    public YearbookInfo? Yearbook { get; private set; }
    public List<int> AvailableYears { get; private set; } = new();
    public int? PreviousYear { get; private set; }
    public int? NextYear { get; private set; }
    public double ClickZoomLevel { get; private set; } = 1.75;
    public string ClickZoomLevelValue => ClickZoomLevel.ToString("0.###", CultureInfo.InvariantCulture);

    public IActionResult OnGet(int? year)
    {
        if (!year.HasValue)
        {
            return RedirectToPage("/Index");
        }

        try
        {
            ClickZoomLevel = Math.Clamp(_configuration.GetValue("Viewer:ClickZoomLevel", 1.75), 1.0, 8.0);
            AvailableYears = _yearbookService.GetAvailableYears();
            Yearbook = _yearbookService.GetYearbook(year.Value);

            if (Yearbook == null)
            {
                _logger.LogWarning($"Yearbook not found for year {year}");
                return Page();
            }

            // Calculate navigation years
            var currentIndex = AvailableYears.IndexOf(year.Value);
            if (currentIndex > 0)
            {
                PreviousYear = AvailableYears[currentIndex - 1];
            }
            if (currentIndex < AvailableYears.Count - 1)
            {
                NextYear = AvailableYears[currentIndex + 1];
            }

            _logger.LogInformation($"Loaded yearbook {Yearbook.Title} with {Yearbook.TotalPages} pages");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading yearbook for year {year}");
        }

        return Page();
    }
}
