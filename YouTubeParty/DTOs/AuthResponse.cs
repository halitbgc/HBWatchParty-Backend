namespace YouTubeParty.DTOs;

public record AuthResponse(
    string Token,
    DateTime ExpiresAtUtc,
    string Username,
    string UserId,
    string DisplayName
);
