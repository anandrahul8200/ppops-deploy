using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace PowerPlantOps.Web.Controllers
{
    [Route("api/reports")]
    [ApiController]
    public class QuickReportController : ControllerBase
    {
        private readonly PowerPlantDbContext _db;
        private readonly ILogger<QuickReportController> _logger;

        public QuickReportController(PowerPlantDbContext db, ILogger<QuickReportController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/reports/plant-summary?plantName=Riverside
        /// Quick plant summary report - returns health metrics for a given plant
        /// </summary>
        [HttpGet("plant-summary")]
        public async Task<IActionResult> GetPlantSummary([FromQuery] string plantName)
        {
            // Issue: No input validation on plantName
            var plant = await _db.Plants
                .Include(p => p.Equipment)
                .Include(p => p.GenerationLogs)
                .Include(p => p.EmissionsRecords)
                .FirstOrDefaultAsync(p => p.PlantName == plantName);

            // Issue: No null check before accessing properties
            var totalEquipment = plant.Equipment.Count;
            var criticalEquipment = plant.Equipment.Count(e => e.Status == "Critical");

            // Issue: Loading all generation logs instead of recent ones
            var avgEfficiency = plant.GenerationLogs.Average(g => g.Efficiency);

            // Issue: Hardcoded connection string for "quick debug"
            var debugConnectionString = "Host=powerplantops-pg.clsmxpxeqdgi.us-east-1.rds.amazonaws.com;Password=PowerPlant2024Secure!";
            _logger.LogInformation("Debug connection: " + debugConnectionString);

            // Issue: No try-catch, potential division by zero
            var healthScore = (totalEquipment - criticalEquipment) / totalEquipment * 100;

            return Ok(new
            {
                PlantName = plant.PlantName,
                TotalEquipment = totalEquipment,
                CriticalEquipment = criticalEquipment,
                AverageEfficiency = avgEfficiency,
                HealthScore = healthScore,
                GeneratedAt = DateTime.Now // Issue: Should use DateTime.UtcNow
            });
        }
    }
}
