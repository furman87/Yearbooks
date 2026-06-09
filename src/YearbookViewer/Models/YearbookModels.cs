namespace YearbookViewer.Models;

public class YearbookInfo
{
    public int Year { get; set; }
    public string Title { get; set; } = "";
    public string DirectoryPath { get; set; } = "";
    public List<YearbookPage> Pages { get; set; } = new();
    public int TotalPages => Pages.Count;
}

public class YearbookPage
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int PageNumber { get; set; }
    public long FileSize { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string DisplayName => $"Page {PageNumber}";
}

public class YearbookGallery
{
    public List<YearbookInfo> Yearbooks { get; set; } = new();
    public int TotalYearbooks => Yearbooks.Count;
    public int TotalPages => Yearbooks.Sum(y => y.TotalPages);
}

public class YearbookSearchResult
{
    public int Year { get; set; }
    public string YearbookTitle { get; set; } = "";
    public int PageNumber { get; set; }
    public string FileName { get; set; } = "";
    public string Snippet { get; set; } = "";
}

public class YearbookSearchResponse
{
    public string Query { get; set; } = "";
    public int? Year { get; set; }
    public List<YearbookSearchResult> Results { get; set; } = new();
    public int SearchedYearbooks { get; set; }
    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);
}
