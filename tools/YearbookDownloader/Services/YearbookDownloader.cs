using HtmlAgilityPack;
using YearbookDownloader.Models;
using YearbookDownloader.Utils;
using System.Text.RegularExpressions;

namespace YearbookDownloader.Services;

public class YearbookDownloader : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "https://furman.tind.io";
    private readonly string _searchUrl;

    public YearbookDownloader(string searchUrl)
    {
        _httpClient = new HttpClient(new HttpClientHandler()
        {
            UseCookies = true
        });
        
        // Configure timeout
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        // Add comprehensive headers to appear more like a real browser
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", 
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q0.9");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        _httpClient.DefaultRequestHeaders.Add("DNT", "1");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        
        _searchUrl = searchUrl;
    }

    public async Task<List<YearbookRecord>> GetYearbookRecordsAsync()
    {
        Console.WriteLine("Fetching yearbook search results...");
        Console.WriteLine($"Request URL: {_searchUrl}");
        
        try
        {
            var response = await _httpClient.GetAsync(_searchUrl);
            Console.WriteLine($"Response Status: {response.StatusCode}");
            Console.WriteLine($"Response Content Type: {response.Content.Headers.ContentType}");
            Console.WriteLine($"Response Content Length: {response.Content.Headers.ContentLength}");
            
            var html = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"HTML Content Length: {html?.Length ?? 0} characters");
            
            // Check for JavaScript challenges
            if (IsJavaScriptChallenge(html))
            {
                Console.WriteLine("?? JavaScript challenge detected! Attempting to handle...");
                await SaveHtmlForDebugging(html, "challenge-response.html");
                
                var challengeHtml = await HandleJavaScriptChallenge(_searchUrl);
                if (challengeHtml != null)
                {
                    html = challengeHtml;
                    await SaveHtmlForDebugging(html, "challenge-resolved.html");
                }
                else
                {
                    Console.WriteLine("? Could not resolve JavaScript challenge with basic methods.");
                    Console.WriteLine("?? Consider using the Playwright version: PlaywrightYearbookDownloader");
                    return new List<YearbookRecord>();
                }
            }
            
            // Save HTML for debugging
            if (!string.IsNullOrEmpty(html))
            {
                await SaveHtmlForDebugging(html, "search-results.html");
            }
            
            if (string.IsNullOrEmpty(html))
            {
                Console.WriteLine("? No HTML content received from the server");
                return new List<YearbookRecord>();
            }
            
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var records = new List<YearbookRecord>();

            // Find all table rows that contain yearbook records in the main-content table
            var recordRows = doc.DocumentNode.SelectNodes("//table[@id='main-content']//tr");
            
            if (recordRows == null)
            {
                Console.WriteLine("No records found in search results with main-content table.");
                return records;
            }

            Console.WriteLine($"Found {recordRows.Count} total rows in main-content table");

            foreach (var row in recordRows)
            {
                try
                {
                    // Look for the link in the result-title div
                    var linkNode = row.SelectSingleNode(".//div[@class='result-title']//a[contains(@href, '/record/')]");
                    if (linkNode == null) continue;

                    var href = linkNode.GetAttributeValue("href", "");
                    if (string.IsNullOrEmpty(href)) continue;

                    var recordUrl = href.StartsWith("http") ? href : _baseUrl + href;
                    var title = linkNode.InnerText?.Trim() ?? "";

                    // Extract year from the brief-options div - look for text after the calendar icon
                    var yearString = "";
                    var calendarElement = row.SelectSingleNode(".//div[@class='brief-options bv-config']//i[@class='fa fa-calendar']");
                    if (calendarElement?.NextSibling != null)
                    {
                        yearString = calendarElement.NextSibling.InnerText?.Trim();
                    }
                    
                    // If not found in the expected location, try extracting from title
                    if (string.IsNullOrEmpty(yearString))
                    {
                        yearString = FileHelper.ExtractYearFromTitle(title);
                    }

                    if (!int.TryParse(yearString, out int year))
                    {
                        Console.WriteLine($"Could not extract year from title: {title}, yearString: '{yearString}'");
                        continue;
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
                    Console.WriteLine($"Error parsing record row: {ex.Message}");
                }
            }

            Console.WriteLine($"Found {records.Count} yearbook records.");
            return records;
        }
        catch (HttpRequestException httpEx)
        {
            Console.WriteLine($"? HTTP Request failed: {httpEx.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Unexpected error: {ex.Message}");
            throw;
        }
    }

    public async Task<List<YearbookFile>> GetYearbookFilesAsync(YearbookRecord record)
    {
        Console.WriteLine($"Getting files for {record.Title}...");
        
        var html = await _httpClient.GetStringAsync(record.RecordUrl);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var files = new List<YearbookFile>();

        // Find the record-files-list table
        var fileTable = doc.DocumentNode.SelectSingleNode("//table[@id='record-files-list']");
        if (fileTable == null)
        {
            Console.WriteLine($"No files table found for {record.Title}");
            return files;
        }

        // Find all download links in the table
        var downloadLinks = fileTable.SelectNodes(".//a[contains(@class, 'tindui-app-file-download-link')]");
        if (downloadLinks == null)
        {
            Console.WriteLine($"No download links found for {record.Title}");
            return files;
        }

        foreach (var link in downloadLinks)
        {
            try
            {
                var downloadUrl = link.GetAttributeValue("url", "");
                if (string.IsNullOrEmpty(downloadUrl)) continue;

                // Extract filename from the link or URL
                var fileName = ExtractFileNameFromUrl(downloadUrl) ?? ExtractFileNameFromLinkText(link.InnerText);
                if (string.IsNullOrEmpty(fileName)) continue;

                // Only process JPG files
                if (!fileName.ToLower().EndsWith(".jpg") && !fileName.ToLower().EndsWith(".jpeg"))
                    continue;

                var sanitizedFileName = FileHelper.SanitizeFileName(fileName);
                var localPath = Path.Combine(record.DirectoryName, sanitizedFileName);

                var file = new YearbookFile
                {
                    FileName = sanitizedFileName,
                    DownloadUrl = downloadUrl,
                    LocalPath = localPath,
                    IsDownloaded = FileHelper.IsFileDownloaded(localPath)
                };

                files.Add(file);
                Console.WriteLine($"Found file: {sanitizedFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing file link: {ex.Message}");
            }
        }

        Console.WriteLine($"Found {files.Count} JPG files for {record.Title}");
        return files;
    }

    public async Task DownloadYearbookFilesAsync(YearbookRecord record)
    {
        Console.WriteLine($"\n--- Downloading files for {record.Title} ---");
        
        // Ensure directory exists
        FileHelper.EnsureDirectoryExists(record.DirectoryName);

        // Get files for this yearbook
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

        int downloadedCount = 0;
        foreach (var file in filesToDownload)
        {
            try
            {
                Console.Write($"Downloading {file.FileName}... ");
                
                var response = await _httpClient.GetAsync(file.DownloadUrl);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(file.LocalPath, content);

                file.IsDownloaded = true;
                downloadedCount++;

                Console.WriteLine($"? ({FileHelper.GetFileSizeString(content.Length)}) [{downloadedCount}/{filesToDownload.Count}]");

                // Small delay to be respectful to the server
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error: {ex.Message}");
            }
        }

        Console.WriteLine($"Downloaded {downloadedCount} files for {record.Title}");
    }

    public async Task DownloadAllYearbooksAsync()
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

    private string? ExtractFileNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileName(uri.LocalPath);
            return string.IsNullOrEmpty(fileName) ? null : fileName;
        }
        catch
        {
            return null;
        }
    }

    private string ExtractFileNameFromLinkText(string linkText)
    {
        var cleaned = linkText?.Trim() ?? "unknown";
        if (!cleaned.ToLower().EndsWith(".jpg") && !cleaned.ToLower().EndsWith(".jpeg"))
        {
            cleaned += ".jpg";
        }
        return cleaned;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private async Task SaveHtmlForDebugging(string? html, string filename)
    {
        if (string.IsNullOrEmpty(html))
        {
            return;
        }

        try
        {
            var debugPath = Path.Combine("debug", filename);
            Directory.CreateDirectory("debug");
            await File.WriteAllTextAsync(debugPath, html);
            Console.WriteLine($"?? HTML saved to {debugPath} for debugging");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? Could not save debug HTML: {ex.Message}");
        }
    }

    private bool IsJavaScriptChallenge(string? html)
    {
        if (string.IsNullOrEmpty(html)) return false;
        
        var challengeIndicators = new[]
        {
            "challenge.js",
            "cf-challenge",
            "cloudflare",
            "please wait",
            "checking your browser",
            "ray id",
            "ddos protection",
            "security check"
        };
        
        var lowerHtml = html.ToLower();
        return challengeIndicators.Any(indicator => lowerHtml.Contains(indicator));
    }

    private async Task<string?> HandleJavaScriptChallenge(string url)
    {
        Console.WriteLine("?? JavaScript challenge detected. Trying alternative approach...");
        
        await Task.Delay(5000);
        
        using var tempClient = new HttpClient();
        tempClient.DefaultRequestHeaders.Clear();
        tempClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)");
        
        try
        {
            var response = await tempClient.GetAsync(url);
            var html = await response.Content.ReadAsStringAsync();
            
            if (!IsJavaScriptChallenge(html))
            {
                Console.WriteLine("? Alternative approach succeeded!");
                return html;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Alternative approach failed: {ex.Message}");
        }
        
        return null;
    }
}
