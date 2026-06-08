using System.Text.RegularExpressions;

namespace YearbookDownloader.Utils;

public static class FileHelper
{
    public static string SanitizeFileName(string fileName)
    {
        // Remove invalid file name characters
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var invalidChar in invalidChars)
        {
            fileName = fileName.Replace(invalidChar, '_');
        }
        
        // Also replace some common problematic characters
        fileName = fileName.Replace(' ', '_')
                          .Replace('?', '_')
                          .Replace('&', '_');
        
        return fileName;
    }

    public static string ExtractYearFromTitle(string title)
    {
        // First try to extract 4-digit year from title
        var yearMatch = Regex.Match(title, @"\b(19\d{2}|20\d{2})\b");
        if (yearMatch.Success)
        {
            return yearMatch.Value;
        }

        // If no 4-digit year found, try to extract volume number and convert to year
        // Pattern for "volume 79", "vol 79", "v79", etc.
        var volumeMatch = Regex.Match(title, @"\b(?:volume|vol|v\.?)\s*(\d{1,3})\b", RegexOptions.IgnoreCase);
        if (volumeMatch.Success)
        {
            if (int.TryParse(volumeMatch.Groups[1].Value, out int volumeNumber))
            {
                // Convert volume number to year
                // Assuming Bonhomie started in 1901 (volume 1 = 1901)
                int year = 1900 + volumeNumber;
                return year.ToString();
            }
        }

        // Try to extract any 2-digit number that could be a volume/year
        var twoDigitMatch = Regex.Match(title, @"\b(\d{2})\b");
        if (twoDigitMatch.Success)
        {
            if (int.TryParse(twoDigitMatch.Value, out int number))
            {
                // Convert 2-digit to year (assuming it's a volume number)
                int year = 1900 + number;
                return year.ToString();
            }
        }

        return "Unknown";
    }

    public static void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            Console.WriteLine($"Created directory: {directoryPath}");
        }
    }

    public static bool IsFileDownloaded(string filePath)
    {
        return File.Exists(filePath) && new FileInfo(filePath).Length > 0;
    }

    public static string GetFileSizeString(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}