namespace YouTubeParty.Services;

public interface IRoomCodeGenerator
{
    Task<string> GenerateUniqueCodeAsync();
}
