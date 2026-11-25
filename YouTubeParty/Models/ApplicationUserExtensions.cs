namespace YouTubeParty.Models;

public static class ApplicationUserExtensions
{
    public static string GetEffectiveDisplayName(this ApplicationUser user)
        => string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName ?? string.Empty : user.DisplayName;
}
