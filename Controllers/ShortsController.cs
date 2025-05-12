using Microsoft.AspNetCore.Mvc;
using YShorts.Models;
using YShorts.Services;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;

namespace YShorts.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShortsController : ControllerBase
    {
        private readonly ILogger<ShortsController> _logger;
        private readonly ShortsService _shortsService;

        public ShortsController(
            ILogger<ShortsController> logger,
            ShortsService shortsService)
        {
            _logger = logger;
            _shortsService = shortsService;
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
        
        [HttpPost("create-with-aspect")]
        public async Task<IActionResult> CreateShortsWithAspectRatio([FromBody] CreateShortsWithAspectRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.YoutubeUrl))
            {
                return BadRequest("YouTube URL is required");
            }
            
            if (request.BestMoments == null || !request.BestMoments.Any())
            {
                return BadRequest("Best moments are required");
            }

            if (!Enum.IsDefined(typeof(AspectRatio), request.AspectRatio))
            {
                return BadRequest("Invalid aspect ratio. Valid values are: 0 (Landscape), 1 (Portrait), 2 (Square)");
            }

            _logger.LogInformation("Received request to create shorts with aspect ratio {Ratio} for URL: {Url}", 
                request.AspectRatio, request.YoutubeUrl);

            var result = await _shortsService.CreateShortsWithAspectRatioAsync(request.YoutubeUrl, 
                request.BestMoments, request.AspectRatio);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        
        [HttpGet("download/{filename}")]
        public IActionResult DownloadShort(string filename)
        {
            var filePath = _shortsService.GetShortFilePath(filename);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound($"Short clip {filename} not found");
            }
            
            // Set Content-Disposition to attachment to trigger download
            return PhysicalFile(filePath, "video/mp4", filename);
        }
        
        [HttpGet("preview/{filename}")]
        public IActionResult PreviewShort(string filename)
        {
            var filePath = _shortsService.GetShortFilePath(filename);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound($"Short clip {filename} not found");
            }
            
            // Set Content-Disposition to inline to allow browser playback
            return PhysicalFile(filePath, "video/mp4", enableRangeProcessing: true);
        }
        
        [HttpGet("thumbnail/{filename}")]
        public IActionResult GetThumbnail(string filename)
        {
            var filePath = _shortsService.GetShortFilePath(filename);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound($"Thumbnail {filename} not found");
            }
            
            return PhysicalFile(filePath, "image/jpeg");
        }

        [HttpPost("generate-selected")]
        public async Task<IActionResult> GenerateSelectedShorts([FromBody] GenerateSelectedShortsRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SourceVideoPath))
            {
                return BadRequest("Source video path is required");
            }
            
            if (request.SelectedShorts == null || !request.SelectedShorts.Any())
            {
                return BadRequest("At least one short must be selected");
            }

            _logger.LogInformation("Received request to generate {Count} selected shorts", request.SelectedShorts.Count);
            
            // Convert the ShortClipGenerationRequest objects to ShortClip objects
            var shorts = request.SelectedShorts.Select(s => {
                var shortClip = new ShortClip
                {
                    Id = s.Id,
                    Title = s.Title,
                    Duration = s.Duration,
                    ThumbnailUrl = s.ThumbnailUrl,
                    DownloadUrl = s.DownloadUrl,
                    PreviewUrl = s.PreviewUrl,
                    FilePath = s.FilePath,
                    FileName = s.FileName,
                    AspectRatio = s.AspectRatio,
                    IsGenerated = s.IsGenerated,
                    AdditionalProperties = new Dictionary<string, object>
                    {
                        { "StartTimeSeconds", s.StartTimeSeconds },
                        { "EndTimeSeconds", s.EndTimeSeconds },
                        { "Content", s.Content }
                    }
                };
                return shortClip;
            }).ToList();

            var result = await _shortsService.GenerateSelectedShortsAsync(request.SourceVideoPath, shorts);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpGet("preview-moment")]
        public async Task<IActionResult> PreviewMoment(string videoPath, double startTime, double duration)
        {
            if (string.IsNullOrEmpty(videoPath) || !System.IO.File.Exists(videoPath))
            {
                return NotFound("Video file not found");
            }

            try
            {
                // Clean up old preview files
                _shortsService.CleanupOldPreviews();
                
                // Create a temporary file to store the preview segment
                var tempDir = Path.Combine(Path.GetTempPath(), "YShorts", "previews");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                // Generate a unique filename for this preview
                var previewFileName = $"preview_{Path.GetFileNameWithoutExtension(videoPath)}_{startTime}_{duration}_{Guid.NewGuid():N}.mp4";
                var previewPath = Path.Combine(tempDir, previewFileName);

                // Check if the preview file already exists
                if (!System.IO.File.Exists(previewPath))
                {
                    // Use FFmpeg to create a preview segment
                    using (var process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            // Extract just the segment we want to preview
                            Arguments = $"-y -ss {startTime} -i \"{videoPath}\" -t {duration} -c copy \"{previewPath}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        
                        process.Start();
                        await process.WaitForExitAsync();
                        
                        if (process.ExitCode != 0)
                        {
                            var error = await process.StandardError.ReadToEndAsync();
                            _logger.LogError("FFmpeg failed to create preview: {Error}", error);
                            return StatusCode(500, "Failed to create video preview");
                        }
                    }
                }

                // Return the preview file
                return PhysicalFile(previewPath, "video/mp4", enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating video preview: {Message}", ex.Message);
                return StatusCode(500, "Error creating video preview");
            }
        }
    }
} 