using System.ComponentModel.DataAnnotations;

namespace YouTubeParty.DTOs;

public record RegisterRequest(
    [Required, MinLength(3), MaxLength(64)] string Username,
    [Required, MinLength(8)] string Password
);
