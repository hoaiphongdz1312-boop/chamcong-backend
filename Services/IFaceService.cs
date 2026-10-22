namespace NguyenHoaiPhong_1681.Services;

/// <summary>Kết quả phân tích 1 ảnh: embedding đã chuẩn hóa L2 và độ quay đầu (yaw tỉ lệ).</summary>
public record FaceInfo(float[] Embedding, float Yaw);

public interface IFaceService
{
    /// <summary>Giải mã ảnh, bắt buộc đúng 1 mặt đủ lớn. Lỗi → AppException 400 tiếng Việt.</summary>
    FaceInfo ExtractFace(byte[] imageBytes);

    float Cosine(float[] a, float[] b);
}
