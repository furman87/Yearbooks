using Microsoft.AspNetCore.Mvc;
using YearbookViewer.Services;
using YearbookViewer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace YearbookViewer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class YearbookController : ControllerBase
{
    private readonly YearbookService _yearbookService;
    private readonly ILogger<YearbookController> _logger;

    public YearbookController(YearbookService yearbookService, ILogger<YearbookController> logger)
    {
        _yearbookService = yearbookService;
        _logger = logger;
    }

    /// <summary>
    /// Get all available yearbooks
    /// </summary>
    [HttpGet]
    public ActionResult<YearbookGallery> GetAllYearbooks()
    {
        try
        {
            var gallery = _yearbookService.GetAllYearbooks();
            return Ok(gallery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving yearbooks");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get specific yearbook by year
    /// </summary>
    [HttpGet("{year:int}")]
    public ActionResult<YearbookInfo> GetYearbook(int year)
    {
        try
        {
            var yearbook = _yearbookService.GetYearbook(year);
            if (yearbook == null)
            {
                return NotFound($"Yearbook for year {year} not found");
            }

            return Ok(yearbook);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving yearbook for year {Year}", year);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get available years
    /// </summary>
    [HttpGet("years")]
    public ActionResult<List<int>> GetAvailableYears()
    {
        try
        {
            var years = _yearbookService.GetAvailableYears();
            return Ok(years);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available years");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Serve yearbook thumbnail images
    /// </summary>
    [HttpGet("{year:int}/thumbnail/{fileName}")]
    public async Task<IActionResult> GetThumbnail(int year, string fileName)
    {
        try
        {
            var thumbnailPath = _yearbookService.GetThumbnailPath(year, fileName);
            if (string.IsNullOrEmpty(thumbnailPath) || !System.IO.File.Exists(thumbnailPath))
            {
                return NotFound("Thumbnail not found");
            }

            var imageBytes = await System.IO.File.ReadAllBytesAsync(thumbnailPath);

            // Set caching headers for thumbnails
            Response.Headers["Cache-Control"] = "public, max-age=86400"; // Cache for 24 hours
            Response.Headers["Expires"] = DateTime.UtcNow.AddDays(1).ToString("R");

            return File(imageBytes, "image/jpeg", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving thumbnail {FileName} for year {Year}", fileName, year);
            return StatusCode(500, "Error serving thumbnail");
        }
    }

    /// <summary>
    /// Serve yearbook images with optional resizing
    /// </summary>
    [HttpGet("{year:int}/image/{fileName}")]
    public async Task<IActionResult> GetImage(int year, string fileName, [FromQuery] int? w, [FromQuery] int? h, [FromQuery] int quality = 85, [FromQuery] bool thumbnail = false)
    {
        try
        {
            string imagePath;
            
            // If thumbnail is requested or small dimensions are specified, try to use thumbnail first
            if (thumbnail || (w.HasValue && w.Value <= 400) || (h.HasValue && h.Value <= 400))
            {
                imagePath = _yearbookService.GetThumbnailPath(year, fileName);
            }
            else
            {
                imagePath = _yearbookService.GetImagePath(year, fileName);
            }

            if (string.IsNullOrEmpty(imagePath) || !System.IO.File.Exists(imagePath))
            {
                return NotFound("Image not found");
            }

            // Read the image file
            var imageBytes = await System.IO.File.ReadAllBytesAsync(imagePath);

            // If no resizing is requested, return the original
            if (!w.HasValue && !h.HasValue)
            {
                // Set appropriate caching headers
                var cacheMaxAge = thumbnail ? "public, max-age=86400" : "public, max-age=3600";
                Response.Headers["Cache-Control"] = cacheMaxAge;
                Response.Headers["Expires"] = DateTime.UtcNow.AddHours(thumbnail ? 24 : 1).ToString("R");
                
                return File(imageBytes, "image/jpeg", fileName);
            }

            // Resize the image
            try
            {
                using var image = Image.Load(imageBytes);
                
                // Calculate dimensions maintaining aspect ratio
                var (newWidth, newHeight) = CalculateNewDimensions(image.Width, image.Height, w, h);
                
                // Resize the image
                image.Mutate(x => x.Resize(newWidth, newHeight));
                
                // Convert to byte array
                using var ms = new MemoryStream();
                var encoder = new JpegEncoder { Quality = quality };
                await image.SaveAsync(ms, encoder);
                
                var resizedBytes = ms.ToArray();
                
                // Set caching headers
                Response.Headers["Cache-Control"] = "public, max-age=3600";
                Response.Headers["Expires"] = DateTime.UtcNow.AddHours(1).ToString("R");
                
                return File(resizedBytes, "image/jpeg", fileName);
            }
            catch (Exception resizeEx)
            {
                _logger.LogError(resizeEx, "Error resizing image {FileName} for year {Year}", fileName, year);
                // Fall back to original image
                return File(imageBytes, "image/jpeg", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving image {FileName} for year {Year}", fileName, year);
            return StatusCode(500, "Error serving image");
        }
    }

    private static (int width, int height) CalculateNewDimensions(int originalWidth, int originalHeight, int? targetWidth, int? targetHeight)
    {
        if (!targetWidth.HasValue && !targetHeight.HasValue)
        {
            return (originalWidth, originalHeight);
        }

        double aspectRatio = (double)originalWidth / originalHeight;

        if (targetWidth.HasValue && targetHeight.HasValue)
        {
            // Both dimensions specified - maintain aspect ratio and fit within bounds
            var ratioByWidth = (double)targetWidth.Value / originalWidth;
            var ratioByHeight = (double)targetHeight.Value / originalHeight;
            var ratio = Math.Min(ratioByWidth, ratioByHeight);

            return ((int)(originalWidth * ratio), (int)(originalHeight * ratio));
        }
        else if (targetWidth.HasValue)
        {
            // Only width specified
            var newHeight = (int)(targetWidth.Value / aspectRatio);
            return (targetWidth.Value, newHeight);
        }
        else
        {
            // Only height specified
            var newWidth = (int)(targetHeight!.Value * aspectRatio);
            return (newWidth, targetHeight.Value);
        }
    }
}