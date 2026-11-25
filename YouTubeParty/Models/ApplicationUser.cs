using Microsoft.AspNetCore.Identity;

namespace YouTubeParty.Models;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? DisplayName { get; set; }

    public ICollection<RoomMember> RoomMemberships { get; set; } = new List<RoomMember>();
}
