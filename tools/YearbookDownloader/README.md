# Yearbook Downloader

Downloads full-size yearbook page images into the external yearbook data folder.

By default, output goes to:

```text
D:\Dev\YearbookData\yearbook-data
```

From the repo root:

```powershell
dotnet run --project .\tools\YearbookDownloader\YearbookDownloader.csproj -- --playwright
```

You can override the output folder:

```powershell
dotnet run --project .\tools\YearbookDownloader\YearbookDownloader.csproj -- --playwright --output D:\Dev\YearbookData\yearbook-data
```

Download methods:

```text
--playwright, -p   Browser automation. More reliable for JavaScript challenges.
--basic, -b        Basic HTTP client. Faster, but less reliable.
--output, -o       Folder where Bonhomie-YYYY folders are written.
```

Install Playwright browser dependencies:

```powershell
.\tools\YearbookDownloader\install-browsers.bat
```

The downloader writes `Bonhomie-YYYY` folders. The rest of the pipeline expects each yearbook folder to contain `full`, `thumbnails`, `text`, and optional `preprocess` directories, so downloaded files may need to be moved into that yearbook's `full` folder before thumbnail/OCR generation.
