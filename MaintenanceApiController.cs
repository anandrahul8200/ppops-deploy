using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PowerPlantOps.Web.Controllers
{
    [Route("api/maintenance")]
    [ApiController]
    [Authorize]  // Fix #7: Added authentication
    public class MaintenanceApiController : ControllerBase
    {
        private readonly PowerPlantDbContext _db;
        private readonly ILogger<MaintenanceApiController> _logger;

        public MaintenanceApiController(PowerPlantDbContext db, ILogger<MaintenanceApiController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // Fix #1: Use LINQ instead of raw SQL to prevent SQL injection
        [HttpGet("search")]
        public async Task<IActionResult> SearchOrders([FromQuery] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest("keyword is required.");

            var results = await _db.MaintenanceOrders
                .Where(o => o.Description.Contains(keyword))
                .ToListAsync();
            return Ok(results);
        }

        // Fix #2: Use a DTO instead of binding directly to entity
        [HttpPost("create")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            var order = new MaintenanceOrder
            {
                Description = dto.Description,
                EquipmentId = dto.EquipmentId,
                Priority = dto.Priority
            };
            _db.MaintenanceOrders.Add(order);
            await _db.SaveChangesAsync();
            return Ok(order);
        }

        // Fix #3: Added null check and authorization comment
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _db.MaintenanceOrders.FindAsync(id);
            if (order is null)
                return NotFound($"Order {id} not found.");

            _db.MaintenanceOrders.Remove(order);
            await _db.SaveChangesAsync();
            return Ok("Deleted");
        }

        // Fix #4: Removed SSN from logs, added null check
        [HttpPost("assign")]
        public async Task<IActionResult> AssignTechnician([FromBody] AssignRequest request)
        {
            _logger.LogInformation("Assigning technician {TechnicianName} to order {OrderId}",
                request.TechnicianName, request.OrderId);

            var order = await _db.MaintenanceOrders.FindAsync(request.OrderId);
            if (order is null)
                return NotFound($"Order {request.OrderId} not found.");

            order.AssignedTo = request.TechnicianName;
            await _db.SaveChangesAsync();
            return Ok(order);
        }

        // Fix #5: Sanitized filename to prevent path traversal
        [HttpGet("export")]
        public IActionResult ExportReport([FromQuery] string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return BadRequest("filename is required.");

            var safeName = Path.GetFileName(filename);
            var path = Path.Combine("/var/reports", safeName);

            if (!System.IO.File.Exists(path))
                return NotFound("Report not found.");

            var content = System.IO.File.ReadAllText(path);
            return Ok(content);
        }

        // Fix #6: Added pagination to prevent resource exhaustion
        [HttpGet("full-report")]
        public async Task<IActionResult> GetFullReport([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            pageSize = Math.Clamp(pageSize, 1, 200);

            var orders = await _db.MaintenanceOrders
                .Include(o => o.Equipment)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(orders);
        }
    }

    public class CreateOrderDto
    {
        public string Description { get; set; }
        public int EquipmentId { get; set; }
        public string Priority { get; set; }
    }

    public class AssignRequest
    {
        public int OrderId { get; set; }
        public string TechnicianName { get; set; }
    }
}