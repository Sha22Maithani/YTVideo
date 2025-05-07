using System.Text.Json.Serialization;

namespace YShorts.Models
{
    public class ShortsGenerationRequest
    {
        public string YoutubeUrl { get; set; } = string.Empty;
        public int Count { get; set; } = 3;
        public int Duration { get; set; } = 30;
    }

    public class CreateShortsRequest
    {
        public string YoutubeUrl { get; set; } = string.Empty;
        public List<BestMoment> BestMoments { get; set; } = new List<BestMoment>();
    }

    public class ShortsGenerationResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<ShortClip> Shorts { get; set; } = new List<ShortClip>();
    }

    public class ShortClip
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }
} 