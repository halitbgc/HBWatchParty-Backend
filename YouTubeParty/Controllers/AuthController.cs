using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YouTubeParty.DTOs;
using YouTubeParty.Models;
using YouTubeParty.Services;

namespace YouTubeParty.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var defaultDisplayName = request.Username.Length <= 20 ? request.Username : request.Username[..20];
        var user = new ApplicationUser { UserName = request.Username, DisplayName = defaultDisplayName };
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(e => e.Description));
        }

        var (token, expiresAtUtc) = _tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, expiresAtUtc, user.UserName!, user.Id, user.GetEffectiveDisplayName()));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.Username);
        if (user is null)
        {
            return Unauthorized("Kullanıcı adı veya şifre hatalı.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return Unauthorized("Kullanıcı adı veya şifre hatalı.");
        }

        var (token, expiresAtUtc) = _tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, expiresAtUtc, user.UserName!, user.Id, user.GetEffectiveDisplayName()));
    }
}
