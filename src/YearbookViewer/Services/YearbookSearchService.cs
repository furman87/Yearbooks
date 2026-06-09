using System.Text.RegularExpressions;
using YearbookViewer.Models;

namespace YearbookViewer.Services;

public partial class YearbookSearchService
{
    private const int MaxResults = 200;
    private readonly YearbookService _yearbookService;
    private readonly ILogger<YearbookSearchService> _logger;

    public YearbookSearchService(YearbookService yearbookService, ILogger<YearbookSearchService> logger)
    {
        _yearbookService = yearbookService;
        _logger = logger;
    }

    public YearbookSearchResponse Search(string? query, int? year = null)
    {
        var response = new YearbookSearchResponse
        {
            Query = query?.Trim() ?? "",
            Year = year
        };

        if (response.Query.Length < 2)
        {
            return response;
        }

        var yearbooks = year.HasValue
            ? LoadSingleYearbook(year.Value)
            : _yearbookService.GetAllYearbooks().Yearbooks;

        response.SearchedYearbooks = yearbooks.Count;

        foreach (var yearbook in yearbooks.OrderBy(y => y.Year))
        {
            SearchYearbook(yearbook, response);

            if (response.Results.Count >= MaxResults)
            {
                break;
            }
        }

        return response;
    }

    private List<YearbookInfo> LoadSingleYearbook(int year)
    {
        var yearbook = _yearbookService.GetYearbook(year, includeDimensions: false);
        return yearbook == null ? [] : [yearbook];
    }

    private void SearchYearbook(YearbookInfo yearbook, YearbookSearchResponse response)
    {
        var textDirectory = Path.Combine(yearbook.DirectoryPath, "text");
        if (!Directory.Exists(textDirectory))
        {
            return;
        }

        var textFiles = Directory.GetFiles(textDirectory, "*.txt", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var textPath in textFiles)
        {
            try
            {
                var text = File.ReadAllText(textPath);
                var matchIndex = text.IndexOf(response.Query, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    continue;
                }

                var fileName = Path.GetFileNameWithoutExtension(textPath) + ".jpg";
                var page = yearbook.Pages.FirstOrDefault(p =>
                    string.Equals(p.FileName, fileName, StringComparison.OrdinalIgnoreCase));

                if (page == null)
                {
                    continue;
                }

                response.Results.Add(new YearbookSearchResult
                {
                    Year = yearbook.Year,
                    YearbookTitle = yearbook.Title,
                    PageNumber = page.PageNumber,
                    FileName = page.FileName,
                    Snippet = BuildSnippet(text, matchIndex, response.Query.Length)
                });

                if (response.Results.Count >= MaxResults)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not search OCR text file {TextPath}", textPath);
            }
        }
    }

    private static string BuildSnippet(string text, int matchIndex, int queryLength)
    {
        var normalized = WhitespaceRegex().Replace(text, " ").Trim();
        if (normalized.Length == 0)
        {
            return "";
        }

        var normalizedMatchIndex = Math.Max(0, normalized.IndexOf(
            text.Substring(matchIndex, Math.Min(queryLength, text.Length - matchIndex)),
            StringComparison.OrdinalIgnoreCase));

        if (normalizedMatchIndex < 0)
        {
            normalizedMatchIndex = 0;
        }

        const int context = 90;
        var start = Math.Max(0, normalizedMatchIndex - context);
        var length = Math.Min(normalized.Length - start, queryLength + context * 2);
        var snippet = normalized.Substring(start, length).Trim();

        if (start > 0)
        {
            snippet = "..." + snippet;
        }

        if (start + length < normalized.Length)
        {
            snippet += "...";
        }

        return snippet;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
