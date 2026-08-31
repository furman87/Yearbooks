using Microsoft.Playwright;
using HtmlAgilityPack;
using YearbookDownloader.Models;
using YearbookDownloader.Utils;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YearbookDownloader.Services;

public class PlaywrightYearbookDownloader : IDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private readonly string _baseUrl = "https://furman.tind.io";
    private readonly string _searchUrl;
    private readonly bool _headless;
    private bool _disposed = false;

    public PlaywrightYearbookDownloader(string searchUrl, bool headless = true)
    {
        _searchUrl = searchUrl;
        _headless = headless;
    }

    private async Task InitializeBrowserAsync()
    {
        if (_playwright == null)
        {
            Console.WriteLine("Initializing browser engine...");
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = _headless,
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-blink-features=AutomationControlled",
                    "--disable-dev-shm-usage"
                }
            });
            _context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = GetUserAgent(),
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8",
                    ["Accept-Language"] = "en-US,en;q=0.9",
                    ["DNT"] = "1",
                    ["Upgrade-Insecure-Requests"] = "1"
                }
            });
            Console.WriteLine($"Browser engine initialized ({(_headless ? "headless" : "headed")})");
        }
    }

    public async Task<List<YearbookRecord>> GetYearbookRecordsAsync()
    {
        await InitializeBrowserAsync();
        
        Console.WriteLine("Fetching yearbook search results with browser...");
        Console.WriteLine($"Request URL: {_searchUrl}");

        var page = await _context!.NewPageAsync();
        
        try
        {
            var response = await page.GotoAsync(_searchUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });

            Console.WriteLine($"Response Status: {response?.Status}");
            Console.WriteLine("Waiting for page to fully load...");
            await WaitForRecordPageAsync(page);

            var html = await page.ContentAsync();
            Console.WriteLine($"HTML Content Length: {html?.Length ?? 0} characters");

            if (!string.IsNullOrEmpty(html))
            {
                await SaveHtmlForDebugging(html, "playwright-search-results.html");
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var records = ParseYearbookRecords(doc);
            return records;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private List<YearbookRecord> ParseYearbookRecords(HtmlDocument doc)
    {
        var records = new List<YearbookRecord>();

        var resultTitleDivs = doc.DocumentNode.SelectNodes("//div[@class='result-title']");
        if (resultTitleDivs != null)
        {
            Console.WriteLine($"Found {resultTitleDivs.Count} result-title divs to process");

            foreach (var div in resultTitleDivs)
            {
                TryAddRecordFromLink(records, div.SelectSingleNode(".//a[contains(@href, '/record/')]"));
            }

            if (records.Count > 0)
            {
                Console.WriteLine($"Found {records.Count} yearbook records.");
                return records;
            }
        }

        var recordRows = doc.DocumentNode.SelectNodes("//table[@id='main-content']//tr") ??
                        doc.DocumentNode.SelectNodes("//tbody//tr") ??
                        doc.DocumentNode.SelectNodes("//tr[.//a[contains(@href, '/record/')]]");
        
        if (recordRows != null)
        {
            Console.WriteLine($"Found {recordRows.Count} rows to process");
            foreach (var row in recordRows)
            {
                TryAddRecordFromLink(records, row.SelectSingleNode(".//a[contains(@href, '/record/')]"));
            }
        }
        else
        {
            var recordLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '/record/')]");
            if (recordLinks != null)
            {
                Console.WriteLine($"Found {recordLinks.Count} record links outside of tables");
                foreach (var linkNode in recordLinks)
                {
                    TryAddRecordFromLink(records, linkNode);
                }
            }
        }

        Console.WriteLine($"Found {records.Count} yearbook records.");
        return records;
    }

    private void TryAddRecordFromLink(List<YearbookRecord> records, HtmlNode? linkNode)
    {
        try
        {
            if (linkNode == null) return;

            var href = linkNode.GetAttributeValue("href", "");
            if (string.IsNullOrEmpty(href)) return;

            var recordUrl = href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : _baseUrl + href;
            var title = WebUtility.HtmlDecode(linkNode.InnerText)?.Trim() ?? "";

            if (string.IsNullOrEmpty(title) || !title.Contains("bonhomie", StringComparison.OrdinalIgnoreCase))
                return;

            var yearString = FileHelper.ExtractYearFromTitle(title);
            if (!int.TryParse(yearString, out int year))
            {
                Console.WriteLine($"Could not extract year from title: {title}");
                return;
            }

            if (records.Any(r => r.Year == year))
            {
                return;
            }

            var record = new YearbookRecord
            {
                Title = title,
                RecordUrl = recordUrl,
                Year = year,
                DirectoryName = $"Bonhomie-{year}"
            };

            records.Add(record);
            Console.WriteLine($"Found yearbook: {title} ({year})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing record link: {ex.Message}");
        }
    }

    public async Task<List<YearbookFile>> GetYearbookFilesAsync(YearbookRecord record)
    {
        Console.WriteLine($"Getting files for {record.Title}...");

        var manifestFiles = await FetchIiifManifestFilesAsync(record);
        if (manifestFiles.Count > 0)
        {
            Console.WriteLine($"Found {manifestFiles.Count} JPG files for {record.Title}");
            return manifestFiles;
        }

        await InitializeBrowserAsync();

        var imageUrlsSeenByBrowser = new List<string>();
        var page = await _context!.NewPageAsync();
        page.Response += (_, response) =>
        {
            if (IsJpegUrl(response.Url))
            {
                imageUrlsSeenByBrowser.Add(response.Url);
            }
        };
        
        try
        {
            await Task.Delay(Random.Shared.Next(1000, 2500));

            var recordUrl = GetUniversalViewerRecordUrl(record.RecordUrl);
            Console.WriteLine($"Navigating to record page: {recordUrl}");
            
            var response = await page.GotoAsync(recordUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 45000
            });

            Console.WriteLine($"Response Status: {response?.Status}");
            await WaitForRecordPageAsync(page);

            var html = await page.ContentAsync();
            Console.WriteLine($"Record page HTML length: {html.Length} characters");
            await SaveHtmlForDebugging(html, $"record-{record.Year}.html");

            var files = ParseYearbookFiles(html, record);

            if (files.Count == 0)
            {
                Console.WriteLine("Browser page did not expose files. Trying direct record HTML fetch...");
                var directHtml = await FetchRecordHtmlWithHttpClientAsync(recordUrl, record.RecordUrl);
                if (!string.IsNullOrWhiteSpace(directHtml))
                {
                    await SaveHtmlForDebugging(directHtml, $"record-{record.Year}-direct.html");
                    files = ParseYearbookFiles(directHtml, record);
                }
            }

            if (files.Count == 0 && imageUrlsSeenByBrowser.Count > 0)
            {
                Console.WriteLine($"Using {imageUrlsSeenByBrowser.Distinct().Count()} image URLs observed by the browser.");
                files = CreateYearbookFiles(imageUrlsSeenByBrowser, record);
            }

            Console.WriteLine($"Found {files.Count} JPG files for {record.Title}");
            return files;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private async Task WaitForRecordPageAsync(IPage page)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var html = await page.ContentAsync();
            if (!IsJavaScriptChallenge(html))
            {
                return;
            }

            Console.WriteLine("JavaScript/WAF challenge page detected; waiting for browser token and reload...");
            await page.WaitForTimeoutAsync(8000);
            html = await page.ContentAsync();
            if (IsJavaScriptChallenge(html))
            {
                try
                {
                    await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Challenge reload failed: {ex.Message}");
                }
            }
        }
    }

    private List<YearbookFile> ParseYearbookFiles(string html, YearbookRecord record)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var candidateUrls = new List<string>();

        AddAttributeUrls(candidateUrls, doc, "//meta[contains(translate(@property, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'og:image') or contains(translate(@name, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'twitter:image')]", "content");
        AddAttributeUrls(candidateUrls, doc, "//a[contains(translate(@href, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '.jpg') or contains(translate(@href, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '.jpeg')]", "href");
        AddAttributeUrls(candidateUrls, doc, "//img[contains(translate(@src, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '.jpg') or contains(translate(@src, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '.jpeg')]", "src");
        AddAttributeUrls(candidateUrls, doc, "//*[@data-src or @data-full or @data-url]", "data-src");
        AddAttributeUrls(candidateUrls, doc, "//*[@data-src or @data-full or @data-url]", "data-full");
        AddAttributeUrls(candidateUrls, doc, "//*[@data-src or @data-full or @data-url]", "data-url");

        return CreateYearbookFiles(candidateUrls, record);
    }

    private void AddAttributeUrls(List<string> urls, HtmlDocument doc, string xpath, string attributeName)
    {
        var nodes = doc.DocumentNode.SelectNodes(xpath);
        if (nodes == null)
        {
            return;
        }

        foreach (var node in nodes)
        {
            var value = WebUtility.HtmlDecode(node.GetAttributeValue(attributeName, "")).Trim();
            if (IsJpegUrl(value))
            {
                urls.Add(MakeAbsoluteUrl(value));
            }
        }
    }

    private List<YearbookFile> CreateYearbookFiles(IEnumerable<string> urls, YearbookRecord record)
    {
        var files = new List<YearbookFile>();
        var seenFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var url in urls.Select(MakeAbsoluteUrl).Where(IsJpegUrl).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(u => u, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = ExtractFileNameFromUrl(url);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var sanitizedFileName = FileHelper.SanitizeFileName(fileName);
            if (!seenFileNames.Add(sanitizedFileName))
            {
                continue;
            }

            var localPath = Path.Combine(record.DirectoryName, sanitizedFileName);
            files.Add(new YearbookFile
            {
                FileName = sanitizedFileName,
                DownloadUrl = url,
                LocalPath = localPath,
                IsDownloaded = FileHelper.IsFileDownloaded(localPath)
            });
        }

        return files;
    }

    public async Task DownloadAllYearbooksAsync()
    {
        try
        {
            var records = await GetYearbookRecordsAsync();
            
            if (records.Count == 0)
            {
                Console.WriteLine("No yearbooks found to download.");
                return;
            }

            Console.WriteLine($"\nStarting download of {records.Count} yearbooks...\n");

            foreach (var record in records.OrderBy(r => r.Year))
            {
                await DownloadYearbookFilesAsync(record);
            }

            Console.WriteLine("\n=== Download process completed ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during download process: {ex.Message}");
            throw;
        }
    }

    public async Task DownloadYearbookFilesAsync(YearbookRecord record)
    {
        Console.WriteLine($"\n--- Downloading files for {record.Title} ---");
        
        FileHelper.EnsureDirectoryExists(record.DirectoryName);
        record.Files = await GetYearbookFilesAsync(record);

        if (record.Files.Count == 0)
        {
            Console.WriteLine($"No files to download for {record.Title}");
            return;
        }

        var filesToDownload = record.Files.Where(f => !f.IsDownloaded).ToList();
        Console.WriteLine($"Files already downloaded: {record.Files.Count - filesToDownload.Count}");
        Console.WriteLine($"Files to download: {filesToDownload.Count}");

        if (filesToDownload.Count == 0)
        {
            Console.WriteLine($"All files already downloaded for {record.Title}");
            return;
        }

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", GetUserAgent());
        httpClient.DefaultRequestHeaders.Referrer = new Uri(GetUniversalViewerRecordUrl(record.RecordUrl));

        int downloadedCount = 0;
        foreach (var file in filesToDownload)
        {
            try
            {
                if (FileHelper.IsFileDownloaded(file.LocalPath))
                {
                    Console.WriteLine($"Skipping existing {file.FileName}");
                    continue;
                }

                Console.Write($"Downloading {file.FileName}... ");
                
                var response = await httpClient.GetAsync(file.DownloadUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(file.LocalPath, content);

                    file.IsDownloaded = true;
                    downloadedCount++;

                    Console.WriteLine($"({FileHelper.GetFileSizeString(content.Length)}) [{downloadedCount}/{filesToDownload.Count}]");
                }
                else
                {
                    Console.WriteLine($"HTTP {response.StatusCode}");
                }

                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        Console.WriteLine($"Downloaded {downloadedCount} files for {record.Title}");
    }

    private async Task<List<YearbookFile>> FetchIiifManifestFilesAsync(YearbookRecord record)
    {
        var files = new List<YearbookFile>();
        var recordIdMatch = Regex.Match(record.RecordUrl, @"/record/(\d+)");
        if (!recordIdMatch.Success)
        {
            return files;
        }

        var manifestUrl = $"{_baseUrl}/record/{recordIdMatch.Groups[1].Value}/export/iiif_manifest";
        Console.WriteLine($"Trying IIIF manifest: {manifestUrl}");

        try
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = true
            };
            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            httpClient.DefaultRequestHeaders.Add("User-Agent", GetUserAgent());
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json,text/plain,*/*");
            httpClient.DefaultRequestHeaders.Referrer = new Uri(GetUniversalViewerRecordUrl(record.RecordUrl));

            var response = await httpClient.GetAsync(manifestUrl);
            Console.WriteLine($"IIIF manifest status: {(int)response.StatusCode} {response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                return files;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("items", out var canvases) || canvases.ValueKind != JsonValueKind.Array)
            {
                return files;
            }

            foreach (var canvas in canvases.EnumerateArray())
            {
                var label = GetJsonLabel(canvas);
                var serviceId = GetCanvasImageServiceId(canvas);
                if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(serviceId))
                {
                    continue;
                }

                var fileName = FileHelper.SanitizeFileName(label.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || label.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                    ? label
                    : label + ".jpg");
                var imageUrl = serviceId.TrimEnd('/') + "/full/max/0/default.jpg";
                var localPath = Path.Combine(record.DirectoryName, fileName);

                files.Add(new YearbookFile
                {
                    FileName = fileName,
                    DownloadUrl = imageUrl,
                    LocalPath = localPath,
                    IsDownloaded = FileHelper.IsFileDownloaded(localPath)
                });
            }

            Console.WriteLine($"IIIF manifest exposed {files.Count} page image services.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IIIF manifest fetch failed: {ex.Message}");
        }

        return files;
    }

    private string? GetJsonLabel(JsonElement canvas)
    {
        if (!canvas.TryGetProperty("label", out var label))
        {
            return null;
        }

        if (label.ValueKind == JsonValueKind.Object &&
            label.TryGetProperty("none", out var values) &&
            values.ValueKind == JsonValueKind.Array)
        {
            return values.EnumerateArray().FirstOrDefault().GetString();
        }

        return label.ValueKind == JsonValueKind.String ? label.GetString() : null;
    }

    private string? GetCanvasImageServiceId(JsonElement canvas)
    {
        if (!canvas.TryGetProperty("items", out var annotationPages) || annotationPages.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var annotationPage in annotationPages.EnumerateArray())
        {
            if (!annotationPage.TryGetProperty("items", out var annotations) || annotations.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var annotation in annotations.EnumerateArray())
            {
                if (!annotation.TryGetProperty("body", out var body))
                {
                    continue;
                }

                if (!body.TryGetProperty("service", out var services) || services.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var service in services.EnumerateArray())
                {
                    if (service.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    {
                        return id.GetString();
                    }
                }
            }
        }

        return null;
    }
    private async Task<string?> FetchRecordHtmlWithHttpClientAsync(string recordUrl, string referrer)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = true
            };
            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            httpClient.DefaultRequestHeaders.Add("User-Agent", GetUserAgent());
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            httpClient.DefaultRequestHeaders.Referrer = new Uri(referrer);

            var response = await httpClient.GetAsync(recordUrl);
            Console.WriteLine($"Direct record fetch status: {(int)response.StatusCode} {response.StatusCode}");
            var html = await response.Content.ReadAsStringAsync();

            if (IsJavaScriptChallenge(html))
            {
                Console.WriteLine("Direct record fetch returned a JavaScript/WAF challenge.");
                return null;
            }

            return html;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Direct record fetch failed: {ex.Message}");
            return null;
        }
    }

    private string? ExtractFileNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(MakeAbsoluteUrl(url));
            var fileName = Path.GetFileName(uri.LocalPath);
            return string.IsNullOrEmpty(fileName) ? null : fileName;
        }
        catch
        {
            return null;
        }
    }

    private string GetUniversalViewerRecordUrl(string recordUrl)
    {
        var absoluteUrl = MakeAbsoluteUrl(recordUrl);
        var builder = new UriBuilder(absoluteUrl);
        var queryParts = builder.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.StartsWith("v=", StringComparison.OrdinalIgnoreCase))
            .ToList();

        queryParts.Add("v=uv");
        builder.Query = string.Join('&', queryParts);
        builder.Fragment = string.Empty;
        return builder.Uri.ToString();
    }

    private string MakeAbsoluteUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        if (url.StartsWith("//"))
        {
            return "https:" + url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return url;
        }

        return new Uri(new Uri(_baseUrl), url).ToString();
    }

    private bool IsJpegUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return url.Contains(".jpg", StringComparison.OrdinalIgnoreCase) ||
               url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private string GetUserAgent() =>
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private async Task SaveHtmlForDebugging(string html, string filename)
    {
        try
        {
            var debugPath = Path.Combine("debug", filename);
            Directory.CreateDirectory("debug");
            await File.WriteAllTextAsync(debugPath, html);
            Console.WriteLine($"HTML saved to {debugPath} for debugging");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not save debug HTML: {ex.Message}");
        }
    }

    private bool IsJavaScriptChallenge(string? html)
    {
        if (string.IsNullOrEmpty(html)) return false;

        var challengeIndicators = new[]
        {
            "challenge.js",
            "awswaf",
            "gokuprops",
            "cf-challenge",
            "cloudflare",
            "please wait",
            "checking your browser",
            "ray id",
            "ddos protection",
            "security check"
        };

        return challengeIndicators.Any(indicator => html.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _context?.DisposeAsync().AsTask().Wait();
            _browser?.DisposeAsync().AsTask().Wait();
            _playwright?.Dispose();
            _disposed = true;
        }
    }
}
