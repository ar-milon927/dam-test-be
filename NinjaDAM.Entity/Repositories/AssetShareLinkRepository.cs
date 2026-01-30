using Microsoft.EntityFrameworkCore;
using NinjaDAM.Entity.Data;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.DAL.Repositories;

namespace NinjaDAM.Entity.Repositories
{
    public class AssetShareLinkRepository : Repository<AssetShareLink>, IAssetShareLinkRepository
    {
        private readonly AppDbContext _context;

        public AssetShareLinkRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<AssetShareLink?> GetByTokenAsync(string token)
        {
            return await _context.AssetShareLinks
                .Include(asl => asl.Asset)
                .FirstOrDefaultAsync(asl => asl.Token == token);
        }

        public async Task<IEnumerable<AssetShareLink>> GetActiveShareLinksAsync(Guid assetId)
        {
            return await _context.AssetShareLinks
                .Where(asl => asl.AssetId == assetId && 
                             asl.IsActive && 
                             asl.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(asl => asl.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> RevokeExpiredLinksAsync()
        {
            var expiredLinks = await _context.AssetShareLinks
                .Where(asl => asl.IsActive && asl.ExpiresAt <= DateTime.UtcNow)
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
