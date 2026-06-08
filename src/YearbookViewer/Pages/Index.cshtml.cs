using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YearbookViewer.Services;
using YearbookViewer.Models;

namespace YearbookViewer.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly YearbookService _yearbookService;
    private readonly IConfiguration _configuration;

    public IndexModel(ILogger<IndexModel> logger, YearbookService yearbookService, IConfiguration configuration)
    {
        _logger = logger;
        _yearbookService = yearbookService;
        _configuration = configuration;
    }

    public YearbookGallery Gallery { get; private set; } = new();
    public List<int> AvailableYears { get; private set; } = new();
    public string YearbookPath => _configuration.GetValue<string>("YearbookPath") ?? "..";

    public void OnGet()
    {
        try
        {
            Gallery = _yearbookService.GetAllYearbooks();
            AvailableYears = Gallery.Yearbooks.Select(y => y.Year).OrderBy(y => y).ToList();
            
            _logger.LogInformation($"Loaded {Gallery.TotalYearbooks} yearbooks with {Gallery.TotalPages} total pages");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading yearbook gallery");
        }
    }
}
