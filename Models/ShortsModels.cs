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

    public class GenerateSelectedShortsRequest
    {
        public string SourceVideoPath { get; set; } = string.Empty;
        public List<ShortClipGenerationRequest> SelectedShorts { get; set; } = new List<ShortClipGenerationRequest>();
    }

    public class ShortClipGenerationRequest : ShortClip
    {
        // These properties are needed for generation since AdditionalProperties is marked with [JsonIgnore]
        public double StartTimeSeconds { get; set; }
        public double EndTimeSeconds { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class ShortsGenerationResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<ShortClip> Shorts { get; set; } = new List<ShortClip>();
        public string? OutputDirectory { get; set; }
        public string? SourceVideoPath { get; set; } // Path to the source video for later processing
        public string? FolderName { get; set; } // Name of the folder where all content is stored
        public string? FolderPath { get; set; } // Web-accessible path to the folder
    }

    public class ShortClip
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string PreviewUrl { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string AspectRatio { get; set; } = "Landscape"; // Default to landscape
        
        // Additional properties for storing metadata needed for video generation
        [JsonIgnore] // Don't serialize this to the client
        public Dictionary<string, object>? AdditionalProperties { get; set; }
        
        // Flag to indicate if the video has been generated
        public bool IsGenerated { get; set; }
    }

    public class CreateClipRequest
    {
        public string VideoPath { get; set; } = string.Empty;
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    public class ClipResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public string? PreviewUrl { get; set; }
        public string? DownloadUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? Title { get; set; }
        public string? Duration { get; set; }
        public string? FolderName { get; set; } // Name of the folder containing the clip
        public string? FolderPath { get; set; } // Web-accessible path to the folder
    }
} 