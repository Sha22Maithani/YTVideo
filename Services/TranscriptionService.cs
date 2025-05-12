using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;
using YShorts.Models;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using System.Net;
using System.Security.Authentication;
using System.Diagnostics;

namespace YShorts.Services
{
    public class TranscriptionService
    {
        private readonly ILogger<TranscriptionService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _assemblyApiKey;
        private readonly string _tempDir;
        private readonly YoutubeClient _youtubeClient;

        public TranscriptionService(ILogger<TranscriptionService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _assemblyApiKey = configuration["AssemblyAI:ApiKey"] ?? "af7c531f7d254d27afb91b1b25f400de";
            
            // Configure HttpClient with proper TLS settings
            var handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) => true // Allow all certificates for troubleshooting
            };
            
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_assemblyApiKey);
            
            // Configure YoutubeClient with the same handler
            _youtubeClient = new YoutubeClient(new HttpClient(handler));
            
            _tempDir = Path.Combine(Path.GetTempPath(), "YShorts");
            
            if (!Directory.Exists(_tempDir))
            {
                Directory.CreateDirectory(_tempDir);
            }
        }

        public async Task<TranscriptionResponse> TranscribeYouTubeVideoAsync(string youtubeUrl)
        {
            try
            {
                // 1. Extract audio from YouTube video
                var audioPath = await ExtractAudioFromYouTubeAsync(youtubeUrl);
                
                // 2. Upload audio to temporary storage
                var audioUrl = await UploadAudioFileAsync(audioPath);
                
                // 3. Submit transcription request to AssemblyAI
                var transcriptId = await StartTranscriptionJobAsync(audioUrl);
                
                // 4. Poll for transcription results
                var transcription = await PollForTranscriptionResultAsync(transcriptId);
                
                // 5. Clean up temporary file
                File.Delete(audioPath);
                
                return transcription;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to transcribe YouTube video: {Message}", ex.Message);
                return new TranscriptionResponse
                {
                    Success = false,
                    ErrorMessage = $"Transcription failed: {ex.Message}"
                };
            }
        }

        private async Task<string> ExtractAudioFromYouTubeAsync(string youtubeUrl)
        {
            // Direct FFmpeg approach
            var videoId = YoutubeExplode.Videos.VideoId.Parse(youtubeUrl);
            var outputPath = Path.Combine(_tempDir, $"{videoId}.mp3");
            
            _logger.LogInformation("Downloading audio from YouTube video {VideoId}", videoId);
            
            try
            {
                // First try using YoutubeExplode with our configured client
                try
                {
                    _logger.LogInformation("Attempting to download with YoutubeExplode client");
                    
                    // Get stream info
                    var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
                    
                    // Get audio-only stream with highest bitrate
                    var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    
                    if (audioStreamInfo != null)
                    {
                        // Download the audio
                        _logger.LogInformation("Downloading audio stream with bitrate: {Bitrate}", audioStreamInfo.Bitrate);
                        await _youtubeClient.Videos.Streams.DownloadAsync(audioStreamInfo, outputPath);
                        
                        if (File.Exists(outputPath))
                        {
                            _logger.LogInformation("Successfully downloaded audio with YoutubeExplode");
                            return outputPath;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No suitable audio stream found, falling back to alternative methods");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "YoutubeExplode download failed, falling back to alternative methods: {Message}", ex.Message);
                }
                
                // Use yt-dlp (better compatibility than youtube-dl) with FFmpeg as downloader
                using (var process = new Process())
                {
                    string ytDlpCommand = "yt-dlp";
                    
                    // Check if yt-dlp is installed, if not, try youtube-dl
                    try
                    {
                        using (var checkProcess = new Process())
                        {
                            checkProcess.StartInfo = new ProcessStartInfo
                            {
                                FileName = ytDlpCommand,
                                Arguments = "--version",
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            checkProcess.Start();
                            await checkProcess.WaitForExitAsync();
                        }
                    }
                    catch
                    {
                        // If yt-dlp isn't available, fallback to youtube-dl
                        ytDlpCommand = "youtube-dl";
                        _logger.LogInformation("yt-dlp not found, falling back to youtube-dl");
                    }
                    
                    // Command to download best audio format and convert to mp3
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = ytDlpCommand,
                        // Format options:
                        // -x: extract audio
                        // --audio-format mp3: convert to mp3
                        // --audio-quality 0: best quality
                        // -o: output file pattern
                        Arguments = $"-x --audio-format mp3 --audio-quality 0 -o \"{outputPath.Replace(".mp3", "")}\" \"{youtubeUrl}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    process.Start();
                    
                    // Log output and error asynchronously
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    
                    await process.WaitForExitAsync();
                    
                    string output = await outputTask;
                    string error = await errorTask;
                    
                    if (process.ExitCode != 0)
                    {
                        _logger.LogError("Error extracting audio with {Command}: {Error}", ytDlpCommand, error);
                        
                        // Try direct FFmpeg approach as fallback
                        _logger.LogInformation("Trying direct FFmpeg fallback for {VideoId}", videoId);
                        
                        await DownloadWithFfmpegAsync(youtubeUrl, outputPath);
                    }
                    else
                    {
                        _logger.LogInformation("Successfully extracted audio with {Command}: {Output}", ytDlpCommand, output);
                    }
                }
                
                if (!File.Exists(outputPath))
                {
                    // Check if there's a file with the base name but different extension
                    string directory = Path.GetDirectoryName(outputPath) ?? _tempDir;
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(outputPath);
                    string[] matchingFiles = Directory.GetFiles(directory, fileNameWithoutExt + ".*");
                    
                    if (matchingFiles.Length > 0)
                    {
                        // Use the first matching file
                        string foundFile = matchingFiles[0];
                        
                        // If it's not an mp3, convert it
                        if (Path.GetExtension(foundFile).ToLower() != ".mp3")
                        {
                            _logger.LogInformation("Converting {File} to MP3 format", foundFile);
                            await ConvertToMp3Async(foundFile, outputPath);
                        }
                        else
                        {
                            // Just copy/rename the file
                            File.Copy(foundFile, outputPath, true);
                        }
                    }
                    else
                    {
                        throw new FileNotFoundException($"Could not find extracted audio file at {outputPath}");
                    }
                }
                
                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting audio from YouTube: {Message}", ex.Message);
                throw new InvalidOperationException($"Failed to extract audio: {ex.Message}", ex);
            }
        }
        
        private async Task DownloadWithFfmpegAsync(string youtubeUrl, string outputPath)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    // -i: input URL
                    // -vn: disable video
                    // -ab: audio bitrate
                    // -y: overwrite output
                    Arguments = $"-y -i \"{youtubeUrl}\" -vn -ab 128k \"{outputPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                process.Start();
                
                var errorTask = process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();
                
                string error = await errorTask;
                
                if (process.ExitCode != 0)
                {
                    _logger.LogError("FFmpeg direct download failed: {Error}", error);
                    throw new InvalidOperationException($"FFmpeg direct download failed with exit code {process.ExitCode}");
                }
                
                _logger.LogInformation("Successfully downloaded with FFmpeg direct approach");
            }
        }
        
        private async Task ConvertToMp3Async(string inputFile, string outputPath)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -i \"{inputFile}\" -vn -ab 128k \"{outputPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                process.Start();
                
                var errorTask = process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();
                
                string error = await errorTask;
                
                if (process.ExitCode != 0)
                {
                    _logger.LogError("FFmpeg conversion failed: {Error}", error);
                    throw new InvalidOperationException($"FFmpeg conversion failed with exit code {process.ExitCode}");
                }
                
                _logger.LogInformation("Successfully converted to MP3 format");
                
                // Delete the original file
                if (File.Exists(inputFile))
                {
                    File.Delete(inputFile);
                }
            }
        }

        // Helper method to retry operations
        private async Task<T> RetryAsync<T>(Func<Task<T>> operation, int maxRetries, int delayMs = 2000)
        {
            Exception lastException = null;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning("Attempt {Attempt} of {MaxRetries} failed: {Message}", attempt, maxRetries, ex.Message);
                    
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delayMs * attempt); // Exponential backoff
                    }
                }
            }
            
            throw new InvalidOperationException($"Operation failed after {maxRetries} attempts", lastException);
        }

        private async Task<string> UploadAudioFileAsync(string audioFilePath)
        {
            _logger.LogInformation("Uploading audio file to AssemblyAI");
            
            using var fileStream = File.OpenRead(audioFilePath);
            using var fileContent = new StreamContent(fileStream);
            
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
            
            var response = await _httpClient.PostAsync("https://api.assemblyai.com/v2/upload", fileContent);
            response.EnsureSuccessStatusCode();
            
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var uploadResponse = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
            
            return uploadResponse.GetProperty("upload_url").GetString() ?? 
                   throw new InvalidOperationException("Failed to get upload URL from AssemblyAI");
        }

        private async Task<string> StartTranscriptionJobAsync(string audioUrl)
        {
            _logger.LogInformation("Submitting transcription request to AssemblyAI");
            
            var request = new AssemblyAiTranscriptRequest
            {
                AudioUrl = audioUrl
            };
            
            var jsonRequest = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("https://api.assemblyai.com/v2/transcript", content);
            response.EnsureSuccessStatusCode();
            
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var transcriptResponse = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
            
            return transcriptResponse.GetProperty("id").GetString() ?? 
                   throw new InvalidOperationException("Failed to get transcript ID from AssemblyAI");
        }

        private async Task<TranscriptionResponse> PollForTranscriptionResultAsync(string transcriptId)
        {
            _logger.LogInformation("Polling for transcription results (ID: {TranscriptId})", transcriptId);
            
            var endpoint = $"https://api.assemblyai.com/v2/transcript/{transcriptId}";
            
            while (true)
            {
                var response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();
                
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var transcriptResponse = JsonSerializer.Deserialize<AssemblyAiTranscriptResponse>(jsonResponse);
                
                switch (transcriptResponse?.Status)
                {
                    case "completed":
                        return new TranscriptionResponse
                        {
                            Success = true,
                            Text = transcriptResponse.Text ?? string.Empty
                        };
                    case "error":
                        return new TranscriptionResponse
                        {
                            Success = false,
                            ErrorMessage = transcriptResponse.Error ?? "Unknown error occurred"
                        };
                    case "processing":
                    case "queued":
                        await Task.Delay(3000); // Wait 3 seconds before polling again
                        break;
                    default:
                        return new TranscriptionResponse
                        {
                            Success = false,
                            ErrorMessage = $"Unknown status: {transcriptResponse?.Status}"
                        };
                }
            }
        }
    }
} 