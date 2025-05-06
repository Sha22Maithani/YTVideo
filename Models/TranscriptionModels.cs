using System.Text.Json.Serialization;

namespace YShorts.Models
{
    public class VideoUrlRequest
    {
        public string YoutubeUrl { get; set; } = string.Empty;
    }

    public class TranscriptionResponse
    {
        public string Text { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // AssemblyAI API Models
    public class AssemblyAiTranscriptRequest
    {
        [JsonPropertyName("audio_url")]
        public string AudioUrl { get; set; } = string.Empty;
    }

    public class AssemblyAiTranscriptResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
} 