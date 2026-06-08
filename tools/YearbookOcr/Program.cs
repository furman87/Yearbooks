using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace YearbookOcr;

internal static partial class Program
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp"];

    private static async Task<int> Main(string[] args)
    {
        var options = OcrOptions.Parse(args);
        if (options.ShowHelp)
        {
            ShowUsage();
            return 0;
        }

        if (!Directory.Exists(options.RootPath))
        {
            Console.Error.WriteLine($"Error: yearbook root does not exist: {options.RootPath}");
            return 1;
        }

        if (!await IsTesseractAvailable(options.TesseractPath))
        {
            Console.Error.WriteLine($"Error: Tesseract was not found or could not run: {options.TesseractPath}");
            Console.Error.WriteLine("Install Tesseract OCR or pass --tesseract <path-to-tesseract.exe>.");
            return 1;
        }

        var pages = DiscoverPages(options).ToList();
        if (options.Limit is > 0)
        {
            pages = pages.Take(options.Limit.Value).ToList();
        }

        if (pages.Count == 0)
        {
            Console.WriteLine("No pages found to OCR.");
            return 0;
        }

        Console.WriteLine($"Yearbook root: {Path.GetFullPath(options.RootPath)}");
        Console.WriteLine($"Tesseract: {options.TesseractPath}");
        Console.WriteLine($"Language: {options.Language}");
        Console.WriteLine($"Page segmentation mode: {options.PageSegmentationMode}");
        Console.WriteLine($"Preprocess: {(options.Preprocess.Enabled ? options.Preprocess : "disabled")}");
        Console.WriteLine($"Jobs: {options.Jobs}");
        Console.WriteLine($"Pages queued: {pages.Count}");
        Console.WriteLine();

        if (options.DryRun)
        {
            foreach (var page in pages.Take(25))
            {
                var ocrInput = options.Preprocess.Enabled ? page.PreprocessPath : page.ImagePath;
                Console.WriteLine($"{page.YearbookName}: {Path.GetFileName(page.ImagePath)} -> {ocrInput} -> {page.TextPath}");
            }

            if (pages.Count > 25)
            {
                Console.WriteLine($"...and {pages.Count - 25} more pages.");
            }

            return 0;
        }

        var stats = new OcrStats();
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
            Console.WriteLine("Cancel requested. Waiting for active OCR jobs to finish...");
        };

        await Parallel.ForEachAsync(
            pages,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = options.Jobs,
                CancellationToken = cancellation.Token
            },
            async (page, token) =>
            {
                var result = await ProcessPage(page, options, token);
                stats.Record(result);
                Console.WriteLine(result.Message);
            });

        Console.WriteLine();
        Console.WriteLine("OCR complete.");
        Console.WriteLine($"Processed: {stats.Processed}");
        Console.WriteLine($"Skipped:   {stats.Skipped}");
        Console.WriteLine($"Errors:    {stats.Errors}");

        return stats.Errors == 0 ? 0 : 2;
    }

    private static IEnumerable<YearbookPageJob> DiscoverPages(OcrOptions options)
    {
        var yearbookDirs = Directory.EnumerateDirectories(options.RootPath, "Bonhomie-*", SearchOption.TopDirectoryOnly)
            .Where(path => options.Year is null || path.EndsWith($"Bonhomie-{options.Year}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var yearbookDir in yearbookDirs)
        {
            var fullDir = Path.Combine(yearbookDir, "full");
            if (!Directory.Exists(fullDir))
            {
                continue;
            }

            var textDir = Path.Combine(yearbookDir, "text");
            var imageFiles = Directory.EnumerateFiles(fullDir)
                .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var imagePath in imageFiles)
            {
            var textFileName = Path.GetFileNameWithoutExtension(imagePath) + ".txt";
            var textPath = Path.Combine(textDir, textFileName);
            var preprocessDir = Path.Combine(yearbookDir, "preprocess");
            var preprocessPath = Path.Combine(preprocessDir, Path.GetFileNameWithoutExtension(imagePath) + ".jpg");
            var shouldSkip = !options.Force && File.Exists(textPath) && new FileInfo(textPath).Length > 0;
            yield return new YearbookPageJob(Path.GetFileName(yearbookDir), imagePath, preprocessDir, preprocessPath, textDir, textPath, shouldSkip);
            }
        }
    }

    private static async Task<OcrResult> ProcessPage(YearbookPageJob page, OcrOptions options, CancellationToken cancellationToken)
    {
        var imageName = Path.GetFileName(page.ImagePath);

        if (page.ShouldSkip)
        {
            return OcrResult.Skip($"Skip {page.YearbookName}/{imageName}");
        }

        Directory.CreateDirectory(page.TextDirectory);
        var tempBase = Path.Combine(page.TextDirectory, $".ocr-{Guid.NewGuid():N}");
        var tempTextPath = tempBase + ".txt";

        try
        {
            var ocrImagePath = options.Preprocess.Enabled
                ? await EnsurePreprocessedImage(page, options, cancellationToken)
                : page.ImagePath;

            var arguments = new List<string>
            {
                ocrImagePath,
                tempBase,
                "-l",
                options.Language,
                "--psm",
                options.PageSegmentationMode.ToString()
            };

            var result = await RunProcess(options.TesseractPath, arguments, cancellationToken);
            if (result.ExitCode != 0)
            {
                return OcrResult.Error($"Error {page.YearbookName}/{imageName}: {result.Error.Trim()}");
            }

            if (!File.Exists(tempTextPath))
            {
                return OcrResult.Error($"Error {page.YearbookName}/{imageName}: Tesseract did not create output text.");
            }

            var text = await File.ReadAllTextAsync(tempTextPath, cancellationToken);
            await File.WriteAllTextAsync(page.TextPath, NormalizeOcrText(text), Encoding.UTF8, cancellationToken);
            File.Delete(tempTextPath);

            return OcrResult.Process($"OCR  {page.YearbookName}/{imageName}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return OcrResult.Error($"Error {page.YearbookName}/{imageName}: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tempTextPath))
            {
                TryDelete(tempTextPath);
            }
        }
    }

    private static async Task<string> EnsurePreprocessedImage(YearbookPageJob page, OcrOptions options, CancellationToken cancellationToken)
    {
        if (!options.Force && File.Exists(page.PreprocessPath) && new FileInfo(page.PreprocessPath).Length > 0)
        {
            return page.PreprocessPath;
        }

        Directory.CreateDirectory(page.PreprocessDirectory);

        using var image = await Image.LoadAsync(page.ImagePath, cancellationToken);
        image.Mutate(context =>
        {
            if (options.Preprocess.Scale != 1)
            {
                context.Resize(new ResizeOptions
                {
                    Size = new Size(image.Width * options.Preprocess.Scale, image.Height * options.Preprocess.Scale),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Lanczos3
                });
            }

            if (options.Preprocess.Grayscale)
            {
                context.Grayscale();
            }

            if (options.Preprocess.Contrast != 1)
            {
                context.Contrast(options.Preprocess.Contrast);
            }

            if (options.Preprocess.Sharpen)
            {
                context.GaussianSharpen(options.Preprocess.SharpenSigma);
            }

            if (options.Preprocess.Threshold)
            {
                context.BinaryThreshold(options.Preprocess.ThresholdValue);
            }
        });

        await image.SaveAsJpegAsync(page.PreprocessPath, new JpegEncoder { Quality = 95 }, cancellationToken);
        return page.PreprocessPath;
    }

    private static string NormalizeOcrText(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        normalized = MultipleBlankLinesRegex().Replace(normalized, "\n\n");
        return normalized + Environment.NewLine;
    }

    private static async Task<bool> IsTesseractAvailable(string tesseractPath)
    {
        try
        {
            var result = await RunProcess(tesseractPath, ["--version"], CancellationToken.None);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<ProcessResult> RunProcess(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                output.AppendLine(eventArgs.Data);
            }
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                error.AppendLine(eventArgs.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
        return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project YearbookOcr -- [yearbook-root] [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  yearbook-root        Root folder containing Bonhomie-* folders. Defaults to the sibling YearbookData folder.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --year <yyyy>        OCR only one yearbook.");
        Console.WriteLine("  --force              Re-run OCR even when text output already exists.");
        Console.WriteLine("  --dry-run            Show what would be processed without running OCR.");
        Console.WriteLine("  --jobs <n>           Number of parallel OCR jobs. Defaults to processor count minus one.");
        Console.WriteLine("  --limit <n>          Process only the first n discovered pages.");
        Console.WriteLine("  --preprocess         Create/use preprocess images before OCR.");
        Console.WriteLine("  --scale <n>          Preprocess scale factor. Defaults to 2 when preprocessing is enabled.");
        Console.WriteLine("  --grayscale          Convert preprocessed image to grayscale.");
        Console.WriteLine("  --no-grayscale       Disable default grayscale preprocessing.");
        Console.WriteLine("  --sharpen            Sharpen preprocessed image.");
        Console.WriteLine("  --no-sharpen         Disable default sharpening preprocessing.");
        Console.WriteLine("  --sharpen-sigma <n>  Sharpen strength. Defaults to 1.2.");
        Console.WriteLine("  --contrast <n>       Contrast multiplier. Defaults to 1.15.");
        Console.WriteLine("  --threshold          Apply binary threshold after other preprocessing.");
        Console.WriteLine("  --threshold-value <n> Threshold value from 0.0 to 1.0. Defaults to 0.55.");
        Console.WriteLine("  --lang <lang>        Tesseract language. Defaults to eng.");
        Console.WriteLine("  --psm <n>            Tesseract page segmentation mode. Defaults to 3.");
        Console.WriteLine("  --tesseract <path>   Path to tesseract executable. Defaults to tesseract on PATH.");
        Console.WriteLine("  --help, -h           Show this help.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --project tools/YearbookOcr");
        Console.WriteLine("  dotnet run --project tools/YearbookOcr -- ../YearbookData/yearbook-data --year 1988 --jobs 2");
        Console.WriteLine("  dotnet run --project tools/YearbookOcr -- ../YearbookData/yearbook-data --year 1988 --preprocess --scale 2 --grayscale --sharpen");
        Console.WriteLine("  dotnet run --project tools/YearbookOcr -- ../YearbookData/yearbook-data --force --lang eng --psm 3");
    }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleBlankLinesRegex();
}

internal sealed record OcrOptions(
    string RootPath,
    string TesseractPath,
    string Language,
    int PageSegmentationMode,
    int Jobs,
    int? Limit,
    int? Year,
    PreprocessOptions Preprocess,
    bool Force,
    bool DryRun,
    bool ShowHelp)
{
    public static OcrOptions Parse(string[] args)
    {
        var rootPath = FindDefaultYearbookRoot();
        var tesseractPath = "tesseract";
        var language = "eng";
        var pageSegmentationMode = 3;
        var jobs = Math.Max(1, Environment.ProcessorCount - 1);
        int? year = null;
        int? limit = null;
        var preprocess = false;
        var scale = 2;
        var grayscale = true;
        var sharpen = true;
        var sharpenSigma = 1.2f;
        var contrast = 1.15f;
        var threshold = false;
        var thresholdValue = 0.55f;
        var force = false;
        var dryRun = false;
        var showHelp = false;

        var index = 0;
        if (args.Length > 0 && !args[0].StartsWith('-'))
        {
            rootPath = args[0];
            index = 1;
        }

        while (index < args.Length)
        {
            var arg = args[index].ToLowerInvariant();
            switch (arg)
            {
                case "--help" or "-h":
                    showHelp = true;
                    index++;
                    break;
                case "--force":
                    force = true;
                    index++;
                    break;
                case "--dry-run":
                    dryRun = true;
                    index++;
                    break;
                case "--year":
                    year = ReadInt(args, ref index, "--year");
                    break;
                case "--jobs":
                    jobs = Math.Max(1, ReadInt(args, ref index, "--jobs"));
                    break;
                case "--limit":
                    limit = Math.Max(1, ReadInt(args, ref index, "--limit"));
                    break;
                case "--preprocess":
                    preprocess = true;
                    index++;
                    break;
                case "--scale":
                    preprocess = true;
                    scale = Math.Max(1, ReadInt(args, ref index, "--scale"));
                    break;
                case "--grayscale":
                    preprocess = true;
                    grayscale = true;
                    index++;
                    break;
                case "--no-grayscale":
                    preprocess = true;
                    grayscale = false;
                    index++;
                    break;
                case "--sharpen":
                    preprocess = true;
                    sharpen = true;
                    index++;
                    break;
                case "--no-sharpen":
                    preprocess = true;
                    sharpen = false;
                    index++;
                    break;
                case "--sharpen-sigma":
                    preprocess = true;
                    sharpenSigma = Math.Max(0, ReadFloat(args, ref index, "--sharpen-sigma"));
                    break;
                case "--contrast":
                    preprocess = true;
                    contrast = Math.Max(0, ReadFloat(args, ref index, "--contrast"));
                    break;
                case "--threshold":
                    preprocess = true;
                    threshold = true;
                    index++;
                    break;
                case "--threshold-value":
                    preprocess = true;
                    thresholdValue = Math.Clamp(ReadFloat(args, ref index, "--threshold-value"), 0, 1);
                    break;
                case "--lang":
                    language = ReadString(args, ref index, "--lang");
                    break;
                case "--psm":
                    pageSegmentationMode = ReadInt(args, ref index, "--psm");
                    break;
                case "--tesseract":
                    tesseractPath = ReadString(args, ref index, "--tesseract");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[index]}");
            }
        }

        var preprocessOptions = new PreprocessOptions(preprocess, scale, grayscale, sharpen, sharpenSigma, contrast, threshold, thresholdValue);
        return new OcrOptions(rootPath, tesseractPath, language, pageSegmentationMode, jobs, limit, year, preprocessOptions, force, dryRun, showHelp);
    }

    private static int ReadInt(string[] args, ref int index, string optionName)
    {
        var value = ReadString(args, ref index, optionName);
        if (!int.TryParse(value, out var number))
        {
            throw new ArgumentException($"{optionName} requires a number.");
        }

        return number;
    }

    private static float ReadFloat(string[] args, ref int index, string optionName)
    {
        var value = ReadString(args, ref index, optionName);
        if (!float.TryParse(value, out var number))
        {
            throw new ArgumentException($"{optionName} requires a number.");
        }

        return number;
    }

    private static string ReadString(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{optionName} requires a value.");
        }

        index += 2;
        return args[index - 1];
    }

    private static string FindDefaultYearbookRoot()
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
}

internal sealed record YearbookPageJob(
    string YearbookName,
    string ImagePath,
    string PreprocessDirectory,
    string PreprocessPath,
    string TextDirectory,
    string TextPath,
    bool ShouldSkip);

internal sealed record PreprocessOptions(
    bool Enabled,
    int Scale,
    bool Grayscale,
    bool Sharpen,
    float SharpenSigma,
    float Contrast,
    bool Threshold,
    float ThresholdValue)
{
    public override string ToString()
    {
        return $"scale={Scale}, grayscale={Grayscale}, sharpen={Sharpen}, sharpenSigma={SharpenSigma:0.##}, contrast={Contrast:0.##}, threshold={Threshold}, thresholdValue={ThresholdValue:0.##}";
    }
}

internal sealed record ProcessResult(int ExitCode, string Output, string Error);

internal sealed record OcrResult(OcrResultKind Kind, string Message)
{
    public static OcrResult Process(string message) => new(OcrResultKind.Processed, message);

    public static OcrResult Skip(string message) => new(OcrResultKind.Skipped, message);

    public static OcrResult Error(string message) => new(OcrResultKind.Error, message);
}

internal enum OcrResultKind
{
    Processed,
    Skipped,
    Error
}

internal sealed class OcrStats
{
    private int _processed;
    private int _skipped;
    private int _errors;

    public int Processed => _processed;

    public int Skipped => _skipped;

    public int Errors => _errors;

    public void Record(OcrResult result)
    {
        switch (result.Kind)
        {
            case OcrResultKind.Processed:
                Interlocked.Increment(ref _processed);
                break;
            case OcrResultKind.Skipped:
                Interlocked.Increment(ref _skipped);
                break;
            case OcrResultKind.Error:
                Interlocked.Increment(ref _errors);
                break;
        }
    }
}
