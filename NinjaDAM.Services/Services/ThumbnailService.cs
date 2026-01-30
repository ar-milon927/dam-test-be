using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;
using SkiaSharp;
using ImageMagick;

namespace NinjaDAM.Services.Services
{
    public class ThumbnailService : IThumbnailService
    {
        private readonly IRepository<Asset> _assetRepo;
        private readonly ILogger<ThumbnailService> _logger;
        
        private const int ThumbnailMaxWidth = 800;
        private const int ThumbnailMaxHeight = 600;
        private const int ThumbnailQuality = 85;

        private static readonly HashSet<string> MagickNetFormats = new(StringComparer.OrdinalIgnoreCase)
        {
            ".heic", ".heif", ".tif", ".tiff", ".psd", ".arw", ".cr2", ".nef", ".dng", ".jpg", ".jpeg", ".png", ".webp"
        };

        public ThumbnailService(
            IRepository<Asset> assetRepo,
            ILogger<ThumbnailService> logger)
        {
            _assetRepo = assetRepo;
            _logger = logger;
        }

        public async Task<string?> GenerateThumbnailAsync(string sourceFilePath, string thumbnailDirectory, string fileType, string mimeType)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
                {
                    _logger.LogWarning("Source file not found: {FilePath}", sourceFilePath);
                    return null;
                }

                Directory.CreateDirectory(thumbnailDirectory);

                if (fileType == "image")
                {
                    return await GenerateImageThumbnailAsync(sourceFilePath, thumbnailDirectory);
                }
                else if (fileType == "video")
                {
                    return await GenerateVideoThumbnailAsync(sourceFilePath, thumbnailDirectory);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail for {FilePath}", sourceFilePath);
                return null;
            }
        }

        private async Task<string?> GenerateImageThumbnailAsync(string sourceFilePath, string thumbnailDirectory)
        {
            try
            {
                var thumbnailFileName = $"thumb_{Guid.NewGuid()}.webp";
                var thumbnailPath = Path.Combine(thumbnailDirectory, thumbnailFileName);
                var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();

                await Task.Run(() =>
                {
                    if (RequiresMagickNet(extension))
                    {
                        GenerateWithMagickNet(sourceFilePath, thumbnailPath);
                    }
                    else
                    {
                        GenerateWithSkiaSharp(sourceFilePath, thumbnailPath);
                    }
                });

                _logger.LogInformation("Generated image thumbnail: {ThumbnailPath}", thumbnailPath);
                return thumbnailPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating image thumbnail for {FilePath}", sourceFilePath);
                return null;
            }
        }

        private static bool RequiresMagickNet(string extension)
        {
            return MagickNetFormats.Contains(extension);
        }

        private void GenerateWithMagickNet(string sourceFilePath, string thumbnailPath)
        {
            using var image = new MagickImage(sourceFilePath);
            image.AutoOrient();

            var (newWidth, newHeight) = CalculateThumbnailSize(
                (int)image.Width,
                (int)image.Height,
                ThumbnailMaxWidth,
                ThumbnailMaxHeight);

            image.Resize((uint)newWidth, (uint)newHeight);
            image.Strip();
            image.Format = MagickFormat.WebP;
            image.Quality = ThumbnailQuality;
            image.Write(thumbnailPath);
        }

        private void GenerateWithSkiaSharp(string sourceFilePath, string thumbnailPath)
        {
            using var inputStream = File.OpenRead(sourceFilePath);
            using var originalBitmap = SKBitmap.Decode(inputStream);

            if (originalBitmap == null)
            {
                throw new InvalidOperationException($"Failed to decode image: {sourceFilePath}");
            }

            var (newWidth, newHeight) = CalculateThumbnailSize(
                originalBitmap.Width,
                originalBitmap.Height,
                ThumbnailMaxWidth,
                ThumbnailMaxHeight);

            using var resizedBitmap = originalBitmap.Resize(
                new SKImageInfo(newWidth, newHeight),
                SKFilterQuality.High);

            if (resizedBitmap == null)
            {
                throw new InvalidOperationException($"Failed to resize image: {sourceFilePath}");
            }

            using var image = SKImage.FromBitmap(resizedBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Webp, ThumbnailQuality);
            using var outputStream = File.OpenWrite(thumbnailPath);
            data.SaveTo(outputStream);
        }

        private async Task<string?> GenerateVideoThumbnailAsync(string sourceFilePath, string thumbnailDirectory)
        {
            try
            {
                var thumbnailFileName = $"thumb_{Guid.NewGuid()}.webp";
                var thumbnailPath = Path.Combine(thumbnailDirectory, thumbnailFileName);

                var result = await ExtractVideoFrameAsync(sourceFilePath, thumbnailPath);
                
                if (result && File.Exists(thumbnailPath))
                {
                    _logger.LogInformation("Generated video thumbnail: {ThumbnailPath}", thumbnailPath);
                    return thumbnailPath;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating video thumbnail for {FilePath}", sourceFilePath);
                return null;
            }
        }

        private async Task<bool> ExtractVideoFrameAsync(string videoPath, string outputPath)
        {
            try
            {
                var ffmpegPath = FindFFmpeg();
                if (string.IsNullOrEmpty(ffmpegPath))
                {
                    _logger.LogWarning("FFmpeg not found. Video thumbnails will not be generated.");
                    return false;
                }

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-i \"{videoPath}\" -ss 00:00:01 -vframes 1 -vf \"scale={ThumbnailMaxWidth}:{ThumbnailMaxHeight}:force_original_aspect_ratio=decrease\" -q:v 2 \"{outputPath}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new System.Diagnostics.Process { StartInfo = startInfo };
                process.Start();

                var timeoutTask = Task.Delay(30000);
                var processTask = Task.Run(() => process.WaitForExit());

                if (await Task.WhenAny(processTask, timeoutTask) == timeoutTask)
                {
                    process.Kill();
                    _logger.LogWarning("FFmpeg process timed out for {VideoPath}", videoPath);
                    return false;
                }

                return process.ExitCode == 0 && File.Exists(outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting video frame from {VideoPath}", videoPath);
                return false;
            }
        }

        private string? FindFFmpeg()
        {
            var possiblePaths = new[]
            {
                "ffmpeg",
                "ffmpeg.exe",
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
                "/usr/bin/ffmpeg",
                "/usr/local/bin/ffmpeg"
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "-version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = System.Diagnostics.Process.Start(startInfo);
                    if (process != null)
                    {
                        process.WaitForExit(5000);
                        if (process.ExitCode == 0)
                        {
                            return path;
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            return null;
        }

        private static (int width, int height) CalculateThumbnailSize(int originalWidth, int originalHeight, int maxWidth, int maxHeight)
        {
            if (originalWidth <= maxWidth && originalHeight <= maxHeight)
            {
                return (originalWidth, originalHeight);
            }

            var ratioX = (double)maxWidth / originalWidth;
            var ratioY = (double)maxHeight / originalHeight;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(originalWidth * ratio);
            var newHeight = (int)(originalHeight * ratio);

            return (Math.Max(1, newWidth), Math.Max(1, newHeight));
        }

        public async Task<int> RegenerateThumbnailsForUserAsync(string userId, Guid? companyId, string webRootPath)
        {
            var query = _assetRepo.Query();

            if (companyId.HasValue)
            {
                query = query.Where(a => (a.UserId == userId || a.CompanyId == companyId.Value) && (a.FileType == "image" || a.FileType == "video"));
            }
            else
            {
                query = query.Where(a => a.UserId == userId && (a.FileType == "image" || a.FileType == "video"));
            }

            var assets = await query.ToListAsync();

            var count = 0;
            var thumbnailsPath = Path.Combine(webRootPath, "uploads", "thumbnails", userId);
            Directory.CreateDirectory(thumbnailsPath);

            foreach (var asset in assets)
            {
                var oldThumbnailPath = asset.ThumbnailPath;

                var thumbnailPath = await GenerateThumbnailAsync(
                    asset.FilePath,
                    thumbnailsPath,
                    asset.FileType,
                    asset.MimeType);

                if (!string.IsNullOrEmpty(thumbnailPath))
                {
                    asset.ThumbnailPath = thumbnailPath;
                    asset.UpdatedAt = DateTime.UtcNow;
                    _assetRepo.Update(asset);
                    count++;

                    // Clean up old thumbnail if it was replaced
                    if (!string.IsNullOrEmpty(oldThumbnailPath) && File.Exists(oldThumbnailPath) && oldThumbnailPath != thumbnailPath)
                    {
                        try { File.Delete(oldThumbnailPath); } catch { }
                    }
                }
            }

            if (count > 0)
            {
                await _assetRepo.SaveAsync();
            }

            _logger.LogInformation("Regenerated {Count} thumbnails for user {UserId}", count, userId);
            return count;
        }
    }
}
