using YouTubeParty.Models;

namespace YouTubeParty.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(ApplicationUser user);
}
