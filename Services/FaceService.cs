using FaceAiSharp;
using FaceAiSharp.Extensions;
using Microsoft.Extensions.Options;
using NguyenHoaiPhong_1681.Middleware;
using NguyenHoaiPhong_1681.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NguyenHoaiPhong_1681.Services;

/// <summary>Singleton: nạp model ONNX một lần, suy luận tuần tự (khóa) để tránh chia sẻ buffer giữa các request.</summary>
public class FaceService : IFaceService
{
    private readonly IFaceDetectorWithLandmarks _detector = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
    private readonly IFaceEmbeddingsGenerator _embedder = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();
    private readonly object _gate = new();
    private readonly FaceOptions _opt;

    public FaceService(IOptions<FaceOptions> options) => _opt = options.Value;

    public FaceInfo ExtractFace(byte[] imageBytes)
    {
        Image<Rgb24> image;
        try
        {
            image = Image.Load<Rgb24>(imageBytes);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new AppException(400, "Ảnh không hợp lệ, chỉ nhận JPEG hoặc PNG");
        }

        using (image)
        {
            image.Mutate(x => x.AutoOrient());

            lock (_gate)
            {
                var faces = _detector.DetectFaces(image);
                if (faces.Count == 0) throw new AppException(400, "Không thấy khuôn mặt");
                if (faces.Count > 1) throw new AppException(400, "Có nhiều hơn 1 khuôn mặt");

                var face = faces.First();
                if (Math.Min(face.Box.Width, face.Box.Height) < _opt.MinFaceSizePx)
                    throw new AppException(400, "Khuôn mặt quá nhỏ, hãy lại gần hơn");
                if (face.Landmarks is not { Count: >= 3 } landmarks)
                    throw new AppException(400, "Không thấy khuôn mặt");

                var yaw = YawRatio(landmarks);
                _embedder.AlignFaceUsingLandmarks(image, landmarks);
                return new FaceInfo(Normalize(_embedder.GenerateEmbedding(image)), yaw);
            }
        }
    }

    public float Cosine(float[] a, float[] b) => a.Dot(b);

    /// <summary>Landmark 5 điểm: [0] mắt trái ảnh, [1] mắt phải ảnh, [2] mũi, [3..4] khóe miệng.</summary>
    private static float YawRatio(IReadOnlyList<PointF> landmarks)
    {
        var eyeDistance = landmarks[1].X - landmarks[0].X;
        if (Math.Abs(eyeDistance) < 1e-3f) return 0f;
        return (landmarks[2].X - (landmarks[0].X + landmarks[1].X) / 2f) / eyeDistance;
    }

    private static float[] Normalize(float[] v)
    {
        var norm = MathF.Sqrt(v.Sum(x => x * x));
        return norm < 1e-6f ? v : v.Select(x => x / norm).ToArray();
    }
}
