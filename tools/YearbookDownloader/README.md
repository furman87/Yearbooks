# Yearbook Downloader

Downloads full-size yearbook page images into the external yearbook data folder.

By default, output goes to:

```text
D:\Dev\YearbookData\yearbook-data
```

From the repo root:

```powershell
dotnet run --project .\tools\YearbookDownloader\YearbookDownloader.csproj -- --playwright --from 1995 --to 2000
```

You can also download a known record directly if TIND search is being challenged:

```powershell
dotnet run --project .\tools\YearbookDownloader\YearbookDownloader.csproj -- --playwright --record-url "https://furman.tind.io/record/19711?ln=en" --year 1995 --output D:\Dev\YearbookData\yearbook-data
```
You can override the output folder:

```powershell
dotnet run --project .\tools\YearbookDownloader\YearbookDownloader.csproj -- --playwright --from 1995 --to 2000 --output D:\Dev\YearbookData\yearbook-data
```

Download methods:

```text
--playwright, -p   Manifest/browser downloader. Uses IIIF manifests before browser fallback.
--basic, -b        Basic HTTP client. Faster, but less reliable.
--from             First year to download. Defaults to 1995.
--to               Last year to download. Defaults to the --from year.
--record-url       Download one known TIND record URL directly instead of searching.
--year             Year to use with --record-url.
--output, -o       Folder where Bonhomie-YYYY folders are written.
--headed           Open a visible Playwright browser if browser fallback is needed.
```

Install Playwright browser dependencies:

```powershell
.\tools\YearbookDownloader\install-browsers.bat
```

The downloader writes `Bonhomie-YYYY` folders. The rest of the pipeline expects each yearbook folder to contain `full`, `thumbnails`, `text`, and optional `preprocess` directories, so downloaded files may need to be moved into that yearbook's `full` folder before thumbnail/OCR generation.

The downloader is restart-safe. It checks each expected local JPG before downloading and skips any file that already exists with a non-zero size.

Current TIND record pages expose yearbook pages through OpenGraph metadata and IIIF manifests instead of the older download table. The Playwright downloader now reads those sources and saves full-size IIIF images using stable names like `bonhomie_1995_001.jpg`. For known Bonhomie years from 1901 through 2010, it uses a built-in year-to-record catalog so it can bypass TIND search when that page is challenged.