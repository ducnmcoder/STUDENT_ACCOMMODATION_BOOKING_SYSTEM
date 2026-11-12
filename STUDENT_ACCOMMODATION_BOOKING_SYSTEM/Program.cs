using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// Cấu hình kết nối SQL Server qua Entity Framework Core
builder.Services.AddDbContext<AccommodationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AccommodationDb")));

// Đăng ký các Repository
builder.Services.AddScoped<AccountRepository>();
builder.Services.AddScoped<IFacilitiesAssetRepository, FacilitiesAssetRepository>();
builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AccommodationDb")));
builder.Services.AddScoped<StudentBookingService>();
builder.Services.AddScoped<GuardBookingRepository>();
builder.Services.AddScoped<GuardInspectionRepository>();
builder.Services.AddScoped<ReputationRepository>();
builder.Services.AddScoped<NoShowService>();
builder.Services.AddHostedService<NoShowBackgroundService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.Cookie.Name = "SABS.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

var initializePasswords = args.Contains("--init-demo-passwords", StringComparer.OrdinalIgnoreCase);
var resetPasswords = args.Contains("--reset-demo-passwords", StringComparer.OrdinalIgnoreCase);
if (initializePasswords || resetPasswords)
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Chỉ khởi tạo mật khẩu mẫu trong môi trường Development.");
    var password = Environment.GetEnvironmentVariable("SABS_DEMO_PASSWORD");
    if (string.IsNullOrWhiteSpace(password) || (initializePasswords && password.Length < 12))
        throw new InvalidOperationException("Đặt SABS_DEMO_PASSWORD dài ít nhất 12 ký tự trước khi chạy.");
    using var scope = app.Services.CreateScope();
    var count = await scope.ServiceProvider.GetRequiredService<AccountRepository>()
        .SetDemoPasswordsAsync(password, resetPasswords);
    Console.WriteLine($"Đã {(resetPasswords ? "đặt lại" : "khởi tạo")} mật khẩu cho {count} tài khoản mẫu.");
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
