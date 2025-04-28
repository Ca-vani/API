using FoodStoreAPI;
using FoodStoreAPI.Data;
using FoodStoreAPI.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Kết nối SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 🔹 Cấu hình Identity
builder.Services.AddIdentity<NguoiDung, VaiTro>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// 🔹 Cấu hình JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("JWT");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["ValidIssuer"],
            ValidAudience = jwtSettings["ValidAudience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"])),
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role  // Cấu hình Claims cho Role
        };
    });


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        policy.WithOrigins("https://localhost:7160") 
              .AllowAnyMethod()   
              .AllowAnyHeader();  
    });
});

// 🔹 Cấu hình Controllers và JSON Serializer
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles; // Hoặc Preserve
    });

// 🔹 Cấu hình Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FoodStore API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập token vào đây (Bearer <token>)"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });

    //  Hỗ trợ upload file trong Swagger
    c.OperationFilter<SwaggerFileOperationFilter>();
    Console.WriteLine("Đã đăng ký SwaggerFileOperationFilter!");
});

var app = builder.Build();

// 🔹 Tạo tài khoản admin mặc định
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<NguoiDung>>();
    var roleManager = services.GetRequiredService<RoleManager<VaiTro>>();

    await SeedAdminUser(userManager, roleManager);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 🔹 Cấu hình Middleware
app.UseCors("AllowSpecificOrigin"); // Áp dụng CORS chính xác

app.UseAuthentication();
app.UseAuthorization();

// Cấu hình Static Files
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
    RequestPath = ""
});

app.MapControllers();

app.Run();

// 🔹 Phương thức Seed Admin User
async Task SeedAdminUser(UserManager<NguoiDung> userManager, RoleManager<VaiTro> roleManager)
{
    string adminRoleId = "Admin";
    string adminEmail = "adminAPI@gmail.com";
    string adminPassword = "Admin@123";

    // 🔹 Tạo vai trò Admin nếu chưa có
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        var adminRole = new VaiTro
        {
            Id = adminRoleId,
            MaVaiTro = adminRoleId,
            Name = "Admin",
            TenVaiTro = "Administrator",
            NormalizedName = "ADMIN"
        };
        await roleManager.CreateAsync(adminRole);
    }

    // 🔹 Tạo tài khoản Admin nếu chưa có
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new NguoiDung
        {
            Id = Guid.NewGuid().ToString(),
            UserName = adminEmail,
            Email = adminEmail,
            NormalizedEmail = adminEmail.ToUpper(),
            EmailConfirmed = true,
            HoTen = "Admin User",
            SDT = "0123456789",
            NgaySinh = new DateTime(1990, 1, 1),
            GioiTinh = "Nam",
            MaVaiTro = adminRoleId,
            TenVaiTro = "Administrator",
            TrangThai = true
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}
