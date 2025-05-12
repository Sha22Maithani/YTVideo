using System.Collections.Generic;

namespace YShorts.Models
{
    public class CreateShortsWithAspectRequest
    {
        public required string YoutubeUrl { get; set; }
        public required List<BestMoment> BestMoments { get; set; }
        public AspectRatio AspectRatio { get; set; } = AspectRatio.Landscape; // Default to landscape
    }
} 