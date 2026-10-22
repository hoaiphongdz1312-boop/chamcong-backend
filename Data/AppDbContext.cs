using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NguyenHoaiPhong_1681.Entities;

namespace NguyenHoaiPhong_1681.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<OtpSendLog> OtpSendLogs => Set<OtpSendLog>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FaceTemplate> FaceTemplates => Set<FaceTemplate>();
    public DbSet<FaceLoginLog> FaceLoginLogs => Set<FaceLoginLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQL Server không lưu Kind → đọc ra luôn gắn UTC để JSON có hậu tố "Z"
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(50).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(15);
            e.Property(x => x.PasswordHash).HasMaxLength(100).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.AvatarUrl).HasMaxLength(500);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("[PhoneVerified] = 1");

            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany(d => d.Members).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<FaceTemplate>(e =>
        {
            e.Property(x => x.Embedding).IsRequired();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FaceLoginLog>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(256).IsRequired();
            e.Property(x => x.Result).HasMaxLength(20).IsRequired();
            e.Property(x => x.Ip).HasMaxLength(64);
            e.Property(x => x.DeviceId).HasMaxLength(100);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        modelBuilder.Entity<OtpCode>(e =>
        {
            e.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Channel).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Destination).HasMaxLength(256).IsRequired();
            e.Property(x => x.CodeHash).HasMaxLength(64);

            e.HasIndex(x => x.SessionId);
            e.HasOne(x => x.User)
                .WithMany(u => u.OtpCodes)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OtpSendLog>(e =>
        {
            e.Property(x => x.Channel).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Destination).HasMaxLength(256).IsRequired();
            e.Property(x => x.Error).HasMaxLength(1000);

            e.HasIndex(x => new { x.Destination, x.CreatedAt });
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(100).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Group).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Permission).WithMany(p => p.RolePermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Department>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Shift>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<AttendanceRecord>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => new { x.UserId, x.ShiftId, x.WorkDate }).IsUnique();
            e.HasIndex(x => x.WorkDate);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.EditedBy).WithMany().HasForeignKey(x => x.EditedById).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LeaveRequest>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.Property(x => x.ReviewNote).HasMaxLength(500);
            e.HasIndex(x => new { x.UserId, x.FromDate });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Reviewer).WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
            e.Property(x => x.DeviceId).HasMaxLength(100);
            e.HasIndex(x => x.TokenHash);
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue && v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
