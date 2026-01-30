using NinjaDAM.Entity.Entities;

namespace NinjaDAM.Entity.IRepositories
{
    public interface IAssetShareLinkRepository : IRepository<AssetShareLink>
    {
        Task<AssetShareLink?> GetByTokenAsync(string token);
        Task<IEnumerable<AssetShareLink>> GetActiveShareLinksAsync(Guid assetId);
        Task<int> RevokeExpiredLinksAsync();
    }
}
