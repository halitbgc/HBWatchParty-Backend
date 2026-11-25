using System.ComponentModel.DataAnnotations;

namespace YouTubeParty.DTOs;

public record LoginRequest(
    [Required] string Username,
    [Required] string Password
);
