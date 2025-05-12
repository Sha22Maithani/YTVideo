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
        private readonly GeminiService _geminiService;
        private readonly ShortsService _shortsService;

        public TranscriptionController(
            ILogger<TranscriptionController> logger,
            TranscriptionService transcriptionService,
            GeminiService geminiService,
            ShortsService shortsService)
        {
            _logger = logger;
            _transcriptionService = transcriptionService;
            _geminiService = geminiService;
            _shortsService = shortsService;
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
        
        [HttpPost("extract-best-moments")]
        public async Task<IActionResult> ExtractBestMoments([FromBody] TranscriptionResponse transcription)
        {
            if (string.IsNullOrWhiteSpace(transcription.Text))
            {
                return BadRequest("Transcription text is required");
            }
            
            _logger.LogInformation("Extracting best moments from transcription");
            
            var result = await _geminiService.ExtractBestMomentsAsync(transcription.Text);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }
            
            return Ok(result);
        }
        
        [HttpPost("transcribe-and-extract")]
        public async Task<IActionResult> TranscribeAndExtractBestMoments([FromBody] VideoUrlRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.YoutubeUrl))
            {
                return BadRequest("YouTube URL is required");
            }

            _logger.LogInformation("Received transcription and extraction request for URL: {Url}", request.YoutubeUrl);

            // Step 1: Transcribe the video
            var transcriptionResult = await _transcriptionService.TranscribeYouTubeVideoAsync(request.YoutubeUrl);

            if (!transcriptionResult.Success)
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorMessage = $"Transcription failed: {transcriptionResult.ErrorMessage}",
                    Phase = "Transcription"
                });
            }
            
            // Step 2: Extract best moments
            var extractionResult = await _geminiService.ExtractBestMomentsAsync(transcriptionResult.Text);
            
            if (!extractionResult.Success)
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorMessage = $"Extraction failed: {extractionResult.ErrorMessage}",
                    Phase = "Extraction",
                    Transcription = transcriptionResult.Text
                });
            }
            
            // Return both the transcription and the extracted moments
            return Ok(new
            {
                Success = true,
                Transcription = transcriptionResult.Text,
                BestMoments = extractionResult.Moments
            });
        }
        
        [HttpPost("transcribe-extract-create")]
        public async Task<IActionResult> TranscribeExtractAndCreateShorts([FromBody] VideoUrlRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.YoutubeUrl))
            {
                return BadRequest("YouTube URL is required");
            }

            _logger.LogInformation("Received full pipeline request for URL: {Url}", request.YoutubeUrl);

            // Step 1: Transcribe the video
            var transcriptionResult = await _transcriptionService.TranscribeYouTubeVideoAsync(request.YoutubeUrl);

            if (!transcriptionResult.Success)
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorMessage = $"Transcription failed: {transcriptionResult.ErrorMessage}",
                    Phase = "Transcription"
                });
            }
            
            // Step 2: Extract best moments
            var extractionResult = await _geminiService.ExtractBestMomentsAsync(transcriptionResult.Text);
            
            if (!extractionResult.Success)
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorMessage = $"Extraction failed: {extractionResult.ErrorMessage}",
                    Phase = "Extraction",
                    Transcription = transcriptionResult.Text
                });
            }
            
            // Step 3: Create shorts from best moments with specified aspect ratio (or default to landscape)
            AspectRatio aspectRatio = AspectRatio.Landscape; // Default
            
            // Try to get aspect ratio from request if provided
            if (request.AspectRatio.HasValue)
            {
                aspectRatio = request.AspectRatio.Value;
            }
            
            var shortsResult = await _shortsService.CreateShortsWithAspectRatioAsync(request.YoutubeUrl, extractionResult.Moments, aspectRatio);
            
            if (!shortsResult.Success)
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorMessage = $"Shorts creation failed: {shortsResult.ErrorMessage}",
                    Phase = "ShortsCreation",
                    Transcription = transcriptionResult.Text,
                    BestMoments = extractionResult.Moments
                });
            }
            
            // Return all results
            return Ok(new
            {
                Success = true,
                Transcription = transcriptionResult.Text,
                BestMoments = extractionResult.Moments,
                Shorts = shortsResult.Shorts
            });
        }
    }
} 