namespace YouTubeParty.Models;

public class Room
{
    public int Id { get; set; }

    public string RoomCode { get; set; } = string.Empty;

    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<RoomMember> Members { get; set; } = new List<RoomMember>();
}
