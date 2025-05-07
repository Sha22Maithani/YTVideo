using Microsoft.AspNetCore.Mvc;
using YShorts.Models;
using YShorts.Services;
using System.Threading.Tasks;

namespace YShorts.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShortsController : ControllerBase
    {
        private readonly ILogger<ShortsController> _logger;
        private readonly ShortsService _shortsService;
        private readonly string _outputDir;

        public ShortsController(
            ILogger<ShortsController> logger,
            ShortsService shortsService)
        {
            _logger = logger;
            _shortsService = shortsService;
            _outputDir = Path.Combine(Path.GetTempPath(), "YShorts", "output");
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateShorts([FromBody] CreateShortsRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.YoutubeUrl))
            {
                return BadRequest("YouTube URL is required");
            }
            
            if (request.BestMoments == null || !request.BestMoments.Any())
            {
                return BadRequest("Best moments are required");
            }

            _logger.LogInformation("Received request to create shorts for URL: {Url}", request.YoutubeUrl);

            var result = await _shortsService.CreateShortsFromBestMomentsAsync(request.YoutubeUrl, request.BestMoments);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        
        [HttpGet("download/{filename}")]
        public IActionResult DownloadShort(string filename)
        {
            var filePath = Path.Combine(_outputDir, filename);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound($"Short clip {filename} not found");
            }
            
            return PhysicalFile(filePath, "video/mp4", filename);
        }
        
        [HttpGet("thumbnail/{filename}")]
        public IActionResult GetThumbnail(string filename)
        {
            var filePath = Path.Combine(_outputDir, filename);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound($"Thumbnail {filename} not found");
            }
            
            return PhysicalFile(filePath, "image/jpeg");
        }
    }
} 