using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using FoodStoreAPI.ApiModels;
using FoodStoreAPI.DTO;
using FoodStoreAPI.Models;
using Microsoft.Extensions.Configuration;
using FoodStoreAPI.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

[Route("api/account")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly UserManager<NguoiDung> _userManager;
    private readonly SignInManager<NguoiDung> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public AccountController(
        UserManager<NguoiDung> userManager,
        SignInManager<NguoiDung> signInManager,
        IConfiguration configuration,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _context = context;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { Message = "Dữ liệu không hợp lệ", Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
            return BadRequest(new { Message = "Email đã được sử dụng" });

        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.MatKhau);
        var user = new NguoiDung
        {
            MaNguoiDung = Guid.NewGuid().ToString(),
            UserName = model.Email,
            MatKhau = hashedPassword,
            Email = model.Email,
            HoTen = model.HoTen,
            SDT = model.SDT,
            NgaySinh = model.NgaySinh,
            GioiTinh = model.GioiTinh,
            MaVaiTro = "Customer",
            TenVaiTro = "Customer",
            TrangThai = true
        };

        var result = await _userManager.CreateAsync(user, model.MatKhau);
        if (!result.Succeeded)
        {
            return BadRequest(new { Message = "Đăng ký thất bại", Errors = result.Errors.Select(e => e.Description) });
        }

        var gioHang = new GioHang
        {
            MaGH = Guid.NewGuid().ToString(),
            MaNguoiDung = user.MaNguoiDung
        };

        _context.GioHangs.Add(gioHang);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Đăng ký thành công", MaNguoiDung = user.MaNguoiDung, MaGH = gioHang.MaGH });
    }


    // Đăng nhập
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { Message = "Dữ liệu không hợp lệ", Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return Unauthorized(new { Message = "Email không tồn tại" });

        if (!await _userManager.CheckPasswordAsync(user, model.MatKhau))
            return Unauthorized(new { Message = "Sai mật khẩu" });

        if (!user.TrangThai)
            return Unauthorized(new { Message = "Tài khoản đã bị khóa" });

        var authClaims = new[]
        {
        new Claim(JwtRegisteredClaimNames.Sub, user.MaNguoiDung),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(ClaimTypes.Name, user.HoTen),
        new Claim(ClaimTypes.Role, user.TenVaiTro),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

        var token = new JwtSecurityToken(
            issuer: _configuration["JWT:ValidIssuer"],
            audience: _configuration["JWT:ValidAudience"],
            expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["JWT:ExpireMinutes"])),
            claims: authClaims,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
        );

        return Ok(new
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Expiration = token.ValidTo,
            User = new
            {
                user.MaNguoiDung,
                user.Email,
                user.HoTen,
                user.TenVaiTro
            }
        });
    }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Administrator")]
    [HttpGet("tong-doanh-thu")]
    public async Task<IActionResult> GetTotalRevenue()
    {
        var totalRevenue = await _context.ChiTietHoaDons
            .Where(ct => ct.HoaDon.TrangThai == "Đã Thanh Toán")
            .SumAsync(ct => (decimal?)ct.SoLuong * ct.DonGia) ?? 0;

        return Ok(new { TongDoanhThu = $"{totalRevenue:N0} VNĐ" });
    }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Administrator")]
    [HttpGet("tong-doanh-thu-theo-mon")]
    public async Task<IActionResult> GetTotalRevenueByDish()
    {
        var result = await _context.ChiTietHoaDons
            .Where(ct => ct.HoaDon.TrangThai == "Đã Thanh Toán")
            .GroupBy(ct => ct.MonAn.TenMon)
            .Select(g => new
            {
                TenMon = g.Key,
                TongDoanhThu = g.Sum(ct => ct.SoLuong * ct.DonGia)
            })
            .OrderByDescending(g => g.TongDoanhThu)
            .ToListAsync();

        return Ok(result.Select(r => new
        {
            r.TenMon,
            TongDoanhThu = $"{r.TongDoanhThu:N0} VNĐ"
        }));
    }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Administrator")]
    [HttpGet("doanh-thu-cao-nhat")]
    public async Task<IActionResult> GetHighestRevenueDay()
    {
        var result = await _context.ChiTietHoaDons
            .Where(ct => ct.HoaDon.TrangThai == "Đã Thanh Toán")
            .GroupBy(ct => new { ct.HoaDon.NgayTaoHD.Date, ct.MonAn.TenMon })
            .Select(g => new
            {
                Ngay = g.Key.Date,
                TenMon = g.Key.TenMon,
                TongDoanhThu = g.Sum(ct => ct.SoLuong * ct.DonGia)
            })
            .OrderByDescending(g => g.TongDoanhThu)
            .FirstOrDefaultAsync();

        if (result == null)
            return Ok(new { Message = "Không có dữ liệu.", DoanhThu = "0 VNĐ" });

        return Ok(new
        {
            tenMon = result.TenMon, 
            ngay = result.Ngay.ToString("dd-MM-yyyy"),
            tongDoanhThu = $"{result.TongDoanhThu:N0} VND"
        });
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Administrator")]
    [HttpGet("doanh-thu-thap-nhat")]
    public async Task<IActionResult> GetLowestRevenueDay()
    {
        var result = await _context.ChiTietHoaDons
            .Where(ct => ct.HoaDon.TrangThai == "Đã Thanh Toán")
            .GroupBy(ct => new { ct.HoaDon.NgayTaoHD.Date, ct.MonAn.TenMon })
            .Select(g => new
            {
                Ngay = g.Key.Date,
                TenMon = g.Key.TenMon,
                TongDoanhThu = g.Sum(ct => ct.SoLuong * ct.DonGia)
            })
            .OrderBy(g => g.TongDoanhThu)
            .FirstOrDefaultAsync();

        if (result == null)
            return Ok(new { Message = "Không có dữ liệu.", DoanhThu = "0 VNĐ" });

        return Ok(new
        {
            result.TenMon,
            Ngay = result.Ngay.ToString("dd-MM-yyyy"),
            TongDoanhThu = $"{result.TongDoanhThu:N0} VNĐ"
        });
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Administrator")]
    [HttpPost("register-staff")]
    public async Task<IActionResult> RegisterStaff([FromBody] RegisterModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { Message = "Dữ liệu không hợp lệ", Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
            return BadRequest(new { Message = "Email đã được sử dụng" });

        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.MatKhau);
        var user = new NguoiDung
        {
            MaNguoiDung = Guid.NewGuid().ToString(),
            UserName = model.Email,
            MatKhau = hashedPassword,
            Email = model.Email,
            HoTen = model.HoTen,
            SDT = model.SDT,
            NgaySinh = model.NgaySinh,
            GioiTinh = model.GioiTinh,
            MaVaiTro = "Staff",
            TenVaiTro = "Staff",
            TrangThai = true
        };

        var result = await _userManager.CreateAsync(user, model.MatKhau);
        if (!result.Succeeded)
        {
            return BadRequest(new { Message = "Đăng ký thất bại", Errors = result.Errors.Select(e => e.Description) });
        }
        return Ok(new { Message = "Tạo tài khoản nhân viên thành công", MaNguoiDung = user.MaNguoiDung});
    }

    private async Task<List<NguoiDungDTO>> GetNguoiDungDTOs(IQueryable<NguoiDung> query)
    {
        return await query
            .Where(u => u.MaVaiTro != "Admin") // Loại bỏ Admin
            .Select(u => new NguoiDungDTO
            {
                MaNguoiDung = u.MaNguoiDung,
                HoTen = u.HoTen,
                Email = u.Email,
                NgaySinh = u.NgaySinh,
                MaVaiTro = u.MaVaiTro,
                TenVaiTro = u.TenVaiTro,
                SDT = u.SDT,
                GioiTinh = u.GioiTinh,
                TrangThai = u.TrangThai
            }).ToListAsync();
    }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Administrator")]
    [HttpGet("TatCaNhanVien")]
    public async Task<IActionResult> GetTatCaNhanVien()
    {
        var nhanViens = await GetNguoiDungDTOs(_userManager.Users);
        return Ok(nhanViens);
    }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Administrator")]
    [HttpGet("TatCaKhachHang")]
    public async Task<IActionResult> GetTatCaKhachHang()
    {
        var khachHangs = await GetNguoiDungDTOs(_userManager.Users.Where(u => u.MaVaiTro == "Customer"));
        return Ok(khachHangs);
    }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Administrator")]
    [HttpGet("TatCaNhanVienBanHang")]
    public async Task<IActionResult> GetTatCaNhanVienBanHang()
    {
        var nhanVienBanHangs = await GetNguoiDungDTOs(_userManager.Users.Where(u => u.MaVaiTro == "Staff"));
        return Ok(nhanVienBanHangs);
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Administrator")]
    [HttpPut("CapNhatTrangThai")]
    public async Task<IActionResult> CapNhatTrangThai([FromBody] CapNhatTrangThaiDTO model)
    {
        var nguoiDung = await _userManager.FindByIdAsync(model.MaNguoiDung);
        if (nguoiDung == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng." });
        }

        nguoiDung.TrangThai = !nguoiDung.TrangThai;
        var result = await _userManager.UpdateAsync(nguoiDung);

        if (result.Succeeded)
        {
            return Ok(new { message = "Cập nhật trạng thái thành công." });
        }
        return BadRequest(new { message = "Cập nhật trạng thái thất bại." });
    }
}
