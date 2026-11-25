using System.ComponentModel.DataAnnotations;

namespace YouTubeParty.DTOs;

public record UpdateDisplayNameRequest(
    [Required, MinLength(1), MaxLength(20)] string DisplayName
);
