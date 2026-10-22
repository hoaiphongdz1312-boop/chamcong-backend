using NguyenHoaiPhong_1681.Enums;

namespace NguyenHoaiPhong_1681.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    /// <summary>Luôn lưu chữ thường.</summary>
    public string Email { get; set; } = "";
    /// <summary>Dạng 84xxxxxxxxx.</summary>
    public string? PhoneNumber { get; set; }
    public bool PhoneVerified { get; set; }
    public bool EmailVerified { get; set; }
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? AvatarUrl { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public UserStatus Status { get; set; } = UserStatus.PendingApproval;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public int FaceLoginFailedCount { get; set; }
    public DateTime? FaceLockoutUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<OtpCode> OtpCodes { get; set; } = new List<OtpCode>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
