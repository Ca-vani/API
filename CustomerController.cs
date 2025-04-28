using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using FoodStoreAPI.Data;
using FoodStoreAPI.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using FoodStoreAPI.DTO;

namespace FoodStoreAPI.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Customer")]
    [Route("api/customer")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CustomerController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<string> GetMaNguoiDungAsync()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return null;

            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.MaNguoiDung)
                .FirstOrDefaultAsync();

            return user;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [Authorize(Roles = "Customer")]
        [HttpGet("cart")]
        public async Task<IActionResult> GetCart()
        {
            var maNguoiDung = await GetMaNguoiDungAsync();
            if (maNguoiDung == null) return Unauthorized("Người dùng không tồn tại.");

            var cartItems = await _context.ChiTietGioHangs
                .Where(ct => ct.GioHang.MaNguoiDung == maNguoiDung)
                .Select(ct => new
                {
                    ct.MaCTGH,
                    ct.MaMon,
                    TenMon = ct.MonAn.TenMon,
                    ct.SoLuong,
                    ct.DonGia,
                    ThanhTien = ct.SoLuong * ct.DonGia
                })
                .ToListAsync();

            if (!cartItems.Any()) return Ok(new { Message = "Giỏ hàng trống" });

            return Ok(cartItems);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [Authorize(Roles = "Customer")]
        [HttpPost("cart/{maMon}")]
        public async Task<IActionResult> AddToCart(string maMon)
        {
            var maNguoiDung = await GetMaNguoiDungAsync();
            if (maNguoiDung == null) return Unauthorized("Người dùng không tồn tại.");

            var monAn = await _context.MonAns.FindAsync(maMon);
            if (monAn == null) return NotFound("Món ăn không tồn tại.");

            var cart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                .FirstOrDefaultAsync(g => g.MaNguoiDung == maNguoiDung);

            if (cart == null)
            {
                cart = new GioHang
                {
                    MaGH = Guid.NewGuid().ToString(),
                    MaNguoiDung = maNguoiDung,
                    ChiTietGioHangs = new List<ChiTietGioHang>()
                };
                _context.GioHangs.Add(cart);
                await _context.SaveChangesAsync();
            }

            var cartItem = cart.ChiTietGioHangs.FirstOrDefault(ct => ct.MaMon == maMon);
            if (cartItem != null)
            {
                cartItem.SoLuong++;
            }
            else
            {
                cart.ChiTietGioHangs.Add(new ChiTietGioHang
                {
                    MaCTGH = Guid.NewGuid().ToString(),
                    MaGH = cart.MaGH,
                    MaMon = maMon,
                    DonGia = monAn.GiaMon,
                    SoLuong = 1
                });
            }

            await _context.SaveChangesAsync();
            return Ok("Đã thêm món vào giỏ hàng.");
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [Authorize(Roles = "Customer")]
        [HttpPut("cart/increase/{maCTGH}")]
        public async Task<IActionResult> IncreaseQuantity(string maCTGH)
        {
            Console.WriteLine("ccccccccc");
            var maNguoiDung = await GetMaNguoiDungAsync();
            if (maNguoiDung == null) return Unauthorized("Người dùng không tồn tại.");

            var cartItem = await _context.ChiTietGioHangs
                .Include(ct => ct.GioHang)
                .FirstOrDefaultAsync(ct => ct.MaCTGH == maCTGH && ct.GioHang.MaNguoiDung == maNguoiDung);

            if (cartItem == null) return NotFound("Chi tiết giỏ hàng không tồn tại.");

            cartItem.SoLuong++;
            await _context.SaveChangesAsync();
            return Ok("Đã tăng số lượng món.");
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [Authorize(Roles = "Customer")]
        [HttpPut("cart/decrease/{maCTGH}")]
        public async Task<IActionResult> DecreaseQuantity(string maCTGH)
        {
            var maNguoiDung = await GetMaNguoiDungAsync();
            if (maNguoiDung == null) return Unauthorized("Người dùng không tồn tại.");

            var cartItem = await _context.ChiTietGioHangs
                .Include(ct => ct.GioHang)
                .FirstOrDefaultAsync(ct => ct.MaCTGH == maCTGH && ct.GioHang.MaNguoiDung == maNguoiDung);

            if (cartItem == null) return NotFound("Chi tiết giỏ hàng không tồn tại.");

            if (cartItem.SoLuong > 1)
            {
                cartItem.SoLuong--;
            }
            else
            {
                _context.ChiTietGioHangs.Remove(cartItem);
            }

            await _context.SaveChangesAsync();
            return Ok("Đã giảm số lượng món.");
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [Authorize(Roles = "Customer")]
        [HttpDelete("cart/{maCTGH}")]
        public async Task<IActionResult> RemoveFromCart(string maCTGH)
        {
            var maNguoiDung = await GetMaNguoiDungAsync();
            if (maNguoiDung == null) return Unauthorized("Người dùng không tồn tại.");

            var cartItem = await _context.ChiTietGioHangs
                .Include(ct => ct.GioHang)
                .FirstOrDefaultAsync(ct => ct.MaCTGH == maCTGH && ct.GioHang.MaNguoiDung == maNguoiDung);

            if (cartItem == null) return NotFound("Chi tiết giỏ hàng không tồn tại.");

            _context.ChiTietGioHangs.Remove(cartItem);
            await _context.SaveChangesAsync();
            return Ok("Đã xóa món khỏi giỏ hàng.");
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [Authorize(Roles = "Customer")]
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout()
        {
            var maNguoiDung = await GetMaNguoiDungAsync();
            if (maNguoiDung == null) return Unauthorized("Người dùng không tồn tại.");

            var cart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                .ThenInclude(ct => ct.MonAn)
                .FirstOrDefaultAsync(g => g.MaNguoiDung == maNguoiDung);

            if (cart == null || !cart.ChiTietGioHangs.Any())
                return BadRequest("Giỏ hàng trống.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var maHD = Guid.NewGuid().ToString();
                var ngayTaoHD = DateTime.UtcNow;

                decimal tongTien = cart.ChiTietGioHangs.Sum(ct => ct.SoLuong * ct.DonGia);

                var hoaDon = new HoaDon
                {
                    MaHD = maHD,
                    MaNguoiDung = maNguoiDung,
                    TrangThai = "Đã Thanh Toán",
                    NgayTaoHD = ngayTaoHD,
                    TongTien = tongTien
                };

                _context.HoaDons.Add(hoaDon);
                await _context.SaveChangesAsync(); // Lưu hóa đơn trước

                var chiTietHoaDons = cart.ChiTietGioHangs.Select(ct => new ChiTietHoaDon
                {
                    MaCTHD = Guid.NewGuid().ToString(),
                    MaHD = maHD,
                    MaMon = ct.MaMon,
                    TenMon = ct.MonAn.TenMon, // Thêm tên món ăn
                    SoLuong = ct.SoLuong,
                    DonGia = ct.DonGia
                }).ToList();


                _context.ChiTietHoaDons.AddRange(chiTietHoaDons);
                _context.ChiTietGioHangs.RemoveRange(cart.ChiTietGioHangs);

                _context.LichSuDatHangs.Add(new LichSuDatHang
                {
                    MaNguoiDung = maNguoiDung,
                    MaHD = maHD,
                    NgayHoanThanh = ngayTaoHD,
                    TongTien = tongTien
                });

                await _context.SaveChangesAsync(); 
                await transaction.CommitAsync(); 

                return Ok(new
                {
                    Message = "Đặt hàng thành công!",
                    HoaDon = hoaDon,
                    ChiTietHoaDon = chiTietHoaDons,
                    TongTien = tongTien
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Lỗi khi đặt hàng, vui lòng thử lại.");
            }
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [Authorize(Roles = "Customer")]
        // MÓN ĂN
        [HttpGet("get-mon")]
        public async Task<IActionResult> GetMon()
        {
            var monAns = await _context.MonAns
                .Include(m => m.LoaiMon)
                .Select(m => new MonAnGetDTO
                {
                    MaMon = m.MaMon,
                    TenMon = m.TenMon,
                    GiaMon = m.GiaMon,
                    AnhMon = m.AnhMon,
                    TenLoai = m.LoaiMon.TenLoai
                })
                .ToListAsync();

            return Ok(monAns);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [Authorize(Roles = "Customer")]
        [HttpGet("get-mon/{id}")]
        public async Task<IActionResult> GetMon(string id)
        {
            var monAn = await _context.MonAns
                .Include(m => m.LoaiMon)
                .Where(m => m.MaMon == id) 
                .Select(m => new
                {
                    m.MaMon,
                    m.TenMon,
                    m.GiaMon,
                    m.AnhMon,
                    m.TrangThai,
                    LoaiMon = new
                    {
                        m.LoaiMon.MaLoai,
                        m.LoaiMon.TenLoai,
                        m.LoaiMon.MoTa
                    }
                })
                .FirstOrDefaultAsync();

            if (monAn == null)
            {
                return NotFound("Không tìm thấy món ăn.");
            }
            Console.WriteLine(monAn);
            return Ok(monAn);
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [Authorize(Roles = "Customer")]
        [HttpGet("order/{maHD}")]
        public async Task<IActionResult> GetOrderById(string maHD)
        {
            var maNguoiDung = await GetMaNguoiDungAsync();
            if (maNguoiDung == null) return Unauthorized("Người dùng không tồn tại.");

            var hoaDon = await _context.HoaDons
                .Where(hd => hd.MaHD == maHD && hd.MaNguoiDung == maNguoiDung)
                .Include(hd => hd.ChiTietHoaDons)
                .ThenInclude(cthd => cthd.MonAn)
                .FirstOrDefaultAsync();

            if (hoaDon == null) return NotFound("Hóa đơn không tồn tại.");

            var hoaDonDTO = new
            {
                hoaDon.MaHD,
                hoaDon.NgayTaoHD,
                hoaDon.TongTien,
                hoaDon.TrangThai,
                ChiTietHoaDon = hoaDon.ChiTietHoaDons.Select(ct => new
                {
                    ct.MaCTHD,
                    ct.TenMon,
                    ct.SoLuong,
                    ct.DonGia,
                    ThanhTien = ct.SoLuong * ct.DonGia
                }).ToList()
            };

            return Ok(hoaDonDTO);
        }



    }
}
