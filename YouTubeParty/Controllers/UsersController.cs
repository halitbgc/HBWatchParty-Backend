using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using YouTubeParty.DTOs;
using YouTubeParty.Hubs;
using YouTubeParty.Models;

namespace YouTubeParty.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<WatchPartyHub> _watchPartyHubContext;
    private readonly IHubContext<VoiceSignalingHub> _voiceHubContext;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        IHubContext<WatchPartyHub> watchPartyHubContext,
        IHubContext<VoiceSignalingHub> voiceHubContext)
    {
        _userManager = userManager;
        _watchPartyHubContext = watchPartyHubContext;
        _voiceHubContext = voiceHubContext;
    }

    [HttpPut("display-name")]
    public async Task<ActionResult<UpdateDisplayNameResponse>> UpdateDisplayName(UpdateDisplayNameRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);
        if (user is null)
        {
            return Unauthorized();
        }

        var trimmed = request.DisplayName.Trim();
        if (trimmed.Length == 0)
        {
            return BadRequest("Görünen ad boş olamaz.");
        }

        user.DisplayName = trimmed;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(e => e.Description));
        }

        foreach (var roomId in WatchPartyHub.UpdateDisplayNameForUser(user.Id, trimmed))
        {
            await _watchPartyHubContext.Clients.Group(roomId).SendAsync("ReceiveDisplayNameChanged", user.Id, trimmed);
        }

        foreach (var roomId in VoiceSignalingHub.UpdateDisplayNameForUser(user.Id, trimmed))
        {
            await _voiceHubContext.Clients.Group(roomId).SendAsync("VoicePeerDisplayNameChanged", user.Id, trimmed);
        }

        return Ok(new UpdateDisplayNameResponse(user.Id, trimmed));
    }
}
