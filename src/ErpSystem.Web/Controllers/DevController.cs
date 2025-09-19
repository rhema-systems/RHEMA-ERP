using ErpSystem.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Web.Controllers
{
    [ApiController]
    [Route("api/dev")]
    public class DevController : ControllerBase
    {
        private readonly IFeatureService _featureService;
        private readonly IDevInfoService _devInfoService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DevController> _logger;

        public DevController(
            IFeatureService featureService,
            IDevInfoService devInfoService,
            IWebHostEnvironment environment,
            ILogger<DevController> logger)
        {
            _featureService = featureService;
            _devInfoService = devInfoService;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet("features")]
        public IActionResult GetFeatures()
        {
            if (!_environment.IsDevelopment())
            {
                return Forbid("Development endpoints only available in development environment");
            }

            try
            {
                var features = _featureService.GetAllFeatures();
                return Ok(features);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get features");
                return StatusCode(500, new { Error = "Failed to retrieve features" });
            }
        }

        [HttpGet("system")]
        public async Task<IActionResult> GetSystemInfo()
        {
            if (!_environment.IsDevelopment())
            {
                return Forbid("Development endpoints only available in development environment");
            }

            try
            {
                var systemInfo = await _devInfoService.GetSystemInfoAsync();
                return Ok(systemInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get system info");
                return StatusCode(500, new { Error = "Failed to retrieve system information" });
            }
        }

        [HttpGet("health")]
        public async Task<IActionResult> GetHealthInfo()
        {
            if (!_environment.IsDevelopment())
            {
                return Forbid("Development endpoints only available in development environment");
            }

            try
            {
                var healthInfo = await _devInfoService.GetHealthInfoAsync();
                return Ok(healthInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get health info");
                return StatusCode(500, new { Error = "Failed to retrieve health information" });
            }
        }

        [HttpGet("environment")]
        public async Task<IActionResult> GetEnvironmentInfo()
        {
            if (!_environment.IsDevelopment())
            {
                return Forbid("Development endpoints only available in development environment");
            }

            try
            {
                var envInfo = await _devInfoService.GetEnvironmentInfoAsync();
                return Ok(envInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get environment info");
                return StatusCode(500, new { Error = "Failed to retrieve environment information" });
            }
        }

        [HttpPost("test-exception")]
        public IActionResult TestException()
        {
            if (!_environment.IsDevelopment())
            {
                return Forbid("Development endpoints only available in development environment");
            }

            _logger.LogInformation("Test exception requested");
            throw new InvalidOperationException("This is a test exception for development purposes");
        }

        [HttpGet("routes")]
        public IActionResult GetRoutes()
        {
            if (!_environment.IsDevelopment())
            {
                return Forbid("Development endpoints only available in development environment");
            }

            var routes = new
            {
                DevelopmentRoutes = new[]
                {
                    "GET /api/dev/features - Get feature flags",
                    "GET /api/dev/system - Get system information",
                    "GET /api/dev/health - Get health information",
                    "GET /api/dev/environment - Get environment information",
                    "GET /api/dev/routes - This endpoint",
                    "POST /api/dev/test-exception - Throw test exception"
                },
                BuiltInRoutes = new[]
                {
                    "GET /health - Application health checks",
                    "GET /health/ready - Readiness checks",
                    "GET /health/live - Liveness checks"
                },
                Timestamp = DateTime.UtcNow
            };

            return Ok(routes);
        }
    }
}