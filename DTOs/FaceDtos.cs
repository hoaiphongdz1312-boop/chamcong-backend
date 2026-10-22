using System.ComponentModel.DataAnnotations;

namespace NguyenHoaiPhong_1681.DTOs;

public class FaceChallengeRequest
{
    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
    [StringLength(256, ErrorMessage = "Tên đăng nhập quá dài")]
    public string Username { get; set; } = "";

    [StringLength(100, ErrorMessage = "deviceId tối đa 100 ký tự")]
    public string? DeviceId { get; set; }
}

/// <summary>action: "turn_left" hoặc "turn_right".</summary>
public record FaceChallengeResponse(string ChallengeToken, string Action, int ExpiresInSeconds);

public class FaceEnrollForm
{
    [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc")]
    [StringLength(100, ErrorMessage = "Mật khẩu quá dài")]
    public string Password { get; set; } = "";

    public List<IFormFile> Images { get; set; } = [];
}

public record FaceEnrollResponse(int Enrolled);

public record FaceStatusResponse(bool Enrolled, int Count);

public class FaceLoginForm
{
    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
    [StringLength(256, ErrorMessage = "Tên đăng nhập quá dài")]
    public string Username { get; set; } = "";

    /// <summary>Tùy chọn: bỏ trống = đăng nhập đơn giản (1 ảnh, không kiểm quay đầu).</summary>
    [StringLength(200, ErrorMessage = "challengeToken không hợp lệ")]
    public string? ChallengeToken { get; set; }

    [StringLength(100, ErrorMessage = "deviceId tối đa 100 ký tự")]
    public string? DeviceId { get; set; }

    [Required(ErrorMessage = "Thiếu ảnh nhìn thẳng")]
    public IFormFile? ImageStart { get; set; }

    public IFormFile? ImageTurn { get; set; }
}
