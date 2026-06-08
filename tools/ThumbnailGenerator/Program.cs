using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ThumbnailGenerator;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Parse command line arguments
        if (args.Length == 0)
        {
            ShowUsage();
            return 1;
        }

        string directoryPath = args[0];
        int width = 300;  // Default width
        int height = 400; // Default height

        // Parse optional width and height arguments
        for (int i = 1; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length) break;

            switch (args[i].ToLowerInvariant())
            {
                case "--width" or "-w":
                    if (int.TryParse(args[i + 1], out int w) && w > 0)
                        width = w;
                    break;
                case "--height" or "-h":
                    if (int.TryParse(args[i + 1], out int h) && h > 0)
                        height = h;
                    break;
            }
        }

        await ProcessImages(directoryPath, width, height);
        return 0;
    }

    static void ShowUsage()
    {
        Console.WriteLine("Usage: ThumbnailGenerator <directory> [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  directory         The directory containing images to process");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --width, -w       Thumbnail width in pixels (default: 300)");
        Console.WriteLine("  --height, -h      Thumbnail height in pixels (default: 400)");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  ThumbnailGenerator C:\\Photos --width 200 --height 300");
    }

    static async Task ProcessImages(string directoryPath, int width, int height)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"Error: Directory '{directoryPath}' does not exist.");
                return;
            }

            // Create subdirectories if they don't exist
            var fullDirectory = Path.Combine(directoryPath, "full");
            var thumbnailsDirectory = Path.Combine(directoryPath, "thumbnails");

            Directory.CreateDirectory(fullDirectory);
            Directory.CreateDirectory(thumbnailsDirectory);

            Console.WriteLine($"Processing images in: {directoryPath}");
            Console.WriteLine($"Thumbnail dimensions: {width}x{height}");

            // Get all image files (common image extensions)
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };
            var imageFiles = Directory.GetFiles(directoryPath)
                .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .ToArray();

            if (imageFiles.Length == 0)
            {
                Console.WriteLine("No image files found in the directory.");
                return;
            }

            Console.WriteLine($"Found {imageFiles.Length} image(s) to process.");

            int processedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            foreach (var imagePath in imageFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(imagePath);
                    var thumbnailPath = Path.Combine(thumbnailsDirectory, fileName);
                    var fullImagePath = Path.Combine(fullDirectory, fileName);

                    // Skip if thumbnail already exists (prevents reprocessing)
                    if (File.Exists(thumbnailPath))
                    {
                        Console.WriteLine($"Skipping {fileName} - thumbnail already exists");
                        skippedCount++;
                        continue;
                    }

                    Console.Write($"Processing {fileName}...");

                    // Create thumbnail
                    using (var image = await Image.LoadAsync(imagePath))
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(width, height),
                            Mode = ResizeMode.Max // Maintains aspect ratio
                        }));

                        await image.SaveAsync(thumbnailPath);
                    }

                    // Move original to full directory
                    File.Move(imagePath, fullImagePath, overwrite: true);

                    Console.WriteLine(" ✓ Done");
                    processedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" ✗ Error: {ex.Message}");
                    errorCount++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Processing complete!");
            Console.WriteLine($"Successfully processed: {processedCount} images");
            if (skippedCount > 0)
            {
                Console.WriteLine($"Skipped (already processed): {skippedCount} images");
            }
            if (errorCount > 0)
            {
                Console.WriteLine($"Errors encountered: {errorCount} images");
            }
            Console.WriteLine($"Thumbnails saved to: {thumbnailsDirectory}");
            Console.WriteLine($"Original images moved to: {fullDirectory}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
        }
    }
}
