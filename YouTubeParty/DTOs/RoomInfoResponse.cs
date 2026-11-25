namespace YouTubeParty.DTOs;

public record RoomInfoResponse(string RoomCode, DateTime CreatedAtUtc, string? CreatedByUsername);
