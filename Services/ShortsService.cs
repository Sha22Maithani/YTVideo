using System.Diagnostics;
using System.ComponentModel;
using YShorts.Models;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using System.IO;
using System.Linq;

namespace YShorts.Services
{
    public class ShortsService
    {
        private readonly ILogger<ShortsService> _logger;
        private readonly string _tempDir;
        private readonly string _outputDir;

        public ShortsService(ILogger<ShortsService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _tempDir = Path.Combine(Path.GetTempPath(), "YShorts", "temp");
            _outputDir = Path.Combine(Path.GetTempPath(), "YShorts", "output");
            
            if (!Directory.Exists(_tempDir))
            {
                Directory.CreateDirectory(_tempDir);
            }
            
            if (!Directory.Exists(_outputDir))
            {
                Directory.CreateDirectory(_outputDir);
            }
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
                    Shorts = shorts
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
                        // Optimized command: use copy codec for video (no re-encoding), use fastest preset
                        Arguments = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac -b:a 128k -preset ultrafast -movflags +faststart -progress pipe:1 \"{outputPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    var progressTimer = new System.Timers.Timer(2000); // Log progress every 2 seconds
                    progressTimer.Elapsed += (sender, e) => {
                        _logger.LogInformation("Muxing in progress... (This may take a few minutes for longer videos)");
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
                var outputPath = Path.Combine(_outputDir, $"short_{i + 1}.mp4");
                
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
                        // Use FFmpeg to create the clip
                        using (var process = new Process())
                        {
                            process.StartInfo = new ProcessStartInfo
                            {
                                FileName = "ffmpeg",
                                Arguments = $"-i \"{videoPath}\" -ss {startTime.TotalSeconds} -t {duration.TotalSeconds} -c:v libx264 -c:a aac -strict experimental \"{outputPath}\"",
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
                                continue;
                            }
                        }
                    }
                    catch (Win32Exception ex) when (ex.Message.Contains("The system cannot find the file specified"))
                    {
                        throw new InvalidOperationException("FFmpeg is not installed or not in the system PATH. Please install FFmpeg: https://ffmpeg.org/download.html");
                    }
                    
                    try
                    {
                        // Create a thumbnail
                        var thumbnailPath = Path.Combine(_outputDir, $"thumbnail_{i + 1}.jpg");
                        using (var process = new Process())
                        {
                            process.StartInfo = new ProcessStartInfo
                            {
                                FileName = "ffmpeg",
                                Arguments = $"-i \"{outputPath}\" -ss 0 -frames:v 1 \"{thumbnailPath}\"",
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
                    
                    // Add the short to the list
                    shorts.Add(new ShortClip
                    {
                        Id = i + 1,
                        Title = $"Short {i + 1}: {moment.Content.Substring(0, Math.Min(50, moment.Content.Length))}...",
                        Duration = $"{duration.Minutes:D2}:{duration.Seconds:D2}",
                        ThumbnailUrl = $"/api/shorts/thumbnail/{Path.GetFileName($"thumbnail_{i + 1}.jpg")}",
                        DownloadUrl = $"/api/shorts/download/{Path.GetFileName(outputPath)}"
                    });
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