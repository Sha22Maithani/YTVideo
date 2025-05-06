using Microsoft.AspNetCore.Mvc;
using YShorts.Models;
using YShorts.Services;

namespace YShorts.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranscriptionController : ControllerBase
    {
        private readonly ILogger<TranscriptionController> _logger;
        private readonly TranscriptionService _transcriptionService;

        public TranscriptionController(
            ILogger<TranscriptionController> logger,
            TranscriptionService transcriptionService)
        {
            _logger = logger;
            _transcriptionService = transcriptionService;
        }

        [HttpPost("transcribe")]
        public async Task<IActionResult> TranscribeVideo([FromBody] VideoUrlRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.YoutubeUrl))
            {
                return BadRequest("YouTube URL is required");
            }

            _logger.LogInformation("Received transcription request for URL: {Url}", request.YoutubeUrl);

            var result = await _transcriptionService.TranscribeYouTubeVideoAsync(request.YoutubeUrl);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
} 