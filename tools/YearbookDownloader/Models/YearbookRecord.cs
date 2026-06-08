namespace YearbookDownloader.Models;

public class YearbookRecord
{
    public string Title { get; set; } = string.Empty;
    public string RecordUrl { get; set; } = string.Empty;
    public int Year { get; set; }
    public string DirectoryName { get; set; } = string.Empty;
    public List<YearbookFile> Files { get; set; } = new List<YearbookFile>();
}

public class YearbookFile
{
    public string FileName { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public bool IsDownloaded { get; set; }
}