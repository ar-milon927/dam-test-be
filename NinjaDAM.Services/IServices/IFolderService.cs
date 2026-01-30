using NinjaDAM.DTO.Folder;

namespace NinjaDAM.Services.IServices
{
    public interface IFolderService
    {
        Task<IEnumerable<FolderDto>> GetUserFoldersAsync(string userId);
        Task<FolderDto> GetFolderByIdAsync(Guid folderId, string userId);
        Task<FolderDto> CreateFolderAsync(CreateFolderDto dto, string userId, Guid? companyId);
        Task<FolderDto> UpdateFolderAsync(Guid folderId, CreateFolderDto dto, string userId);
        Task<bool> DeleteFolderAsync(Guid folderId, string userId);
        Task<bool> MoveFolderAsync(Guid folderId, Guid? newParentId, string userId);
    }
}
