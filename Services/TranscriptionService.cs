using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;
using YShorts.Models;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YShorts.Services
{
    public class TranscriptionService
    {
        private readonly ILogger<TranscriptionService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _assemblyApiKey;
        private readonly string _tempDir;

        public TranscriptionService(ILogger<TranscriptionService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _assemblyApiKey = configuration["AssemblyAI:ApiKey"] ?? "af7c531f7d254d27afb91b1b25f400de";
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_assemblyApiKey);
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
            var youtube = new YoutubeClient();
            var videoId = VideoId.Parse(youtubeUrl);
            var outputPath = Path.Combine(_tempDir, $"{videoId}.mp3");
            
            _logger.LogInformation("Downloading audio from YouTube video {VideoId}", videoId);
            
            // Get the stream manifest
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoId);
            
            // Get the audio with highest bitrate using LINQ
            var audioStreamInfo = streamManifest.GetAudioOnlyStreams()
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

            if (audioStreamInfo == null)
            {
                throw new InvalidOperationException("No audio streams found for the video");
            }
            
            // Download the audio
            await youtube.Videos.Streams.DownloadAsync(audioStreamInfo, outputPath);
            
            return outputPath;
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