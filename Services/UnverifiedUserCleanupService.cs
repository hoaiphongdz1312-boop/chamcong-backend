using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.Enums;
using NguyenHoaiPhong_1681.Options;

namespace NguyenHoaiPhong_1681.Services;

/// <summary>Định kỳ xóa hẳn khỏi database các tài khoản đăng ký nhưng chưa xác thực email/SMS.</summary>
public class UnverifiedUserCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuthOptions> options,
    ILogger<UnverifiedUserCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = DateTime.UtcNow.AddMinutes(-options.Value.UnverifiedExpireMinutes);
                var deleted = await db.Users.IgnoreQueryFilters()
                    .Where(u => !u.EmailVerified && !u.PhoneVerified
                                && u.Status == UserStatus.PendingApproval
                                && u.CreatedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);
                if (deleted > 0) logger.LogInformation("Đã xóa {Count} tài khoản chưa xác thực", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi dọn tài khoản chưa xác thực");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
