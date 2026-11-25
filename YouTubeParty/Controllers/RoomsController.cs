using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YouTubeParty.Data;
using YouTubeParty.DTOs;
using YouTubeParty.Models;
using YouTubeParty.Services;

namespace YouTubeParty.Controllers;

[ApiController]
[Authorize]
[Route("api/rooms")]
public partial class RoomsController : ControllerBase
{
    private const int MaxCreateAttempts = 5;

    private readonly ApplicationDbContext _dbContext;
    private readonly IRoomCodeGenerator _roomCodeGenerator;

    public RoomsController(ApplicationDbContext dbContext, IRoomCodeGenerator roomCodeGenerator)
    {
        _dbContext = dbContext;
        _roomCodeGenerator = roomCodeGenerator;
    }

    [HttpPost]
    public async Task<ActionResult<CreateRoomResponse>> CreateRoom()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        for (var attempt = 0; attempt < MaxCreateAttempts; attempt++)
        {
            var code = await _roomCodeGenerator.GenerateUniqueCodeAsync();
            var room = new Room { RoomCode = code, CreatedByUserId = userId };
            _dbContext.Rooms.Add(room);

            try
            {
                await _dbContext.SaveChangesAsync();
                return Ok(new CreateRoomResponse(room.RoomCode, room.CreatedAtUtc));
            }
            catch (DbUpdateException)
            {
                _dbContext.Entry(room).State = EntityState.Detached;
            }
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Oda kodu üretilemedi, lütfen tekrar deneyin.");
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<RoomInfoResponse>> GetRoom(string code)
    {
        var normalizedCode = (code ?? string.Empty).Trim().ToUpperInvariant();

        if (!RoomCodeFormat().IsMatch(normalizedCode))
        {
            return BadRequest("Geçersiz oda kodu formatı.");
        }

        var room = await _dbContext.Rooms
            .Include(r => r.CreatedByUser)
            .FirstOrDefaultAsync(r => r.RoomCode == normalizedCode);

        if (room is null)
        {
            return NotFound();
        }

        var createdByDisplayName = room.CreatedByUser?.GetEffectiveDisplayName();

        return Ok(new RoomInfoResponse(room.RoomCode, room.CreatedAtUtc, createdByDisplayName));
    }

    [GeneratedRegex("^[A-Z0-9]{6}$")]
    private static partial Regex RoomCodeFormat();
}
