using YearbookDownloader.Services;

// Furman University Yearbook Downloader
// Downloads JPEG images from Furman yearbooks (1980-1994) from tind.io

Console.WriteLine("===== Furman Yearbook Downloader =====");
Console.WriteLine("Downloading yearbooks from 1980-1994\n");

// const string searchUrl = "https://furman.tind.io/search?cc=Furman%20Yearbooks&ln=en&p=&f=&rm=&sf=year&so=a&rg=15&c=Furman%20Yearbooks&c=&of=hb&fti=1&fct__4-range=1980%2C1994&fti=1";
const string searchUrl = "https://furman.tind.io/search?cc=Furman%20Yearbooks&ln=en&p=&f=&rm=&sf=year&so=a&rg=15&c=Furman%20Yearbooks&c=&of=hb&fti=1&fct__4-range=1979%2C1979&fti=1";

// Check command line arguments for method preference
var cmdArgs = Environment.GetCommandLineArgs();
bool usePlaywright = cmdArgs.Contains("--playwright") || cmdArgs.Contains("-p");
bool useBasic = cmdArgs.Contains("--basic") || cmdArgs.Contains("-b");
var outputRoot = GetOptionValue(cmdArgs, "--output", "-o") ?? FindDefaultYearbookRoot();

Directory.CreateDirectory(outputRoot);
Directory.SetCurrentDirectory(outputRoot);

Console.WriteLine($"Output folder: {Path.GetFullPath(outputRoot)}");
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
        Console.WriteLine("🌐 Using browser automation method...");
        Console.WriteLine("📋 Note: First run will install browser dependencies automatically.\n");

        using var downloader = new PlaywrightYearbookDownloader(searchUrl);
        await downloader.DownloadAllYearbooksAsync();
    }
    else
    {
        Console.WriteLine("⚡ Using basic HTTP client method...\n");

        using var downloader = new global::YearbookDownloader.Services.YearbookDownloader(searchUrl);
        await downloader.DownloadAllYearbooksAsync();
    }
    
    Console.WriteLine("\n✅ Download process completed successfully!");
    Console.WriteLine("All yearbook files have been organized into Bonhomie-[year] directories.");
    Console.WriteLine("\nYou can re-run this program to resume any incomplete downloads.");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ An error occurred: {ex.Message}");
    
    if (!usePlaywright)
    {
        Console.WriteLine("\n💡 If you're getting JavaScript challenges, try the browser automation method:");
        Console.WriteLine("Run: dotnet run -- --playwright");
    }
    else if (ex.Message.Contains("playwright") || ex.Message.Contains("browser"))
    {
        Console.WriteLine("\n💡 Browser installation may be needed. Run:");
        Console.WriteLine("install-browsers.bat");
        Console.WriteLine("Or: pwsh bin/Debug/net10.0/playwright.ps1 install chromium");
    }
    
    Console.WriteLine("\nStack trace for debugging:");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

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

static string FindDefaultYearbookRoot()
{
    string[] candidates =
    [
        Path.Combine("..", "YearbookData", "yearbook-data"),
        Path.Combine("..", "..", "YearbookData", "yearbook-data"),
        Path.Combine("..", "..", "..", "YearbookData", "yearbook-data"),
        Path.Combine("data", "yearbook-data"),
        "yearbook-data"
    ];

    return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
}
