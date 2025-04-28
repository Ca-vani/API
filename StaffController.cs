using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using FoodStoreAPI.Models;
using FoodStoreAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FoodStoreAPI.DTO;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace FoodStoreAPI.Controllers
{
    //LOẠI MÓN
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize(Roles = "Staff")]
    [Route("api/staff")]
    [ApiController]
    public class StaffController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StaffController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("them-loai")]
        public async Task<IActionResult> ThemLoai([FromBody] LoaiMon loaiMon)
        {
            if (loaiMon == null || string.IsNullOrEmpty(loaiMon.MaLoai) || string.IsNullOrEmpty(loaiMon.TenLoai))
            {
                return BadRequest("Mã loại và tên loại món không được để trống.");
            }

            if (await _context.LoaiMons.AnyAsync(l => l.MaLoai == loaiMon.MaLoai))
            {
                return BadRequest("Mã loại món đã tồn tại.");
            }

            loaiMon.TrangThai = true;
            _context.LoaiMons.Add(loaiMon);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLoai), new { id = loaiMon.MaLoai }, loaiMon);
        }

        [HttpPut("update-loai/{id}")]
        public async Task<IActionResult> UpdateLoai(string id, [FromBody] LoaiMon loaiMon)
        {
            var loaiMonExist = await _context.LoaiMons.FindAsync(id);
            if (loaiMonExist == null)
            {
                return NotFound("Loại món không tồn tại.");
            }

            if (string.IsNullOrEmpty(loaiMon.TenLoai))
            {
                return BadRequest("Tên loại món không được để trống.");
            }

            loaiMonExist.TenLoai = loaiMon.TenLoai;
            loaiMonExist.MoTa = loaiMon.MoTa;

            _context.LoaiMons.Update(loaiMonExist);
            await _context.SaveChangesAsync();

            return Ok(loaiMonExist);
        }

        [HttpGet("get-loai")]
        public async Task<IActionResult> GetLoai()
        {
            var loaiMons = await _context.LoaiMons.ToListAsync();
            if (!loaiMons.Any())
            {
                return NotFound("Không có loại món nào.");
            }
            return Ok(loaiMons);
        }
        [HttpGet("get-loai/{id}")]
        public async Task<IActionResult> GetLoaiById(string id)
        {
            var loaiMon = await _context.LoaiMons.FindAsync(id);
            if (loaiMon == null)
            {
                return NotFound("Loại món không tồn tại.");
            }

            return Ok(loaiMon);
        }


        [HttpGet("get-mon-theo-loai/{maLoai}")]
        public async Task<IActionResult> GetMonTheoLoai(string maLoai)
        {
            var monAnList = await _context.MonAns
                .Where(m => m.MaLoai == maLoai)
                .ToListAsync();

            if (!monAnList.Any())
            {
                return NotFound("Không có món nào thuộc loại này.");
            }

            return Ok(monAnList);
        }

        [HttpPut("toggle-loai/{id}")]
        public async Task<IActionResult> ToggleLoaiMon(string id)
        {
            var loaiMon = await _context.LoaiMons.FindAsync(id);
            if (loaiMon == null)
            {
                return NotFound("Loại món không tồn tại.");
            }

            loaiMon.TrangThai = !loaiMon.TrangThai;
            _context.LoaiMons.Update(loaiMon);
            await _context.SaveChangesAsync();

            string status = loaiMon.TrangThai ? "được kích hoạt" : "bị vô hiệu hóa";
            return Ok($"Loại món '{loaiMon.TenLoai}' đã {status}.");
        }

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
        [HttpGet("get-mon/{id}")]
        public async Task<IActionResult> GetMon(string id)
        {
            var monAn = await _context.MonAns
                .Include(m => m.LoaiMon)
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
                .FirstOrDefaultAsync(m => m.MaMon == id);

            if (monAn == null)
            {
                return NotFound("Không tìm thấy món ăn.");
            }

            return Ok(monAn);
        }

        [HttpPost("them-mon")]
        public async Task<IActionResult> ThemMon([FromBody] MonAn monAn)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(monAn.TenMon) || monAn.GiaMon <= 0 || string.IsNullOrWhiteSpace(monAn.MaLoai))
            {
                return BadRequest("Tên món, giá món và mã loại không được để trống hoặc không hợp lệ.");
            }

            var loaiMon = await _context.LoaiMons.FindAsync(monAn.MaLoai);
            if (loaiMon == null)
            {
                return BadRequest("Mã loại món không tồn tại.");
            }


            monAn.TrangThai = true;

            await _context.MonAns.AddAsync(monAn);
            await _context.SaveChangesAsync();

            return Ok(monAn);
        }

        [HttpPut("update-mon/{id}")]
        public async Task<IActionResult> UpdateMon(string id, [FromBody] MonAn monAn)
        {
            var monAnExist = await _context.MonAns.FindAsync(id);
            if (monAnExist == null)
            {
                return NotFound("Món ăn không tồn tại.");
            }

            if (string.IsNullOrEmpty(monAn.TenMon) && monAn.GiaMon > 0 && string.IsNullOrEmpty(monAn.AnhMon) && string.IsNullOrEmpty(monAn.MaLoai))
            {
                return BadRequest("Không có thông tin nào được cập nhật.");
            }

            if (!string.IsNullOrEmpty(monAn.TenMon))
                monAnExist.TenMon = monAn.TenMon;

            if (monAn.GiaMon > 0)
                monAnExist.GiaMon = monAn.GiaMon;

            if (!string.IsNullOrEmpty(monAn.AnhMon))
                monAnExist.AnhMon = monAn.AnhMon;

            if (!string.IsNullOrEmpty(monAn.MaLoai))
            {
                var loaiMon = await _context.LoaiMons.FindAsync(monAn.MaLoai);
                if (loaiMon == null)
                {
                    return NotFound("Loại món không tồn tại.");
                }
                monAnExist.MaLoai = monAn.MaLoai;
            }

            _context.MonAns.Update(monAnExist);
            await _context.SaveChangesAsync();

            return Ok(monAnExist);
        }

        [HttpPut("toggle-mon/{id}")]
        public async Task<IActionResult> ToggleMon(string id)
        {
            var monAn = await _context.MonAns.FindAsync(id);
            if (monAn == null)
            {
                return NotFound("Món ăn không tồn tại.");
            }

            monAn.TrangThai = !monAn.TrangThai;
            _context.MonAns.Update(monAn);
            await _context.SaveChangesAsync();

            string status = monAn.TrangThai ? "kích hoạt" : "vô hiệu hóa";
            return Ok($"Món ăn '{monAn.TenMon}' đã được {status}.");
        }
    }
}
