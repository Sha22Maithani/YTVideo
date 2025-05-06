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
        private readonly TranscriptionService _transcriptionService;

        public ShortsController(
            ILogger<ShortsController> logger,
            TranscriptionService transcriptionService)
        {
            _logger = logger;
            _transcriptionService = transcriptionService;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateShorts([FromBody] ShortsGenerationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.YoutubeUrl))
            {
                return BadRequest("YouTube URL is required");
            }

            if (request.Count <= 0 || request.Count > 10)
            {
                return BadRequest("Count must be between 1 and 10");
            }

            if (request.Duration < 15 || request.Duration > 60)
            {
                return BadRequest("Duration must be between 15 and 60 seconds");
            }

            _logger.LogInformation("Received shorts generation request for URL: {Url}, Count: {Count}, Duration: {Duration}s", 
                request.YoutubeUrl, request.Count, request.Duration);

            // TODO: Implement actual shorts generation
            // This is a placeholder that returns mock data
            var mockShorts = new List<ShortClip>();
            for (int i = 1; i <= request.Count; i++)
            {
                mockShorts.Add(new ShortClip
                {
                    Id = i,
                    Title = $"Short Clip {i}",
                    Duration = $"0:{new Random().Next(20, 55)}",
                    ThumbnailUrl = "https://via.placeholder.com/150",
                    DownloadUrl = "#"
                });
            }

            return Ok(new ShortsGenerationResponse 
            { 
                Success = true,
                Shorts = mockShorts
            });
        }
    }
} 