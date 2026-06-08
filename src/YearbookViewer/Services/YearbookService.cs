using YearbookViewer.Models;
using SixLabors.ImageSharp;
using System.Text.RegularExpressions;

namespace YearbookViewer.Services;

public class YearbookService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<YearbookService> _logger;
    private readonly string _yearbookPath;

    public YearbookService(IConfiguration configuration, ILogger<YearbookService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _yearbookPath = _configuration.GetValue<string>("YearbookPath") ?? "..";
    }

    public YearbookGallery GetAllYearbooks()
    {
        var gallery = new YearbookGallery();
        
        try
        {
            var baseDirectory = new DirectoryInfo(_yearbookPath);
            if (!baseDirectory.Exists)
            {
                _logger.LogWarning($"Yearbook directory not found: {_yearbookPath}");
                return gallery;
            }

            // Find all Bonhomie-* directories
            var yearbookDirs = baseDirectory.GetDirectories("Bonhomie-*")
                .OrderBy(d => d.Name)
                .ToList();

            foreach (var dir in yearbookDirs)
            {
                var yearbook = CreateYearbookInfo(dir, includeDimensions: false);
                if (yearbook != null)
                {
                    gallery.Yearbooks.Add(yearbook);
                }
            }

            _logger.LogInformation($"Found {gallery.TotalYearbooks} yearbooks with {gallery.TotalPages} total pages");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning yearbook directories");
        }

        return gallery;
    }

    public YearbookInfo? GetYearbook(int year, bool includeDimensions = true)
    {
        try
        {
            var dirPath = Path.Combine(_yearbookPath, $"Bonhomie-{year}");
            var dir = new DirectoryInfo(dirPath);
            
            if (!dir.Exists)
            {
                return null;
            }

            return CreateYearbookInfo(dir, includeDimensions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading yearbook for year {year}");
            return null;
        }
    }

    private YearbookInfo? CreateYearbookInfo(DirectoryInfo directory, bool includeDimensions)
    {
        try
        {
            // Extract year from directory name (e.g., "Bonhomie-1984" -> 1984)
            var match = Regex.Match(directory.Name, @"Bonhomie-(\d{4})");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int year))
            {
                _logger.LogWarning($"Could not extract year from directory name: {directory.Name}");
                return null;
            }

            var yearbook = new YearbookInfo
            {
                Year = year,
                Title = $"Bonhomie Volume {year}",
                DirectoryPath = directory.FullName
            };

            // Look for images in the "full" subdirectory first, then fall back to root directory
            var fullDirectory = new DirectoryInfo(Path.Combine(directory.FullName, "full"));
            var imageDirectory = fullDirectory.Exists ? fullDirectory : directory;

            // Get all JPG files from the appropriate directory
            var imageFiles = imageDirectory.GetFiles("*.jpg", SearchOption.TopDirectoryOnly)
                .Concat(imageDirectory.GetFiles("*.jpeg", SearchOption.TopDirectoryOnly))
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int pageNumber = 1;
            foreach (var file in imageFiles)
            {
                var page = new YearbookPage
                {
                    FileName = file.Name,
                    FilePath = file.FullName,
                    PageNumber = pageNumber++,
                    FileSize = file.Length
                };

                if (includeDimensions)
                {
                    try
                    {
                        var metadata = Image.Identify(file.FullName);
                        if (metadata != null)
                        {
                            page.Width = metadata.Width;
                            page.Height = metadata.Height;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not read image dimensions for {ImagePath}", file.FullName);
                    }
                }

                yearbook.Pages.Add(page);
            }

            return yearbook.Pages.Count > 0 ? yearbook : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating yearbook info for {directory.Name}");
            return null;
        }
    }

    public string GetImagePath(int year, string fileName)
    {
        var yearbook = GetYearbook(year, includeDimensions: false);
        if (yearbook == null) return string.Empty;

        var page = yearbook.Pages.FirstOrDefault(p => 
            string.Equals(p.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        
        return page?.FilePath ?? string.Empty;
    }

    public string GetThumbnailPath(int year, string fileName)
    {
        try
        {
            var dirPath = Path.Combine(_yearbookPath, $"Bonhomie-{year}");
            var thumbnailsPath = Path.Combine(dirPath, "thumbnails", fileName);
            
            if (File.Exists(thumbnailsPath))
            {
                return thumbnailsPath;
            }
            
            // Fall back to full image if thumbnail doesn't exist
            return GetImagePath(year, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting thumbnail path for {fileName} in year {year}");
            return GetImagePath(year, fileName);
        }
    }

    public List<int> GetAvailableYears()
    {
        return GetAllYearbooks().Yearbooks.Select(y => y.Year).OrderBy(y => y).ToList();
    }
}
