using YearbookDownloader.Models;
using YearbookDownloader.Services;

// Furman University Yearbook Downloader
// Downloads JPEG images from Furman yearbooks from tind.io

Console.WriteLine("===== Furman Yearbook Downloader =====");

// Check command line arguments for method preference
var cmdArgs = Environment.GetCommandLineArgs();
bool usePlaywright = cmdArgs.Contains("--playwright") || cmdArgs.Contains("-p");
bool useBasic = cmdArgs.Contains("--basic") || cmdArgs.Contains("-b");
bool useHeadlessBrowser = !cmdArgs.Contains("--headed");
var outputRoot = GetOptionValue(cmdArgs, "--output", "-o") ?? FindDefaultYearbookRoot();
var directRecordUrl = GetOptionValue(cmdArgs, "--record-url", string.Empty);
var directRecordYear = GetYearOptionValue(cmdArgs, "--year", null);
var fromYear = GetYearOptionValue(cmdArgs, "--from", null) ?? directRecordYear ?? 1995;
var toYear = GetYearOptionValue(cmdArgs, "--to", null) ?? fromYear;

if (fromYear > toYear)
{
    Console.WriteLine("--from cannot be greater than --to.");
    Environment.Exit(1);
}

string searchUrl = $"https://furman.tind.io/search?cc=Furman%20Yearbooks&ln=en&p=&f=&rm=&sf=year&so=a&rg=100&c=Furman%20Yearbooks&c=&of=hb&fti=1&fct__4-range={fromYear}%2C{toYear}&fti=1";

outputRoot = Path.GetFullPath(outputRoot);
Directory.CreateDirectory(outputRoot);
Directory.SetCurrentDirectory(outputRoot);

Console.WriteLine(string.IsNullOrWhiteSpace(directRecordUrl)
    ? $"Downloading yearbooks from {fromYear}-{toYear}"
    : $"Downloading yearbook record {directRecordUrl}");
Console.WriteLine($"Output folder: {outputRoot}");
Console.WriteLine();

if (!useBasic && !usePlaywright)
{
    Console.WriteLine("Select download method:");
    Console.WriteLine("1. Basic HTTP client (faster, may not work with JavaScript challenges)");
    Console.WriteLine("2. Browser automation (slower, handles JavaScript challenges)");
    Console.Write("Enter your choice (1 or 2): ");
    
    var choice = Console.ReadLine();
    usePlaywright = choice == "2";
}

try
{
    if (usePlaywright)
    {
        Console.WriteLine("Browser automation method selected...");
        Console.WriteLine("Note: First run will install browser dependencies automatically.\n");

        using var downloader = new PlaywrightYearbookDownloader(searchUrl, useHeadlessBrowser);
        if (!string.IsNullOrWhiteSpace(directRecordUrl))
        {
            await downloader.DownloadYearbookFilesAsync(CreateDirectRecord(directRecordUrl, directRecordYear ?? fromYear));
        }
        else if (TryGetKnownRecords(fromYear, toYear, out var knownRecords))
        {
            foreach (var record in knownRecords)
            {
                await downloader.DownloadYearbookFilesAsync(record);
            }
        }
        else
        {
            await downloader.DownloadAllYearbooksAsync();
        }
    }
    else
    {
        Console.WriteLine("Basic HTTP client method selected...\n");

        using var downloader = new global::YearbookDownloader.Services.YearbookDownloader(searchUrl);
        if (!string.IsNullOrWhiteSpace(directRecordUrl))
        {
            await downloader.DownloadYearbookFilesAsync(CreateDirectRecord(directRecordUrl, directRecordYear ?? fromYear));
        }
        else if (TryGetKnownRecords(fromYear, toYear, out var knownRecords))
        {
            foreach (var record in knownRecords)
            {
                await downloader.DownloadYearbookFilesAsync(record);
            }
        }
        else
        {
            await downloader.DownloadAllYearbooksAsync();
        }
    }
    
    Console.WriteLine("\nDownload process completed successfully!");
    Console.WriteLine("All yearbook files have been organized into Bonhomie-[year] directories.");
    Console.WriteLine("\nYou can re-run this program to resume any incomplete downloads.");
}
catch (Exception ex)
{
    Console.WriteLine($"\nAn error occurred: {ex.Message}");
    
    if (!usePlaywright)
    {
        Console.WriteLine("\nIf you're getting JavaScript challenges, try the browser automation method:");
        Console.WriteLine("Run: dotnet run -- --playwright");
    }
    else if (ex.Message.Contains("playwright", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("browser", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("\nBrowser installation may be needed. Run:");
        Console.WriteLine("install-browsers.bat");
        Console.WriteLine("Or: pwsh bin/Debug/net10.0/playwright.ps1 install chromium");
    }
    
    Console.WriteLine("\nStack trace for debugging:");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

if (!Console.IsInputRedirected)
{
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey();
}

static string? GetOptionValue(string[] args, string longName, string shortName)
{
    for (var i = 0; i < args.Length; i++)
    {
        if ((args[i] == longName || args[i] == shortName) && i + 1 < args.Length)
        {
            return args[i + 1];
        }

        if (args[i].StartsWith(longName + "=", StringComparison.OrdinalIgnoreCase))
        {
            return args[i][(longName.Length + 1)..];
        }
    }

    return null;
}

static int? GetYearOptionValue(string[] args, string longName, string? shortName)
{
    var value = GetOptionValue(args, longName, shortName ?? string.Empty);
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    if (int.TryParse(value, out var year) && year >= 1900 && year <= 2100)
    {
        return year;
    }

    Console.WriteLine($"Invalid value for {longName}: {value}");
    Environment.Exit(1);
    return null;
}

static string FindDefaultYearbookRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            var candidates = new[]
            {
                Path.Combine(directory.FullName, "yearbook-data"),
                Path.Combine(directory.FullName, "YearbookData", "yearbook-data"),
                Path.Combine(directory.FullName, "..", "YearbookData", "yearbook-data")
            };

            var existing = candidates.Select(Path.GetFullPath).FirstOrDefault(Directory.Exists);
            if (existing != null)
            {
                return existing;
            }

            directory = directory.Parent;
        }
    }

    return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "yearbook-data"));
}

static YearbookRecord CreateDirectRecord(string recordUrl, int year)
{
    return new YearbookRecord
    {
        Title = $"Bonhomie {year}",
        RecordUrl = recordUrl,
        Year = year,
        DirectoryName = $"Bonhomie-{year}"
    };
}
static bool TryGetKnownRecords(int fromYear, int toYear, out List<YearbookRecord> records)
{
    var knownRecordIdsByYear = new Dictionary<int, int>
    {
        [1901] = 19652,
        [1902] = 19647,
        [1903] = 19633,
        [1904] = 19660,
        [1905] = 19638,
        [1906] = 19665,
        [1907] = 19657,
        [1908] = 19629,
        [1909] = 19669,
        [1910] = 19644,
        [1911] = 19648,
        [1912] = 19634,
        [1913] = 19672,
        [1914] = 19639,
        [1915] = 19650,
        [1916] = 19649,
        [1917] = 19682,
        [1918] = 19646,
        [1919] = 19627,
        [1920] = 19631,
        [1921] = 19733,
        [1922] = 19640,
        [1923] = 19654,
        [1924] = 19676,
        [1925] = 19630,
        [1926] = 19663,
        [1927] = 19636,
        [1928] = 19658,
        [1929] = 19664,
        [1930] = 19678,
        [1931] = 19635,
        [1932] = 19653,
        [1933] = 19637,
        [1934] = 19651,
        [1935] = 19671,
        [1936] = 19656,
        [1937] = 19668,
        [1938] = 19628,
        [1939] = 19732,
        [1940] = 19674,
        [1941] = 19642,
        [1942] = 19659,
        [1943] = 19670,
        [1944] = 19632,
        [1945] = 19675,
        [1946] = 19666,
        [1947] = 19704,
        [1948] = 19641,
        [1949] = 19655,
        [1950] = 19643,
        [1951] = 19662,
        [1952] = 19684,
        [1953] = 19661,
        [1954] = 19679,
        [1955] = 19689,
        [1956] = 19667,
        [1957] = 19681,
        [1958] = 19673,
        [1959] = 19680,
        [1960] = 19645,
        [1961] = 19691,
        [1962] = 19692,
        [1963] = 19726,
        [1964] = 19685,
        [1965] = 19690,
        [1966] = 19677,
        [1967] = 19686,
        [1968] = 19695,
        [1969] = 19688,
        [1970] = 19698,
        [1971] = 19696,
        [1972] = 19710,
        [1973] = 19712,
        [1974] = 19705,
        [1975] = 19702,
        [1976] = 19699,
        [1977] = 19707,
        [1978] = 19701,
        [1979] = 19716,
        [1980] = 19703,
        [1981] = 19714,
        [1982] = 19706,
        [1983] = 19709,
        [1984] = 19693,
        [1985] = 19700,
        [1986] = 19687,
        [1987] = 19697,
        [1988] = 19729,
        [1989] = 19694,
        [1990] = 19708,
        [1991] = 19738,
        [1992] = 19713,
        [1993] = 19720,
        [1994] = 19730,
        [1995] = 19711,
        [1996] = 19727,
        [1997] = 19725,
        [1998] = 19722,
        [1999] = 19715,
        [2000] = 19719,
        [2001] = 19718,
        [2002] = 19724,
        [2003] = 19736,
        [2004] = 19731,
        [2005] = 19735,
        [2006] = 19728,
        [2007] = 19721,
        [2008] = 19683,
        [2009] = 19723,
        [2010] = 19737
    };

    records = new List<YearbookRecord>();
    for (var year = fromYear; year <= toYear; year++)
    {
        if (!knownRecordIdsByYear.TryGetValue(year, out var recordId))
        {
            records.Clear();
            return false;
        }

        records.Add(CreateDirectRecord($"https://furman.tind.io/record/{recordId}?ln=en", year));
    }

    return records.Count > 0;
}
