using Microsoft.Playwright;
using HtmlAgilityPack;
using YearbookDownloader.Models;
using YearbookDownloader.Utils;
using System.Text.RegularExpressions;

namespace YearbookDownloader.Services;

public class PlaywrightYearbookDownloader : IDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly string _baseUrl = "https://furman.tind.io";
    private readonly string _searchUrl;
    private bool _disposed = false;

    public PlaywrightYearbookDownloader(string searchUrl)
    {
        _searchUrl = searchUrl;
    }

    private async Task InitializeBrowserAsync()
    {
        if (_playwright == null)
        {
            Console.WriteLine("?? Initializing browser engine...");
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { 
                    "--no-sandbox", 
                    "--disable-blink-features=AutomationControlled",
                    "--disable-dev-shm-usage"
                }
            });
            Console.WriteLine("? Browser engine initialized");
        }
    }

    public async Task<List<YearbookRecord>> GetYearbookRecordsAsync()
    {
        await InitializeBrowserAsync();
        
        Console.WriteLine("?? Fetching yearbook search results with browser...");
        Console.WriteLine($"Request URL: {_searchUrl}");

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        });

        var page = await context.NewPageAsync();
        
        try
        {
            var response = await page.GotoAsync(_searchUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });

            Console.WriteLine($"Response Status: {response?.Status}");
            Console.WriteLine("? Waiting for page to fully load...");
            await page.WaitForTimeoutAsync(5000);

            var html = await page.ContentAsync();
            Console.WriteLine($"HTML Content Length: {html?.Length ?? 0} characters");

            if (!string.IsNullOrEmpty(html))
            {
                await SaveHtmlForDebugging(html, "playwright-search-results.html");
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var records = await ParseYearbookRecords(doc);
            return records;
        }
        finally
        {
            await page.CloseAsync();
            await context.CloseAsync();
        }
    }

    private async Task<List<YearbookRecord>> ParseYearbookRecords(HtmlDocument doc)
    {
        var records = new List<YearbookRecord>();

        // First try to find records in result-title divs (the actual structure we found)
        var resultTitleDivs = doc.DocumentNode.SelectNodes("//div[@class='result-title']");
        if (resultTitleDivs != null)
        {
            Console.WriteLine($"Found {resultTitleDivs.Count} result-title divs to process");

            foreach (var div in resultTitleDivs)
            {
                try
                {
                    var linkNode = div.SelectSingleNode(".//a[contains(@href, '/record/')]");
                    if (linkNode == null) continue;

                    var href = linkNode.GetAttributeValue("href", "");
                    if (string.IsNullOrEmpty(href)) continue;

                    var recordUrl = href.StartsWith("http") ? href : _baseUrl + href;
                    var title = linkNode.InnerText?.Trim() ?? "";

                    if (string.IsNullOrEmpty(title) || !title.ToLower().Contains("bonhomie"))
                        continue;

                    var yearString = FileHelper.ExtractYearFromTitle(title);
                    if (!int.TryParse(yearString, out int year))
                    {
                        Console.WriteLine($"Could not extract year from title: {title}");
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
                    Console.WriteLine($"? Found yearbook: {title} ({year})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing result-title div: {ex.Message}");
                }
            }

            if (records.Count > 0)
            {
                Console.WriteLine($"Found {records.Count} yearbook records.");
                return records;
            }
        }

        // Fallback to the original table-based parsing
        var recordRows = doc.DocumentNode.SelectNodes("//table[@id='main-content']//tr") ??
                        doc.DocumentNode.SelectNodes("//tbody//tr") ??
                        doc.DocumentNode.SelectNodes("//tr[.//a[contains(@href, '/record/')]]");
        
        if (recordRows == null)
        {
            var recordLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '/record/')]");
            if (recordLinks != null)
            {
                Console.WriteLine($"Found {recordLinks.Count} record links outside of tables");
                return await ParseRecordLinksDirectly(recordLinks);
            }
            return records;
        }

        Console.WriteLine($"Found {recordRows.Count} rows to process");

        foreach (var row in recordRows)
        {
            try
            {
                var linkNode = row.SelectSingleNode(".//a[contains(@href, '/record/')]");
                if (linkNode == null) continue;

                var href = linkNode.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href)) continue;

                var recordUrl = href.StartsWith("http") ? href : _baseUrl + href;
                var title = linkNode.InnerText?.Trim() ?? "";

                if (string.IsNullOrEmpty(title) || !title.ToLower().Contains("bonhomie"))
                    continue;

                var yearString = FileHelper.ExtractYearFromTitle(title);
                if (!int.TryParse(yearString, out int year))
                {
                    Console.WriteLine($"Could not extract year from title: {title}");
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
                Console.WriteLine($"? Found yearbook: {title} ({year})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing record row: {ex.Message}");
            }
        }

        Console.WriteLine($"Found {records.Count} yearbook records.");
        return records;
    }

    private async Task<List<YearbookRecord>> ParseRecordLinksDirectly(HtmlNodeCollection recordLinks)
    {
        var records = new List<YearbookRecord>();

        foreach (var linkNode in recordLinks)
        {
            try
            {
                var href = linkNode.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href)) continue;

                var recordUrl = href.StartsWith("http") ? href : _baseUrl + href;
                var title = linkNode.InnerText?.Trim() ?? "";

                if (string.IsNullOrEmpty(title) || !title.ToLower().Contains("bonhomie"))
                    continue;

                var yearString = FileHelper.ExtractYearFromTitle(title);
                if (!int.TryParse(yearString, out int year))
                    continue;

                var record = new YearbookRecord
                {
                    Title = title,
                    RecordUrl = recordUrl,
                    Year = year,
                    DirectoryName = $"Bonhomie-{year}"
                };

                records.Add(record);
                Console.WriteLine($"? Found yearbook: {title} ({year})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing record link: {ex.Message}");
            }
        }

        return records;
    }

    public async Task<List<YearbookFile>> GetYearbookFilesAsync(YearbookRecord record)
    {
        await InitializeBrowserAsync();
        
        Console.WriteLine($"Getting files for {record.Title}...");

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8",
                ["Accept-Language"] = "en-US,en;q=0.9",
                ["Accept-Encoding"] = "gzip, deflate, br",
                ["DNT"] = "1",
                ["Connection"] = "keep-alive",
                ["Upgrade-Insecure-Requests"] = "1"
            }
        });

        var page = await context.NewPageAsync();
        
        try
        {
            // Add a random delay to seem more human-like
            await Task.Delay(Random.Shared.Next(2000, 5000));

            Console.WriteLine($"?? Navigating to record page: {record.RecordUrl}");
            
            var response = await page.GotoAsync(record.RecordUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 45000
            });

            Console.WriteLine($"Response Status: {response?.Status}");

            if (response?.Status >= 400)
            {
                Console.WriteLine($"?? HTTP Error {response.Status} when accessing record page");
                await SaveHtmlForDebugging(await page.ContentAsync(), $"record-{record.Year}-error.html");
                
                // Try to wait and retry once
                Console.WriteLine("?? Waiting before retry...");
                await Task.Delay(5000);
                
                response = await page.GotoAsync(record.RecordUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 45000
                });
                
                Console.WriteLine($"Retry Response Status: {response?.Status}");
            }

            // Wait for the page to fully load and any JavaScript to execute
            await page.WaitForTimeoutAsync(5000);
            
            // Try to wait for file download components to load
            try
            {
                await page.WaitForSelectorAsync("tindui-app-file-download-link, .file-download, .download-link", new PageWaitForSelectorOptions { Timeout = 10000 });
            }
            catch (TimeoutException)
            {
                Console.WriteLine("? No download components found within timeout");
            }

            var html = await page.ContentAsync();
            Console.WriteLine($"Record page HTML length: {html.Length} characters");
            await SaveHtmlForDebugging(html, $"record-{record.Year}.html");

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            return await ParseYearbookFiles(doc, record);
        }
        finally
        {
            await page.CloseAsync();
            await context.CloseAsync();
        }
    }

    private async Task<List<YearbookFile>> ParseYearbookFiles(HtmlDocument doc, YearbookRecord record)
    {
        var files = new List<YearbookFile>();

        // Method 1: Try to find tindui-app-file-download-link components
        var downloadComponents = doc.DocumentNode.SelectNodes("//tindui-app-file-download-link[@url]");
        if (downloadComponents != null)
        {
            Console.WriteLine($"Found {downloadComponents.Count} tindui download components");
            foreach (var component in downloadComponents)
            {
                var downloadUrl = component.GetAttributeValue("url", "");
                if (!string.IsNullOrEmpty(downloadUrl) && 
                    (downloadUrl.ToLower().EndsWith(".jpg") || downloadUrl.ToLower().EndsWith(".jpeg")))
                {
                    if (!downloadUrl.StartsWith("http"))
                    {
                        downloadUrl = _baseUrl + downloadUrl;
                    }

                    var fileName = ExtractFileNameFromUrl(downloadUrl);
                    if (!string.IsNullOrEmpty(fileName))
                    {
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
                        Console.WriteLine($"?? Found file via tindui component: {sanitizedFileName}");
                    }
                }
            }
        }

        // Method 2: Look for traditional download links
        if (files.Count == 0)
        {
            var downloadLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '.jpg') or contains(@href, '.jpeg')]") ??
                               doc.DocumentNode.SelectNodes("//a[contains(@class, 'download') or contains(@class, 'file')]");
            
            if (downloadLinks != null)
            {
                Console.WriteLine($"Found {downloadLinks.Count} potential download links");
                foreach (var link in downloadLinks)
                {
                    var downloadUrl = link.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        if (!downloadUrl.StartsWith("http"))
                        {
                            downloadUrl = _baseUrl + downloadUrl;
                        }

                        var fileName = ExtractFileNameFromUrl(downloadUrl);
                        if (!string.IsNullOrEmpty(fileName) && 
                            (fileName.ToLower().EndsWith(".jpg") || fileName.ToLower().EndsWith(".jpeg")))
                        {
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
                            Console.WriteLine($"?? Found file via download link: {sanitizedFileName}");
                        }
                    }
                }
            }
        }

        // Method 3: Look for any img tags with JPG sources
        if (files.Count == 0)
        {
            var jpgImgs = doc.DocumentNode.SelectNodes("//img[contains(@src, '.jpg')]");
            var jpegImgs = doc.DocumentNode.SelectNodes("//img[contains(@src, '.jpeg')]");
            
            var imgTags = new List<HtmlNode>();
            if (jpgImgs != null) imgTags.AddRange(jpgImgs);
            if (jpegImgs != null) imgTags.AddRange(jpegImgs);
            
            if (imgTags.Count > 0)
            {
                Console.WriteLine($"Found {imgTags.Count} img tags with JPG sources");
                foreach (var img in imgTags)
                {
                    var imageUrl = img.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        if (!imageUrl.StartsWith("http"))
                        {
                            imageUrl = _baseUrl + imageUrl;
                        }

                        var fileName = ExtractFileNameFromUrl(imageUrl);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            var sanitizedFileName = FileHelper.SanitizeFileName(fileName);
                            var localPath = Path.Combine(record.DirectoryName, sanitizedFileName);

                            var file = new YearbookFile
                            {
                                FileName = sanitizedFileName,
                                DownloadUrl = imageUrl,
                                LocalPath = localPath,
                                IsDownloaded = FileHelper.IsFileDownloaded(localPath)
                            };

                            files.Add(file);
                            Console.WriteLine($"?? Found file via img tag: {sanitizedFileName}");
                        }
                    }
                }
            }
        }

        Console.WriteLine($"Found {files.Count} JPG files for {record.Title}");
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
            Console.WriteLine($"? Error during download process: {ex.Message}");
            throw;
        }
    }

    private async Task DownloadYearbookFilesAsync(YearbookRecord record)
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
        Console.WriteLine($"Files to download: {filesToDownload.Count}");

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        int downloadedCount = 0;
        foreach (var file in filesToDownload)
        {
            try
            {
                Console.Write($"Downloading {file.FileName}... ");
                
                var response = await httpClient.GetAsync(file.DownloadUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(file.LocalPath, content);

                    file.IsDownloaded = true;
                    downloadedCount++;

                    Console.WriteLine($"? ({FileHelper.GetFileSizeString(content.Length)})");
                }
                else
                {
                    Console.WriteLine($"? HTTP {response.StatusCode}");
                }

                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error: {ex.Message}");
            }
        }

        Console.WriteLine($"Downloaded {downloadedCount} files for {record.Title}");
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

    private async Task SaveHtmlForDebugging(string html, string filename)
    {
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _browser?.DisposeAsync().AsTask().Wait();
            _playwright?.Dispose();
            _disposed = true;
        }
    }
}