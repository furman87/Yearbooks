# Yearbook OCR

Creates OCR text files for yearbook page images. The yearbook data folder is outside the code repo.

Expected data layout:

```text
D:\Dev\YearbookData\yearbook-data\
  Bonhomie-1988\
    full\
    thumbnails\
    text\
    preprocess\
```

`text` receives OCR output. `preprocess` is created or updated when preprocessing is enabled.

From the repo root:

```powershell
dotnet run --project .\tools\YearbookOcr\YearbookOcr.csproj
```

Useful examples:

```powershell
dotnet run --project .\tools\YearbookOcr\YearbookOcr.csproj -- ..\YearbookData\yearbook-data --year 1988
dotnet run --project .\tools\YearbookOcr\YearbookOcr.csproj -- ..\YearbookData\yearbook-data --year 1988 --dry-run
dotnet run --project .\tools\YearbookOcr\YearbookOcr.csproj -- ..\YearbookData\yearbook-data --year 1988 --force
dotnet run --project .\tools\YearbookOcr\YearbookOcr.csproj -- ..\YearbookData\yearbook-data --year 1988 --limit 1 --jobs 1
dotnet run --project .\tools\YearbookOcr\YearbookOcr.csproj -- ..\YearbookData\yearbook-data --year 1988 --preprocess --scale 2 --grayscale --sharpen
```

Options:

```text
--year <yyyy>         OCR only one yearbook.
--force               Re-run OCR even when text output already exists.
--dry-run             Show what would be processed without running OCR.
--jobs <n>            Number of parallel OCR jobs.
--limit <n>           Process only the first n discovered pages.
--preprocess          Create/use preprocess images before OCR.
--scale <n>           Preprocess scale factor. Defaults to 2.
--grayscale           Convert preprocessed image to grayscale.
--no-grayscale        Disable default grayscale preprocessing.
--sharpen             Sharpen preprocessed image.
--no-sharpen          Disable default sharpening preprocessing.
--sharpen-sigma <n>   Sharpen strength. Defaults to 1.2.
--contrast <n>        Contrast multiplier. Defaults to 1.15.
--threshold           Apply binary threshold after other preprocessing.
--threshold-value <n> Threshold value from 0.0 to 1.0. Defaults to 0.55.
--lang <lang>         Tesseract language. Defaults to eng.
--psm <n>             Tesseract page segmentation mode. Defaults to 3.
--tesseract <path>    Path to tesseract executable.
```
