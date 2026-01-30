using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using NinjaDAM.Services.IServices;

namespace NinjaDAM.Services.Services
{
    public class RecycleBinCleanupService : BackgroundService
    {
        private readonly ILogger<RecycleBinCleanupService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _checkInterval = TimeSpan.FromDays(1); // Run once daily

        public RecycleBinCleanupService(
            ILogger<RecycleBinCleanupService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Recycle Bin Cleanup Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Running daily cleanup check...");

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var assetService = scope.ServiceProvider.GetRequiredService<IAssetService>();
                        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<NinjaDAM.Entity.Entities.Users>>();

                        // Since cleanup is system-wide, we might need a system user or admin context
                        // For now, we'll iterate through users or modify the service to support system-wide cleanup
                        // A better approach for the service layer is to add a system-level cleanup method
                        // But sticking to the interface:
                        
                        // We will add PermanentlyDeleteExpiredAssetsAsync to IAssetService to handle this system-wide
                        
                        int deletedCount = await assetService.PermanentlyDeleteExpiredAssetsAsync();
                        
                        if (deletedCount > 0)
                        {
                            _logger.LogInformation("Cleaned up {Count} expired assets from recycle bin.", deletedCount);
                        }
                        else
                        {
                            _logger.LogInformation("No expired assets found.");
                        }
                    }

                    // Wait for next run
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up recycle bin.");
                    
                    // Wait a bit before retrying if there's an error, but still respect cancellation
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _logger.LogInformation("Recycle Bin Cleanup Service is stopping.");
        }
    }
}
