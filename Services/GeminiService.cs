using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using YShorts.Models;

namespace YShorts.Services
{
    public class GeminiService
    {
        private readonly ILogger<GeminiService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _geminiApiKey;
        private readonly string _geminiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

        public GeminiService(ILogger<GeminiService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _geminiApiKey = configuration["Google:ApiKey"] ?? "AIzaSyCGMnW4ZC7CCVL1K4HfVlv0kxHbxg0AB-Y";
            _httpClient = new HttpClient();
        }

        public async Task<BestMomentsResponse> ExtractBestMomentsAsync(string transcriptText)
        {
            try
            {
                _logger.LogInformation("Extracting best moments from transcript using Gemini API");

                var requestUrl = $"{_geminiEndpoint}?key={_geminiApiKey}";
                var prompt = CreateGeminiPrompt(transcriptText);
                var request = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = prompt
                                }
                            }
                        }
                    }
                };

                var jsonRequest = JsonSerializer.Serialize(request);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(requestUrl, content);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                return ParseGeminiResponse(jsonResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract best moments: {Message}", ex.Message);
                return new BestMomentsResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to extract best moments: {ex.Message}"
                };
            }
        }

        private string CreateGeminiPrompt(string transcriptText)
        {
            return @$"
I have a transcript from a video. Please identify the 5-8 best moments or highlights from this transcript.

For each moment, provide:
1. The content/quote of the moment
2. A start timestamp (in format MM:SS)
3. An end timestamp (in format MM:SS)
4. A brief reason why this is a standout moment (compelling, funny, insightful, etc.)

Format your response as a JSON array of objects with properties 'content', 'startTimestamp', 'endTimestamp', and 'reason'. Do not add any commentary before or after the JSON.

Here is the transcript:
{transcriptText}
";
        }

        private BestMomentsResponse ParseGeminiResponse(string jsonResponse)
        {
            try
            {
                var response = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                
                if (!response.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                {
                    return new BestMomentsResponse
                    {
                        Success = false,
                        ErrorMessage = "No response candidates found in Gemini API response"
                    };
                }

                var content = candidates[0].GetProperty("content");
                var parts = content.GetProperty("parts");
                var text = parts[0].GetProperty("text").GetString() ?? string.Empty;

                // Extract JSON from text (Gemini might wrap the JSON in markdown code blocks)
                var jsonString = ExtractJsonFromText(text);
                
                if (string.IsNullOrEmpty(jsonString))
                {
                    return new BestMomentsResponse
                    {
                        Success = false,
                        ErrorMessage = "Could not extract valid JSON from Gemini response"
                    };
                }

				var options = new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				};
				var moments = JsonSerializer.Deserialize<List<BestMoment>>(jsonString, options);


				//var moments = JsonSerializer.Deserialize<List<BestMoment>>(jsonString);
                
                return new BestMomentsResponse
                {
                    Success = true,
                    Moments = moments ?? new List<BestMoment>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini response: {Message}", ex.Message);
                return new BestMomentsResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to parse Gemini response: {ex.Message}"
                };
            }
        }

        private string ExtractJsonFromText(string text)
        {
            // Try to extract JSON from markdown code blocks if present
            if (text.Contains("```json"))
            {
                var start = text.IndexOf("```json") + 7;
                var end = text.IndexOf("```", start);
                if (end > start)
                {
                    return text.Substring(start, end - start).Trim();
                }
            }
            
            // If no code blocks, try to find JSON array directly
            var jsonStart = text.IndexOf('[');
            var jsonEnd = text.LastIndexOf(']');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                return text.Substring(jsonStart, jsonEnd - jsonStart + 1).Trim();
            }
            
            return text.Trim(); // Return the whole text as a fallback
        }
    }
} 