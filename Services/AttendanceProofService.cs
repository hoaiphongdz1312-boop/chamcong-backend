using Microsoft.Extensions.Options;
using NguyenHoaiPhong_1681.DTOs;
using NguyenHoaiPhong_1681.Middleware;
using NguyenHoaiPhong_1681.Options;

namespace NguyenHoaiPhong_1681.Services;

public interface IAttendanceProofService
{
    /// <summary>Kiểm tra vị trí giả và bán kính công ty; trả về khoảng cách (mét).</summary>
    int Verify(CheckInOutRequest request);
}

public class AttendanceProofService(IOptions<WorkplaceOptions> options) : IAttendanceProofService
{
    private const double EarthRadiusMeters = 6371000;
    private readonly WorkplaceOptions _options = options.Value;

    public int Verify(CheckInOutRequest request)
    {
        if (request.IsMockLocation)
            throw new AppException(400, "Phát hiện vị trí giả");
        if (_options.Latitude == 0 && _options.Longitude == 0)
            throw new AppException(500, "Chưa cấu hình vị trí công ty");

        var distance = (int)Math.Round(Haversine(request.Latitude!.Value, request.Longitude!.Value));
        if (distance > _options.RadiusMeters)
            throw new AppException(400, $"Bạn đang cách công ty {distance} m, vượt bán kính cho phép {_options.RadiusMeters} m");
        return distance;
    }

    private double Haversine(double lat, double lon)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat - _options.Latitude);
        var dLon = Rad(lon - _options.Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Rad(_options.Latitude)) * Math.Cos(Rad(lat)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
