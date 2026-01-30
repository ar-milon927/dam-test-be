namespace NinjaDAM.Services.IServices
{
    public interface IThumbnailService
    {
        Task<string?> GenerateThumbnailAsync(string sourceFilePath, string thumbnailDirectory, string fileType, string mimeType);
        Task<int> RegenerateThumbnailsForUserAsync(string userId, Guid? companyId, string webRootPath);
    }
}
