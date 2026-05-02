using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PowerPlantOps.Web.Controllers
{
    [Route("api/maintenance")]
    [ApiController]
    public class MaintenanceApiController : ControllerBase
    {
        private readonly PowerPlantDbContext _db;
        private readonly ILogger<MaintenanceApiController> _logger;

        public MaintenanceApiController(PowerPlantDbContext db, ILogger<MaintenanceApiController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // Vulnerability 1: SQL Injection via string concatenation
        [HttpGet("search")]
        public async Task<IActionResult> SearchOrders([FromQuery] string keyword)
        {
            var query = $"SELECT * FROM \"MaintenanceOrders\" WHERE \"Description\" LIKE '%{keyword}%'";
            var results = await _db.MaintenanceOrders.FromSqlRaw(query).ToListAsync();
            return Ok(results);
        }

        // Vulnerability 2: Mass assignment / over-posting
        [HttpPost("create")]
        public async Task<IActionResult> CreateOrder([FromBody] MaintenanceOrder order)
        {
            _db.MaintenanceOrders.Add(order);
            await _db.SaveChangesAsync();
            return Ok(order);
        }

        // Vulnerability 3: Insecure direct object reference (no ownership check)
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _db.MaintenanceOrders.FindAsync(id);
            _db.MaintenanceOrders.Remove(order);
            await _db.SaveChangesAsync();
            return Ok("Deleted");
        }

        // Vulnerability 4: Sensitive data exposure in logs
        [HttpPost("assign")]
        public async Task<IActionResult> AssignTechnician([FromBody] AssignRequest request)
        {
            _logger.LogInformation($"Assigning technician SSN:{request.TechnicianSSN} to order {request.OrderId}");
            var order = await _db.MaintenanceOrders.FindAsync(request.OrderId);
            order.AssignedTo = request.TechnicianName;
            await _db.SaveChangesAsync();
            return Ok(order);
        }

        // Vulnerability 5: Unrestricted file path access
        [HttpGet("export")]
        public IActionResult ExportReport([FromQuery] string filename)
        {
            var path = $"/var/reports/{filename}";
            var content = System.IO.File.ReadAllText(path);
            return Ok(content);
        }

        // Vulnerability 6: No rate limiting on expensive operation
        [HttpGet("full-report")]
        public async Task<IActionResult> GetFullReport()
        {
            var allOrders = await _db.MaintenanceOrders
                .Include(o => o.Equipment)
                .Include(o => o.Equipment.Plant)
                .ToListAsync();

            var allEquipment = await _db.Equipment.ToListAsync();
            var allPlants = await _db.Plants.ToListAsync();
            var allLogs = await _db.GenerationLogs.ToListAsync();

            return Ok(new { allOrders, allEquipment, allPlants, allLogs });
        }
    }

    public class AssignRequest
    {
        public int OrderId { get; set; }
        public string TechnicianName { get; set; }
        public string TechnicianSSN { get; set; }
    }
}
