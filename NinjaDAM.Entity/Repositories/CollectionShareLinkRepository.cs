using Microsoft.EntityFrameworkCore;
using NinjaDAM.DAL.Repositories;
using NinjaDAM.Entity.Data;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;

namespace NinjaDAM.Entity.Repositories
{
    public class CollectionShareLinkRepository : Repository<CollectionShareLink>, ICollectionShareLinkRepository
    {
        private readonly AppDbContext _context;

        public CollectionShareLinkRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<CollectionShareLink?> GetByTokenAsync(string token)
        {
            return await _context.CollectionShareLinks
                .Include(csl => csl.Collection)
                    .ThenInclude(c => c.CollectionAssets)
                        .ThenInclude(ca => ca.Asset)
                .FirstOrDefaultAsync(csl => csl.Token == token);
        }

        public async Task<IEnumerable<CollectionShareLink>> GetActiveShareLinksAsync(Guid collectionId)
        {
            return await _context.CollectionShareLinks
                .Where(csl => csl.CollectionId == collectionId && 
                             csl.IsActive && 
                             csl.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(csl => csl.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> RevokeExpiredLinksAsync()
        {
            var expiredLinks = await _context.CollectionShareLinks
                .Where(csl => csl.IsActive && csl.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var link in expiredLinks)
            {
                link.IsActive = false;
                link.RevokedAt = DateTime.UtcNow;
            }

            return await _context.SaveChangesAsync();
        }
    }
}
