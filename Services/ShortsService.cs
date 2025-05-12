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
using System.Net.Http;
using System.Net;
using System.Security.Authentication;

namespace YShorts.Services
{
    public class ShortsService
    {
        private readonly ILogger<ShortsService> _logger;
        private readonly string _tempDir;
        private readonly string _outputDir;
        private readonly string _publicOutputDir;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly YoutubeClient _youtubeClient;

        public ShortsService(ILogger<ShortsService> logger, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            
            // Configure YoutubeClient with proper TLS settings
            var handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) => true // Allow all certificates for troubleshooting
            };
            _youtubeClient = new YoutubeClient(new HttpClient(handler));
            
            // Get configured directories from appsettings.json or use defaults
            var configuredOutputDir = configuration["Shorts:OutputDirectory"];
            _tempDir = Path.Combine(Path.GetTempPath(), "YShorts", "temp");
            
            // Initialize _publicOutputDir with a default value to fix the nullable warning
            _publicOutputDir = string.IsNullOrEmpty(configuredOutputDir) ? "shorts" : configuredOutputDir;
            
            // If configured output directory is absolute, use it; otherwise, make it relative to wwwroot
            if (!string.IsNullOrEmpty(configuredOutputDir) && Path.IsPathRooted(configuredOutputDir))
            {
                _outputDir = configuredOutputDir;
            }
            else
            {
                // Create a directory in wwwroot for public access
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
            
            // Create default folder for backward compatibility
            var defaultFolder = Path.Combine(_outputDir, "default");
            if (!Directory.Exists(defaultFolder))
            {
                Directory.CreateDirectory(defaultFolder);
            }
            
            _logger.LogInformation("Shorts will be saved to: {OutputDir}", _outputDir);
        }

        /// <summary>
        /// Gets the web-accessible URL for a short clip
        /// </summary>
        public string GetShortPreviewUrl(string folderName, string filename)
        {
            return $"/{_publicOutputDir}/{folderName}/{filename}";
        }
        
        /// <summary>
        /// Gets the absolute file path for a short clip
        /// </summary>
        public string GetShortFilePath(string folderName, string filename)
        {
            return Path.Combine(_outputDir, folderName, filename);
        }
        
        /// <summary>
        /// Gets the absolute file path for a short clip in a specific folder
        /// </summary>
        public string GetShortFilePathFromFolder(string folder, string filename)
        {
            return Path.Combine(_outputDir, folder, filename);
        }

        public async Task<ShortsGenerationResponse> CreateShortsFromBestMomentsAsync(string youtubeUrl, List<BestMoment> bestMoments)
        {
            return await CreateShortsWithAspectRatioAsync(youtubeUrl, bestMoments, AspectRatio.Landscape);
        }

        public async Task<ShortsGenerationResponse> CreateShortsWithAspectRatioAsync(string youtubeUrl, List<BestMoment> bestMoments, AspectRatio aspectRatio)
        {
            try
            {
                _logger.LogInformation("Creating shorts preview with aspect ratio {AspectRatio} from best moments for video: {Url}", 
                    aspectRatio, youtubeUrl);
                
                // Download the full video
                var downloadResult = await DownloadYouTubeVideoAsync(youtubeUrl);
                
                // Create a unique folder for this video
                var videoId = VideoId.Parse(youtubeUrl);
                var folderName = $"{videoId.Value}_{DateTime.Now:yyyyMMdd_HHmmss}";
                var videoFolder = Path.Combine(_outputDir, folderName);
                
                // Ensure the folder exists
                if (!Directory.Exists(videoFolder))
                {
                    Directory.CreateDirectory(videoFolder);
                }
                
                // Copy the downloaded video to the new folder
                var videoFileName = $"source_{videoId.Value}.mp4";
                var destinationVideoPath = Path.Combine(videoFolder, videoFileName);
                File.Copy(downloadResult.VideoPath, destinationVideoPath, true);
                
                // Create preview data for shorts without generating the actual video files
                var shorts = CreateShortClipPreviews(destinationVideoPath, bestMoments, aspectRatio, folderName);
                
                return new ShortsGenerationResponse
                {
                    Success = true,
                    Shorts = shorts,
                    OutputDirectory = videoFolder,
                    SourceVideoPath = destinationVideoPath, // Store the path to use later when creating actual videos
                    FolderName = folderName, // Store the folder name for reference
                    FolderPath = GetShortPreviewUrl(folderName, "") // Web-accessible path to the folder
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create shorts previews: {Message}", ex.Message);
                return new ShortsGenerationResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to create shorts previews: {ex.Message}"
                };
            }
        }

        // New method to create actual video files from previews
        public async Task<ShortsGenerationResponse> GenerateSelectedShortsAsync(string sourceVideoPath, List<ShortClip> selectedShorts)
        {
            try
            {
                _logger.LogInformation("Generating {Count} selected shorts from video", selectedShorts.Count);
                
                // Extract folder name from the source video path
                var folderName = Path.GetFileName(Path.GetDirectoryName(sourceVideoPath) ?? "");
                if (string.IsNullOrEmpty(folderName))
                {
                    // If no folder found, create a new one
                    folderName = $"shorts_{DateTime.Now:yyyyMMdd_HHmmss}";
                    var videoFolder = Path.Combine(_outputDir, folderName);
                    Directory.CreateDirectory(videoFolder);
                }
                
                var generatedShorts = new List<ShortClip>();
                
                foreach (var shortClip in selectedShorts)
                {
                    try
                    {
                        // Get start time and duration directly from the properties sent by the client
                        double startTimeSeconds = 0;
                        double endTimeSeconds = 0;
                        
                        // First check if AdditionalProperties has the values
                        if (shortClip.AdditionalProperties != null && 
                            shortClip.AdditionalProperties.TryGetValue("StartTimeSeconds", out var startTimeObj) && 
                            shortClip.AdditionalProperties.TryGetValue("EndTimeSeconds", out var endTimeObj))
                        {
                            startTimeSeconds = Convert.ToDouble(startTimeObj);
                            endTimeSeconds = Convert.ToDouble(endTimeObj);
                            _logger.LogInformation("Using start time {Start} and end time {End} from AdditionalProperties", 
                                startTimeSeconds, endTimeSeconds);
                        }
                        // Then try to get the properties using reflection since they're not part of the model
                        else
                        {
                            var type = shortClip.GetType();
                            var startTimeProp = type.GetProperty("StartTimeSeconds");
                            var endTimeProp = type.GetProperty("EndTimeSeconds");
                            
                            if (startTimeProp != null && endTimeProp != null)
                            {
                                startTimeSeconds = Convert.ToDouble(startTimeProp.GetValue(shortClip));
                                endTimeSeconds = Convert.ToDouble(endTimeProp.GetValue(shortClip));
                                _logger.LogInformation("Using start time {Start} and end time {End} from direct properties", 
                                    startTimeSeconds, endTimeSeconds);
                            }
                            else
                            {
                                // Fallback to parsing from Duration property
                                _logger.LogWarning("No timing information found, using fallback method");
                                var durationParts = shortClip.Duration.Split(':');
                                var durationMinutes = int.Parse(durationParts[0]);
                                var durationSeconds = int.Parse(durationParts[1]);
                                endTimeSeconds = startTimeSeconds + (durationMinutes * 60) + durationSeconds;
                            }
                        }
                        
                        var startTime = TimeSpan.FromSeconds(startTimeSeconds);
                        var duration = TimeSpan.FromSeconds(endTimeSeconds - startTimeSeconds);
                        
                        _logger.LogInformation("Generating short clip {Id} from {Start} for {Duration} seconds", 
                            shortClip.Id, startTime, duration.TotalSeconds);
                        
                        // Parse aspect ratio
                        if (!Enum.TryParse<AspectRatio>(shortClip.AspectRatio, true, out var aspectRatio))
                        {
                            aspectRatio = AspectRatio.Landscape; // Default
                        }
                        
                        // Get filter args for the aspect ratio
                        string filterArgs = GetAspectRatioFilterArgs(aspectRatio);
                        
                        // Update file paths to use the folder
                        var fileName = $"short_{shortClip.Id}_{aspectRatio}.mp4";
                        var filePath = GetShortFilePath(folderName, fileName);
                        
                        // Generate the actual video file
                        await GenerateShortClipAsync(sourceVideoPath, filePath, startTime, duration, filterArgs);
                        
                        // Create thumbnail if it doesn't exist
                        var thumbnailFileName = $"thumbnail_{shortClip.Id}_{aspectRatio}.jpg";
                        var thumbnailPath = GetShortFilePath(folderName, thumbnailFileName);
                        
                        if (!File.Exists(thumbnailPath))
                        {
                            await GenerateThumbnailAsync(sourceVideoPath, thumbnailPath, startTime, filterArgs);
                        }
                        
                        // Update the shortClip with new paths
                        shortClip.FileName = fileName;
                        shortClip.FilePath = filePath;
                        shortClip.PreviewUrl = GetShortPreviewUrl(folderName, fileName);
                        shortClip.ThumbnailUrl = GetShortPreviewUrl(folderName, thumbnailFileName);
                        shortClip.DownloadUrl = $"/api/shorts/download/{folderName}/{fileName}";
                        
                        // Mark as generated
                        shortClip.IsGenerated = true;
                        generatedShorts.Add(shortClip);
                        
                        _logger.LogInformation("Generated short clip: {Title}", shortClip.Title);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error generating short {ShortId}: {Message}", shortClip.Id, ex.Message);
                    }
                }
                
                return new ShortsGenerationResponse
                {
                    Success = true,
                    Shorts = generatedShorts,
                    OutputDirectory = Path.Combine(_outputDir, folderName),
                    FolderName = folderName,
                    FolderPath = GetShortPreviewUrl(folderName, "")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate selected shorts: {Message}", ex.Message);
                return new ShortsGenerationResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to generate selected shorts: {ex.Message}"
                };
            }
        }

        private List<ShortClip> CreateShortClipPreviews(string videoPath, List<BestMoment> bestMoments, AspectRatio aspectRatio, string folderName)
        {
            var shorts = new List<ShortClip>();
            
            for (int i = 0; i < bestMoments.Count; i++)
            {
                var moment = bestMoments[i];
                var shortFileName = $"short_{i + 1}_{aspectRatio}.mp4";
                var outputPath = GetShortFilePath(folderName, shortFileName);
                
                try
                {
                    _logger.LogInformation("Creating preview for moment {Index}: {Content}", i + 1, moment.Content);
                    
                    // Parse timestamps
                    if (!TimeSpan.TryParse($"00:{moment.StartTimestamp}", out TimeSpan startTime) ||
                        !TimeSpan.TryParse($"00:{moment.EndTimestamp}", out TimeSpan endTime))
                    {
                        _logger.LogWarning("Invalid timestamp format for moment {Index}", i + 1);
                        continue;
                    }
                    
                    var duration = endTime - startTime;
                    
                    // Create preview links (without actually creating the files yet)
                    var thumbnailFileName = $"thumbnail_{i + 1}_{aspectRatio}.jpg";
                    
                    // Add the short to the list
                    shorts.Add(new ShortClip
                    {
                        Id = i + 1,
                        Title = $"Short {i + 1}: {moment.Content.Substring(0, Math.Min(50, moment.Content.Length))}...",
                        Duration = $"{duration.Minutes:D2}:{duration.Seconds:D2}",
                        ThumbnailUrl = GetShortPreviewUrl(folderName, thumbnailFileName),
                        DownloadUrl = $"/api/shorts/download/{folderName}/{shortFileName}",
                        PreviewUrl = GetShortPreviewUrl(folderName, shortFileName),
                        FilePath = outputPath,
                        FileName = shortFileName,
                        AspectRatio = aspectRatio.ToString(),
                        AdditionalProperties = new Dictionary<string, object>
                        {
                            { "StartTimeSeconds", startTime.TotalSeconds },
                            { "EndTimeSeconds", endTime.TotalSeconds },
                            { "Content", moment.Content }
                        }
                    });
                    
                    _logger.LogInformation("Created preview for short {Index}: {Title}", i + 1, moment.Content);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating short preview {Index}: {Message}", i + 1, ex.Message);
                }
            }
            
            return shorts;
        }

        // Helper method to generate a single short clip
        private async Task GenerateShortClipAsync(string videoPath, string outputPath, TimeSpan startTime, TimeSpan duration, string filterArgs)
        {
            try
            {
                // Make sure the directory exists
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Use FFmpeg to create the clip with specified aspect ratio
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        // Apply aspect ratio filter
                        Arguments = $"-y -ss {startTime.TotalSeconds} -i \"{videoPath}\" -t {duration.TotalSeconds} {filterArgs} -c:v libx264 -preset ultrafast -c:a aac -b:a 128k -threads 4 \"{outputPath}\"",
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
                        throw new InvalidOperationException($"FFmpeg failed: {error}");
                    }
                }
            }
            catch (Win32Exception ex) when (ex.Message.Contains("The system cannot find the file specified"))
            {
                throw new InvalidOperationException("FFmpeg is not installed or not in the system PATH. Please install FFmpeg: https://ffmpeg.org/download.html");
            }
        }

        // Helper method to generate a thumbnail
        private async Task GenerateThumbnailAsync(string videoPath, string thumbnailPath, TimeSpan startTime, string filterArgs)
        {
            try
            {
                // Make sure the directory exists
                var directory = Path.GetDirectoryName(thumbnailPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Create a thumbnail with the same aspect ratio
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        // Extract thumbnail with same aspect ratio filter
                        Arguments = $"-y -ss {startTime.TotalSeconds} -i \"{videoPath}\" {filterArgs} -frames:v 1 -q:v 2 \"{thumbnailPath}\"",
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
                        _logger.LogError("FFmpeg thumbnail generation failed: {Error}", error);
                    }
                }
            }
            catch (Win32Exception ex) when (ex.Message.Contains("The system cannot find the file specified"))
            {
                _logger.LogError("Could not create thumbnail: FFmpeg not found");
            }
        }

        private async Task<(string VideoPath, string FolderName)> DownloadYouTubeVideoAsync(string youtubeUrl)
        {
            var videoId = VideoId.Parse(youtubeUrl);
            var folderName = $"{videoId.Value}_{DateTime.Now:yyyyMMdd_HHmmss}";
            var tempFolderPath = Path.Combine(_tempDir, folderName);
            
            // Ensure directory exists
            if (!Directory.Exists(tempFolderPath))
            {
                Directory.CreateDirectory(tempFolderPath);
            }
            
            var outputPath = Path.Combine(tempFolderPath, $"{videoId}.mp4");
            
            _logger.LogInformation("Downloading video from YouTube: {VideoId} to folder {Folder}", videoId, folderName);
            
            try
            {
                // Clean up any previous failed attempts
                if (File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete existing file: {Path}", outputPath);
                    }
                }
                
                // Download with YoutubeExplode
                _logger.LogInformation("Downloading with YoutubeExplode client");
                
                // Get video info with retry logic
                var retryCount = 5;
                Exception lastException = null;
                
                while (retryCount > 0)
                {
                    try
                    {
                        // Progress callback
                        var progress = new Progress<double>(p => 
                        {
                            if (Math.Floor(p * 100) % 10 == 0) // Log every 10%
                            {
                                _logger.LogInformation("Download progress: {Progress}%", Math.Floor(p * 100));
                            }
                        });
                        
                        // Download the video with the converter
                        await _youtubeClient.Videos.DownloadAsync(
                            videoId.Value,  // Use the string value of the video ID
                            outputPath,     // Target file path
                            o => o.SetContainer("mp4") // Set container format
                                 .SetPreset(ConversionPreset.UltraFast), // Fast conversion
                            progress        // Progress tracking
                        );
                        
                        break; // If successful, exit the retry loop
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.LogWarning(ex, "Download attempt failed, retries left: {Count}", retryCount - 1);
                        retryCount--;
                        
                        if (retryCount > 0)
                        {
                            await Task.Delay(3000); // Wait 3 seconds before retrying
                        }
                    }
                }
                
                // Check if all retries failed
                if (retryCount == 0 && lastException != null)
                {
                    throw new InvalidOperationException("All download attempts failed", lastException);
                }
                
                if (!File.Exists(outputPath))
                {
                    throw new FileNotFoundException($"Download completed but file not found at {outputPath}");
                }
                
                _logger.LogInformation("Successfully downloaded video with YoutubeExplode to folder {Folder}", folderName);
                return (outputPath, folderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading YouTube video: {Message}", ex.Message);
                throw new InvalidOperationException($"Failed to download video: {ex.Message}", ex);
            }
        }

        private string GetAspectRatioFilterArgs(AspectRatio aspectRatio)
        {
            // Define filter arguments for different aspect ratios
            return aspectRatio switch
            {
                AspectRatio.Landscape => "-vf \"scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2\"", // 16:9
                AspectRatio.Portrait => "-vf \"scale=1080:1920:force_original_aspect_ratio=decrease,pad=1080:1920:(ow-iw)/2:(oh-ih)/2\"",  // 9:16
                AspectRatio.Square => "-vf \"scale=1080:1080:force_original_aspect_ratio=decrease,pad=1080:1080:(ow-iw)/2:(oh-ih)/2\"",    // 1:1
                _ => "-vf \"scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2\""  // Default to landscape
            };
        }

        // Clean up old preview files
        public void CleanupOldPreviews()
        {
            try
            {
                var previewsDir = Path.Combine(Path.GetTempPath(), "YShorts", "previews");
                if (Directory.Exists(previewsDir))
                {
                    var oldFiles = Directory.GetFiles(previewsDir)
                        .Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddHours(-1))
                        .ToList();
                    
                    foreach (var file in oldFiles)
                    {
                        try
                        {
                            File.Delete(file);
                            _logger.LogInformation("Deleted old preview file: {File}", file);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete old preview file: {File}", file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old preview files");
            }
        }

        /// <summary>
        /// Creates a clip from an existing video file using the specified start and end times
        /// </summary>
        public async Task<ClipResult> CreateClipAsync(string videoPath, double startTime, double endTime, string title)
        {
            try
            {
                _logger.LogInformation("Creating clip from video: {VideoPath} from {Start} to {End}", 
                    videoPath, startTime, endTime);
                
                // Create a unique folder for this clip to avoid conflicts
                var folderName = Path.GetFileNameWithoutExtension(videoPath) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var clipFolder = Path.Combine(_outputDir, folderName);
                
                // Ensure the folder exists
                if (!Directory.Exists(clipFolder))
                {
                    Directory.CreateDirectory(clipFolder);
                }
                
                // Generate a unique filename for the clip
                var clipFileName = $"clip_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                var outputPath = Path.Combine(clipFolder, clipFileName);
                
                // Calculate duration
                var duration = endTime - startTime;
                var timespan = TimeSpan.FromSeconds(duration);
                var durationFormatted = $"{timespan.Minutes:D2}:{timespan.Seconds:D2}";
                
                // Create the clip using FFmpeg
                await GenerateClipAsync(videoPath, outputPath, TimeSpan.FromSeconds(startTime), TimeSpan.FromSeconds(duration), GetAspectRatioFilterArgs(AspectRatio.Landscape));
                
                // Generate a thumbnail
                var thumbnailFileName = Path.GetFileNameWithoutExtension(clipFileName) + "_thumb.jpg";
                var thumbnailPath = Path.Combine(clipFolder, thumbnailFileName);
                await GenerateThumbnailAsync(videoPath, thumbnailPath, TimeSpan.FromSeconds(startTime), GetAspectRatioFilterArgs(AspectRatio.Landscape));
                
                // Get relative paths for URLs
                var relativePath = Path.GetRelativePath(_webHostEnvironment.WebRootPath, clipFolder);
                var publicPath = "/" + relativePath.Replace("\\", "/");
                
                return new ClipResult
                {
                    Success = true,
                    FilePath = outputPath,
                    FileName = clipFileName,
                    PreviewUrl = $"{publicPath}/{clipFileName}",
                    DownloadUrl = $"/api/shorts/download/{folderName}/{clipFileName}",
                    ThumbnailUrl = $"{publicPath}/{thumbnailFileName}",
                    Title = string.IsNullOrEmpty(title) ? $"Clip {DateTime.Now:yyyy-MM-dd HH:mm:ss}" : title,
                    Duration = durationFormatted,
                    FolderName = folderName,
                    FolderPath = publicPath
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create clip: {Message}", ex.Message);
                return new ClipResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to create clip: {ex.Message}"
                };
            }
        }
        
        // This is a separate method to make it clearer what's happening
        private async Task GenerateClipAsync(string videoPath, string outputPath, TimeSpan startTime, TimeSpan duration, string filterArgs)
        {
            // Use the same logic as GenerateShortClipAsync
            await GenerateShortClipAsync(videoPath, outputPath, startTime, duration, filterArgs);
        }
    }
} 