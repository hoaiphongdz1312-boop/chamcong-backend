# NguyenHoaiPhong_1681 – Backend chấm công

ASP.NET Core Web API (.NET 10) cho app chấm công: đăng ký + OTP (Email/SMS), **đăng nhập 2 bước (mật khẩu + OTP)**, JWT + refresh token, **phân quyền động lưu trong SQL Server**, CRUD người dùng / phòng ban / ca làm / chấm công / nghỉ phép, dashboard theo vai trò.

## Yêu cầu
- Visual Studio 2026 (workload ASP.NET and web development), .NET 10 SDK
- SQL Server + SSMS. Chuỗi kết nối ở `appsettings.Development.json` (`Server=.;Database=NguyenHoaiPhong_1681Db;...`; SQL Express thì đổi thành `Server=.\SQLEXPRESS`).
- Khi chạy, app tự `Migrate` database rồi seed dữ liệu.

## Cấu hình bí mật (user-secrets)
```bash
dotnet user-secrets set "Smtp:User" "taikhoan@gmail.com"
dotnet user-secrets set "Smtp:AppPassword" "abcd efgh ijkl mnop"
dotnet user-secrets set "SpeedSms:AccessToken" "<token SpeedSMS>"
dotnet user-secrets set "Sms:Provider" "SpeedSms"      # mặc định "Console": SMS chỉ in ra console
```
- `Jwt:Key` cho Development đã có sẵn trong `appsettings.Development.json`. Môi trường khác: `dotnet user-secrets set "Jwt:Key" "<chuỗi ngẫu nhiên ≥ 32 ký tự>"`.
- `Otp:DevMode = true` → mọi mã OTP được in ra console dạng `[DEV OTP] <đích>: 123456` (vẫn gửi thật).

## Tài khoản seed
| Tài khoản | Mật khẩu | Vai trò | Ghi chú |
|---|---|---|---|
| `admin` | `Admin@1681` | Admin | email hoaiphongdz1312@gmail.com, luôn được tạo |
| `quanly` | `Manager@123` | Manager | quản lý Phòng Kỹ thuật (chỉ Development) |
| `nhanvien1`, `nhanvien2` | `Employee@123` | Employee | Phòng Kỹ thuật (chỉ Development) |

Tài khoản Development dùng email giả `<username>@example.com` → khi đăng nhập kênh Email, lấy mã ở dòng `[DEV OTP]` trong console.
Development còn seed 2 phòng ban (Kỹ thuật, Kinh doanh) và 3 ca: Ca sáng 08:00–12:00, Ca chiều 13:00–17:00, Hành chính 08:00–17:00 (trễ cho phép 10 phút).

## Luồng đăng nhập
1. `POST /api/auth/login` `{ username (hoặc email), password, channel: "Email" | "Sms" }` → gửi OTP, **chưa** cấp token.
   Sai mật khẩu 5 lần → khóa tạm 15 phút (423, có `retryAfterSeconds`).
2. `POST /api/auth/login/verify` `{ username, code, deviceId? }` → `{ accessToken, accessTokenExpiresAt, refreshToken, user, permissions, homeRoute }`.
3. Access token hết hạn (30 phút) → `POST /api/auth/refresh` `{ refreshToken }` (refresh token dùng 1 lần, xoay vòng; dùng lại token cũ → thu hồi mọi phiên).
4. `POST /api/auth/logout` `{ refreshToken }`.

Swagger: bấm **Authorize**, dán `accessToken` (không cần gõ "Bearer").
`/api/auth/*` giới hạn 10 request/phút/IP (`Login:AuthRateLimitPerMinute`).

## Vai trò → trang → quyền
| Vai trò | homeRoute | Quyền mặc định |
|---|---|---|
| Admin | `/admin` | tất cả |
| Manager | `/manager` | departments.view, shifts.view, attendance.self, attendance.view_team, attendance.edit, leave.self, leave.approve_team, dashboard.manager, profile.self |
| Employee | `/employee/dashboard` | shifts.view, attendance.self, leave.self, dashboard.employee, profile.self |

Quyền lưu trong bảng `Roles`/`Permissions`/`RolePermissions`, Admin sửa qua API và có hiệu lực ngay. Mỗi request đọc lại user + vai trò từ DB (khóa/xóa tài khoản là mất quyền ngay, không chờ token hết hạn).
Phạm vi dữ liệu: Manager chỉ thấy/xử lý dữ liệu của phòng mình **quản lý**; Employee chỉ dữ liệu của mình.

## Danh sách API
| Method | Path | Quyền |
|---|---|---|
| POST | `/api/auth/register`, `/register/resend`, `/register/verify` | công khai |
| POST | `/api/auth/login`, `/login/resend`, `/login/verify`, `/refresh` | công khai |
| POST | `/api/auth/forgot-password`, `/reset-password` | công khai |
| POST | `/api/auth/logout` | đăng nhập |
| GET | `/api/auth/me` | đăng nhập |
| GET, PUT | `/api/me` | profile.self |
| POST | `/api/me/change-password` | profile.self |
| GET | `/api/admin/users`, `/pending`, `/{id}` | users.view |
| POST, PUT, DELETE | `/api/admin/users`, `/{id}` | users.manage |
| PATCH | `/api/admin/users/{id}/approve`, `/lock`, `/unlock`, `/role`, `/reset-password` | users.manage |
| GET | `/api/admin/permissions`, `/api/admin/roles` | roles.manage |
| POST, PUT, DELETE | `/api/admin/roles`, `/{id}` | roles.manage |
| PUT | `/api/admin/roles/{id}/permissions` | roles.manage |
| GET | `/api/departments`, `/{id}` | departments.view |
| POST, PUT, DELETE, PATCH | `/api/departments`, `/{id}`, `/{id}/manager` | departments.manage |
| GET | `/api/shifts`, `/{id}` | shifts.view |
| POST, PUT, DELETE | `/api/shifts`, `/{id}` | shifts.manage |
| POST | `/api/attendance/check-in`, `/check-out` | attendance.self |
| GET | `/api/attendance/my` | attendance.self |
| GET | `/api/attendance`, `/{id}` | attendance.view_team / attendance.view_all |
| GET | `/api/reports/attendance-monthly?month=yyyy-MM&departmentId=`, `/export` (CSV) | attendance.view_team / attendance.view_all |
| PUT | `/api/attendance/{id}` | attendance.edit |
| DELETE | `/api/attendance/{id}` | attendance.delete |
| POST, GET | `/api/leave-requests`, `/my` | leave.self |
| PUT, POST | `/api/leave-requests/{id}`, `/{id}/cancel` | leave.self (đơn của mình, còn Pending) |
| GET, POST | `/api/leave-requests`, `/{id}/approve`, `/{id}/reject` | leave.approve_team / leave.approve_all |
| GET | `/api/dashboard/admin` · `/manager` · `/employee` | dashboard.* tương ứng |
| GET | `/api/dev/smtp-test?to=`, `/api/dev/sms-test?phone=` | chỉ Development |

Danh sách trả `{ items, page, pageSize, totalCount }` (`page` mặc định 1, `pageSize` mặc định 20, tối đa 100). Lỗi trả ProblemDetails có field `message` tiếng Việt. Thời gian trả về là UTC (hậu tố `Z`); ngày làm việc (`workDate`) tính theo giờ Việt Nam.

## Chạy & test
1. Mở `NguyenHoaiPhong_1681.slnx`, profile **http** → F5 (Swagger: `http://localhost:5074/swagger`). Profile **lan** lắng nghe `0.0.0.0:5074` cho điện thoại/emulator.
2. Chạy lần lượt các request trong `NguyenHoaiPhong_1681.http` (điền mã OTP từ console vào biến `@adminOtp`, `@nv1Otp`, `@qlOtp`).
