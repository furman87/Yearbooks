# Yearbooks

Clean code repository for the Yearbook Viewer website and supporting utilities.

## Layout

```text
src/
  YearbookViewer/          ASP.NET Core website

tools/
  YearbookDownloader/      Downloads full-size yearbook pages
  ThumbnailGenerator/      Creates thumbnails from full-size pages
  YearbookOcr/             Creates OCR text and optional preprocessed images

deploy/
  docker-compose.yml       Production Docker Compose file
  DEPLOYMENT.md            Ubuntu/nginx deployment guide
```

Yearbook images, thumbnails, OCR text, and preprocessed images are intentionally not stored in this repo. Keep them in a sibling folder locally:

```text
D:\Dev\YearbookData\yearbook-data\
  Bonhomie-1988\
    full\
    thumbnails\
    text\
    preprocess\
```

On the server, keep the same data outside the cloned repo, for example:

```text
/opt/yearbook-data/
```

## Build

```powershell
dotnet build Yearbooks.sln
```

## Run The Viewer Locally

From the repo root:

```powershell
dotnet run --project .\src\YearbookViewer\YearbookViewer.csproj
```

The viewer defaults to `D:\Dev\YearbookData\yearbook-data` when run from the project folder.

## Run OCR

```powershell
dotnet run --project .\tools\YearbookOcr\YearbookOcr.csproj
```

You can also pass the data path explicitly:

```powershell
dotnet run --project .\tools\YearbookOcr\YearbookOcr.csproj -- ..\YearbookData\yearbook-data --year 1988 --preprocess
```

## Deploy

See [deploy/DEPLOYMENT.md](deploy/DEPLOYMENT.md).
