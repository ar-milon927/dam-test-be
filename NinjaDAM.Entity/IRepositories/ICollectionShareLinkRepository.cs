using NinjaDAM.Entity.Entities;

namespace NinjaDAM.Entity.IRepositories
{
    public interface ICollectionShareLinkRepository : IRepository<CollectionShareLink>
    {
        Task<CollectionShareLink?> GetByTokenAsync(string token);
        Task<IEnumerable<CollectionShareLink>> GetActiveShareLinksAsync(Guid collectionId);
        Task<int> RevokeExpiredLinksAsync();
    }
}
