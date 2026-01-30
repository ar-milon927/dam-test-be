namespace NinjaDAM.Services.IServices
{
    public interface IIptcExtractionService
    {
        Task<string?> ExtractIptcMetadataAsync(string filePath);
    }
}
