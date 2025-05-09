using System.Diagnostics;
using System.ComponentModel;
using YShorts.Models;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Converter;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;

namespace YShorts.Services
{
    public class ShortsService
    {
        private readonly ILogger<ShortsService> _logger;
        private readonly string _tempDir;
        private readonly string _outputDir;
        private readonly string _publicOutputDir;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ShortsService(ILogger<ShortsService> logger, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            
            // Get configured directories from appsettings.json or use defaults
            var configuredOutputDir = configuration["Shorts:OutputDirectory"];
            _tempDir = Path.Combine(Path.GetTempPath(), "YShorts", "temp");
            
            // If configured output directory is absolute, use it; otherwise, make it relative to wwwroot
            if (!string.IsNullOrEmpty(configuredOutputDir) && Path.IsPathRooted(configuredOutputDir))
            {
                _outputDir = configuredOutputDir;
            }
            else
            {
                // Create a directory in wwwroot for public access
                _publicOutputDir = string.IsNullOrEmpty(configuredOutputDir) ? "shorts" : configuredOutputDir;
                _outputDir = Path.Combine(_webHostEnvironment.WebRootPath, _publicOutputDir);
            }
            
            if (!Directory.Exists(_tempDir))
            {
                Directory.CreateDirectory(_tempDir);
            }
            
            if (!Directory.Exists(_outputDir))
            {
                Directory.CreateDirectory(_outputDir);
            }
            
            _logger.LogInformation("Shorts will be saved to: {OutputDir}", _outputDir);
        }

        /// <summary>
        /// Gets the web-accessible URL for a short clip
        /// </summary>
        public string GetShortPreviewUrl(string filename)
        {
            return $"/{_publicOutputDir}/{filename}";
        }
        
        /// <summary>
        /// Gets the absolute file path for a short clip
        /// </summary>
        public string GetShortFilePath(string filename)
        {
            return Path.Combine(_outputDir, filename);
        }

        public async Task<ShortsGenerationResponse> CreateShortsFromBestMomentsAsync(string youtubeUrl, List<BestMoment> bestMoments)
        {
            try
            {
                _logger.LogInformation("Creating shorts from best moments for video: {Url}", youtubeUrl);
                
                // Download the full video
                var videoPath = await DownloadYouTubeVideoAsync(youtubeUrl);
                
                // Create shorts from best moments
                var shorts = await CreateShortClipsAsync(videoPath, bestMoments);
                
                // Clean up the full video file
                File.Delete(videoPath);
                
                return new ShortsGenerationResponse
                {
                    Success = true,
                    Shorts = shorts,
                    OutputDirectory = _outputDir
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create shorts: {Message}", ex.Message);
                return new ShortsGenerationResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to create shorts: {ex.Message}"
                };
            }
        }

        private async Task<string> DownloadYouTubeVideoAsync(string youtubeUrl)
        {
            var youtube = new YoutubeClient();
            var videoId = VideoId.Parse(youtubeUrl);
            var outputPath = Path.Combine(_tempDir, $"{videoId}.mp4");
            
            _logger.LogInformation("Downloading video from YouTube: {VideoId}", videoId);
            
            try
            {
                // Try using the YoutubeExplode.Converter for faster, direct downloading
                try
                {
                    _logger.LogInformation("Attempting to download video using optimized converter...");
                    
                    // Download with optimized settings
                    await youtube.Videos.DownloadAsync(
                        youtubeUrl, 
                        outputPath, 
                        o => o
                            .SetPreset(ConversionPreset.UltraFast) // Use fastest preset
                            .SetFFmpegPath("ffmpeg") // Use system ffmpeg
                    );
                    
                    _logger.LogInformation("Successfully downloaded video using optimized converter");
                    return outputPath;
                }
                catch (Exception converterEx)
                {
                    _logger.LogWarning("Optimized converter failed, falling back to manual method: {Message}", converterEx.Message);
                    // Fall back to the original method if converter fails
                }
                
                // Original implementation as fallback
                // Get video info
                var video = await youtube.Videos.GetAsync(videoId);
                _logger.LogInformation("Successfully retrieved video info: {Title}", video.Title);
                
                // Get stream manifest
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoId);
                
                // Try to get muxed streams
                var muxedStreams = streamManifest.GetMuxedStreams().ToList();
                IStreamInfo streamInfo = null;
                
                if (muxedStreams.Any())
                {
                    _logger.LogInformation("Found {Count} muxed streams", muxedStreams.Count);
                    streamInfo = muxedStreams.OrderByDescending(s => s.VideoQuality).First();
                }
                else
                {
                    _logger.LogInformation("No muxed streams available, looking for separate streams");
                    
                    // Get video and audio streams
                    var videoStreams = streamManifest.GetVideoOnlyStreams().ToList();
                    var audioStreams = streamManifest.GetAudioOnlyStreams().ToList();
                    
                    if (!videoStreams.Any())
                    {
                        _logger.LogWarning("No video streams found for this video");
                        throw new InvalidOperationException("No video streams available for this video");
                    }
                    
                    if (!audioStreams.Any())
                    {
                        _logger.LogWarning("No audio streams found for this video");
                        // We can still proceed with video-only
                    }
                    
                    // Get best video stream
                    var videoStreamInfo = videoStreams.OrderByDescending(s => s.VideoQuality).First();
                    
                    if (audioStreams.Any())
                    {
                        // Get best audio stream
                        var audioStreamInfo = audioStreams.OrderByDescending(s => s.Bitrate).First();
                        
                        var videoTempPath = Path.Combine(_tempDir, $"{videoId}_video.{videoStreamInfo.Container}");
                        var audioTempPath = Path.Combine(_tempDir, $"{videoId}_audio.{audioStreamInfo.Container}");
                        
                        // Download video and audio
                        _logger.LogInformation("Downloading video stream: {Quality}", videoStreamInfo.VideoQuality);
                        await youtube.Videos.Streams.DownloadAsync(videoStreamInfo, videoTempPath);
                        
                        _logger.LogInformation("Downloading audio stream: {Bitrate}", audioStreamInfo.Bitrate);
                        await youtube.Videos.Streams.DownloadAsync(audioStreamInfo, audioTempPath);
                        
                        // Mux video and audio using FFmpeg
                        _logger.LogInformation("Muxing video and audio streams");
                        await MuxVideoAndAudioAsync(videoTempPath, audioTempPath, outputPath);
                        
                        // Clean up temporary files
                        File.Delete(videoTempPath);
                        File.Delete(audioTempPath);
                    }
                    else
                    {
                        // Download video-only stream
                        _logger.LogInformation("Downloading video-only stream: {Quality}", videoStreamInfo.VideoQuality);
                        await youtube.Videos.Streams.DownloadAsync(videoStreamInfo, outputPath);
                    }
                    
                    return outputPath;
                }
                
                // Download the muxed stream
                _logger.LogInformation("Downloading muxed stream: {Quality}", ((MuxedStreamInfo)streamInfo).VideoQuality);
                await youtube.Videos.Streams.DownloadAsync(streamInfo, outputPath);
                
                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading YouTube video: {Message}", ex.Message);
                throw new InvalidOperationException($"Failed to download video: {ex.Message}", ex);
            }
        }
        
        private async Task MuxVideoAndAudioAsync(string videoPath, string audioPath, string outputPath)
        {
            try
            {
                _logger.LogInformation("Starting to mux video and audio...");
                
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        // Even faster muxing: use copy codec for both video and audio (no re-encoding), higher thread count
                        Arguments = $"-y -i \"{videoPath}\" -i \"{audioPath}\" -c copy -movflags +faststart -threads 4 \"{outputPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    var progressTimer = new System.Timers.Timer(2000); // Log progress every 2 seconds
                    progressTimer.Elapsed += (sender, e) => {
                        _logger.LogInformation("Muxing in progress...");
                    };
                    progressTimer.Start();
                    
                    process.Start();
                    
                    // Read output asynchronously
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    
                    await process.WaitForExitAsync();
                    progressTimer.Stop();
                    
                    if (process.ExitCode != 0)
                    {
                        var error = await errorTask;
                        _logger.LogError("FFmpeg failed with error: {Error}", error);
                        throw new Exception($"FFmpeg failed with error: {error}");
                    }
                    
                    _logger.LogInformation("Muxing completed successfully");
                }
            }
            catch (Win32Exception ex) when (ex.Message.Contains("The system cannot find the file specified"))
            {
                throw new InvalidOperationException("FFmpeg is not installed or not in the system PATH. Please install FFmpeg: https://ffmpeg.org/download.html");
            }
        }

        private async Task<List<ShortClip>> CreateShortClipsAsync(string videoPath, List<BestMoment> bestMoments)
        {
            var shorts = new List<ShortClip>();
            
            for (int i = 0; i < bestMoments.Count; i++)
            {
                var moment = bestMoments[i];
                var shortFileName = $"short_{i + 1}.mp4";
                var outputPath = Path.Combine(_outputDir, shortFileName);
                
                try
                {
                    _logger.LogInformation("Creating short for moment {Index}: {Content}", i + 1, moment.Content);
                    
                    // Parse timestamps
                    if (!TimeSpan.TryParse($"00:{moment.StartTimestamp}", out TimeSpan startTime) ||
                        !TimeSpan.TryParse($"00:{moment.EndTimestamp}", out TimeSpan endTime))
                    {
                        _logger.LogWarning("Invalid timestamp format for moment {Index}", i + 1);
                        continue;
                    }
                    
                    var duration = endTime - startTime;
                    
                    try
                    {
                        // Use FFmpeg to create the clip with stream copying (no re-encoding)
                        using (var process = new Process())
                        {
                            process.StartInfo = new ProcessStartInfo
                            {
                                FileName = "ffmpeg",
                                // Use -c copy for ultra-fast stream copying without re-encoding
                                // Add -avoid_negative_ts 1 to handle timestamp issues
                                // Use -vsync 2 for smoother video when cutting
                                Arguments = $"-y -ss {startTime.TotalSeconds} -i \"{videoPath}\" -t {duration.TotalSeconds} -c copy -avoid_negative_ts 1 -vsync 2 \"{outputPath}\"",
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
                                _logger.LogError("FFmpeg failed with error: {Error}", error);
                                
                                // Fallback to slower but more reliable method if fast method fails
                                _logger.LogInformation("Trying fallback method for clip creation...");
                                using (var fallbackProcess = new Process())
                                {
                                    fallbackProcess.StartInfo = new ProcessStartInfo
                                    {
                                        FileName = "ffmpeg",
                                        Arguments = $"-y -i \"{videoPath}\" -ss {startTime.TotalSeconds} -t {duration.TotalSeconds} -c:v libx264 -preset ultrafast -c:a aac -b:a 128k -threads 4 \"{outputPath}\"",
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        UseShellExecute = false,
                                        CreateNoWindow = true
                                    };
                                    
                                    fallbackProcess.Start();
                                    await fallbackProcess.WaitForExitAsync();
                                    
                                    if (fallbackProcess.ExitCode != 0)
                                    {
                                        error = await fallbackProcess.StandardError.ReadToEndAsync();
                                        _logger.LogError("Fallback FFmpeg method also failed: {Error}", error);
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                    catch (Win32Exception ex) when (ex.Message.Contains("The system cannot find the file specified"))
                    {
                        throw new InvalidOperationException("FFmpeg is not installed or not in the system PATH. Please install FFmpeg: https://ffmpeg.org/download.html");
                    }
                    
                    // Create thumbnails
                    var thumbnailFileName = $"thumbnail_{i + 1}.jpg";
                    var thumbnailPath = Path.Combine(_outputDir, thumbnailFileName);
                    
                    try
                    {
                        // Create a thumbnail using a faster approach
                        using (var process = new Process())
                        {
                            process.StartInfo = new ProcessStartInfo
                            {
                                FileName = "ffmpeg",
                                // Extract thumbnail directly from the source video at the start point
                                // Faster than extracting from the output file
                                Arguments = $"-y -ss {startTime.TotalSeconds} -i \"{videoPath}\" -frames:v 1 -q:v 2 -threads 2 \"{thumbnailPath}\"",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            
                            process.Start();
                            await process.WaitForExitAsync();
                        }
                    }
                    catch (Win32Exception ex) when (ex.Message.Contains("The system cannot find the file specified"))
                    {
                        _logger.LogError("Could not create thumbnail: FFmpeg not found");
                        // Continue without thumbnail
                    }
                    
                    // Create preview links
                    var previewUrl = GetShortPreviewUrl(shortFileName);
                    var thumbnailUrl = GetShortPreviewUrl(thumbnailFileName);
                    
                    // Add the short to the list
                    shorts.Add(new ShortClip
                    {
                        Id = i + 1,
                        Title = $"Short {i + 1}: {moment.Content.Substring(0, Math.Min(50, moment.Content.Length))}...",
                        Duration = $"{duration.Minutes:D2}:{duration.Seconds:D2}",
                        ThumbnailUrl = thumbnailUrl,
                        DownloadUrl = $"/api/shorts/download/{shortFileName}",
                        PreviewUrl = previewUrl,
                        FilePath = outputPath,
                        FileName = shortFileName
                    });
                    
                    _logger.LogInformation("Created short clip: {Title} at {Path} (Preview: {PreviewUrl})", 
                        $"Short {i + 1}", outputPath, previewUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating short {Index}: {Message}", i + 1, ex.Message);
                }
            }
            
            return shorts;
        }
    }
} 