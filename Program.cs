using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NguyenHoaiPhong_1681.Authorization;
using NguyenHoaiPhong_1681.Data;
using NguyenHoaiPhong_1681.Helpers;
using NguyenHoaiPhong_1681.Middleware;
using NguyenHoaiPhong_1681.Options;
using NguyenHoaiPhong_1681.Services;

var builder = WebApplication.CreateBuilder(args);
var isDev = builder.Environment.IsDevelopment();

// Options
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection(OtpOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection(SmsOptions.SectionName));
builder.Services.Configure<SpeedSmsOptions>(builder.Configuration.GetSection(SpeedSmsOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<LoginOptions>(builder.Configuration.GetSection(LoginOptions.SectionName));

builder.Services.Configure<FaceOptions>(builder.Configuration.GetSection(FaceOptions.SectionName));
builder.Services.Configure<WorkplaceOptions>(builder.Configuration.GetSection(WorkplaceOptions.SectionName));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrEmpty(jwt.Key) || jwt.Key.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key chưa cấu hình hoặc ngắn hơn 32 ký tự. Không ghi key vào appsettings; chạy trong thư mục backend: dotnet user-secrets set \"Jwt:Key\" \"<chuỗi ngẫu nhiên ≥ 64 ký tự>\"");
}
var loginOptions = builder.Configuration.GetSection(LoginOptions.SectionName).Get<LoginOptions>() ?? new LoginOptions();

// Database
// Transaction được rollback chủ động trong code nên tắt cảnh báo savepoint của MARS;
// User có soft-delete filter, các bảng phụ thuộc chấp nhận bị ẩn theo user đã xóa
builder.Services.AddDbContext<AppDbContext>(o => o
    .UseSqlServer(builder.Configuration.GetConnectionString("Default"))
    .ConfigureWarnings(w => w
        .Ignore(SqlServerEventId.SavepointsDisabledBecauseOfMARS)
        .Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

// Services
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<UnverifiedUserCleanupService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddScoped<IRoleAdminService, RoleAdminService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ILeaveRequestService, LeaveRequestService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddSingleton<IFaceService, FaceService>();
builder.Services.AddSingleton<IAttendanceProofService, AttendanceProofService>();
builder.Services.AddScoped<IFaceLoginService, FaceLoginService>();

var smsProvider = builder.Configuration.GetSection(SmsOptions.SectionName).Get<SmsOptions>()?.Provider ?? "Console";
if (string.Equals(smsProvider, "SpeedSms", StringComparison.OrdinalIgnoreCase))
{
    var timeout = builder.Configuration.GetSection(SpeedSmsOptions.SectionName).Get<SpeedSmsOptions>()?.TimeoutSeconds ?? 15;
    builder.Services.AddHttpClient<ISmsSender, SpeedSmsSender>(c => c.Timeout = TimeSpan.FromSeconds(timeout));
}
else if (string.Equals(smsProvider, "Console", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ISmsSender, ConsoleSmsSender>();
}
else
{
    throw new InvalidOperationException($"Sms:Provider \"{smsProvider}\" không hợp lệ. Chỉ nhận \"Console\" hoặc \"SpeedSms\".");
}

// Xác thực JWT + phân quyền động theo DB
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "unique_name",
            RoleClaimType = ClaimTypes.Role
        };
        o.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await ExceptionMiddleware.WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                    "Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn");
            },
            OnForbidden = context => ExceptionMiddleware.WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                CurrentUserExtensions.ForbiddenMessage)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

// Giới hạn tần suất: "auth" chỉ gắn cho register*/login*/forgot/reset (AuthController),
// mọi request còn lại (kể cả /auth/me, /auth/refresh, /auth/logout) chỉ dính giới hạn chung 120 req/phút/IP
builder.Services.AddRateLimiter(o =>
{
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginOptions.DefaultRateLimitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    o.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginOptions.AuthRateLimitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    o.OnRejected = (context, _) => new ValueTask(ExceptionMiddleware.WriteProblemAsync(context.HttpContext,
        StatusCodes.Status429TooManyRequests, "Bạn thao tác quá nhanh, vui lòng thử lại sau ít phút"));
});

// Controllers + lỗi validate model trả 400 kèm lỗi từng trường
builder.Services.AddControllers()
    .ConfigureApplicationPartManager(apm =>
    {
        if (isDev) return;
        var defaults = apm.FeatureProviders.OfType<ControllerFeatureProvider>().ToList();
        foreach (var p in defaults) apm.FeatureProviders.Remove(p);
        apm.FeatureProviders.Add(new ExcludeDevControllerFeatureProvider());
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        o.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu không hợp lệ",
                Detail = "Dữ liệu không hợp lệ, vui lòng kiểm tra lại",
                Instance = context.HttpContext.Request.Path
            };
            problem.Extensions["message"] = "Dữ liệu không hợp lệ, vui lòng kiểm tra lại";
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        };
    });

if (isDev)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "NguyenHoaiPhong_1681 – API chấm công", Version = "v1" });
        var xml = Path.Combine(AppContext.BaseDirectory, "NguyenHoaiPhong_1681.xml");
        if (File.Exists(xml)) c.IncludeXmlComments(xml);

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Dán accessToken nhận từ /api/auth/login/verify (không cần gõ chữ Bearer)"
        });
        c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    });
    builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, app.Environment, app.Logger);
    // Nạp sẵn model ONNX nhận diện khuôn mặt để lần quét mặt đầu tiên không bị chậm
    scope.ServiceProvider.GetRequiredService<IFaceService>();
}

if (isDev)
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("AllowAll");
}
else
{
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
