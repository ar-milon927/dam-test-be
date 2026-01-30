using MetadataExtractor;
using MetadataExtractor.Formats.Iptc;
using Microsoft.Extensions.Logging;
using NinjaDAM.Services.IServices;
using System.Text.Json;

namespace NinjaDAM.Services.Services
{
    public class IptcExtractionService : IIptcExtractionService
    {
        private readonly ILogger<IptcExtractionService> _logger;
        private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
        {
            "FileType"
        };

        public IptcExtractionService(ILogger<IptcExtractionService> logger)
        {
            _logger = logger;
        }

        public async Task<string?> ExtractIptcMetadataAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            return await Task.Run(() =>
            {
                try
                {
                    var directories = ImageMetadataReader.ReadMetadata(filePath);
                    var allMetadata = new Dictionary<string, object>();

                    foreach (var directory in directories)
                    {
                        if (!directory.Tags.Any())
                        {
                            continue;
                        }

                        var cleanDirectoryName = NormalizeDirectoryName(directory.Name);
                        
                        if (ExcludedDirectories.Contains(cleanDirectoryName))
                        {
                            continue;
                        }

                        var directoryMetadata = ExtractDirectoryTags(directory);

                        if (directoryMetadata.Count > 0)
                        {
                            allMetadata[cleanDirectoryName] = directoryMetadata;
                        }
                    }

                    if (allMetadata.Count == 0)
                    {
                        return null;
                    }

                    var jsonResult = JsonSerializer.Serialize(allMetadata, new JsonSerializerOptions
                    {
                        WriteIndented = false
                    });

                    _logger.LogInformation("Extracted metadata from {DirectoryCount} directories", allMetadata.Count);
                    return jsonResult;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract metadata from {FilePath}", filePath);
                    return null;
                }
            });
        }

        private static string NormalizeDirectoryName(string name)
        {
            return name.Replace(" ", "").Replace("-", "");
        }

        private Dictionary<string, string> ExtractDirectoryTags(MetadataExtractor.Directory directory)
        {
            var metadata = new Dictionary<string, string>();

            foreach (var tag in directory.Tags)
            {
                try
                {
                    var description = tag.Description;
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        var cleanKey = NormalizeTagName(tag.Name);
                        metadata[cleanKey] = description;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract tag '{TagName}'", tag.Name);
                }
            }

            return metadata;
        }

        private static string NormalizeTagName(string name)
        {
            return name.Replace(" ", "").Replace("-", "").Replace("/", "");
        }
    }
}
