using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using YouTubeParty.Data;

namespace YouTubeParty.Services;

public class RoomCodeGenerator : IRoomCodeGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int CodeLength = 6;
    private const int MaxAttempts = 10;

    private readonly ApplicationDbContext _dbContext;

    public RoomCodeGenerator(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateUniqueCodeAsync()
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var code = GenerateCode();
            var exists = await _dbContext.Rooms.AnyAsync(r => r.RoomCode == code);
            if (!exists)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Benzersiz oda kodu üretilemedi, lütfen tekrar deneyin.");
    }

    private static string GenerateCode()
    {
        Span<char> chars = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            var index = RandomNumberGenerator.GetInt32(Alphabet.Length);
            chars[i] = Alphabet[index];
        }

        return new string(chars);
    }
}
