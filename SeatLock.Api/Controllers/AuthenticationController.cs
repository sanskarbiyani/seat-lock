using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatLock.Api.DTOs.Authentication;
using SeatLock.Api.Interfaces.Authentication;

namespace SeatLock.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthenticationController(ITokenService tokenService)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("token")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<TokenResponse> CreateToken(
        [FromBody] TokenRequest request)
    {
        if (request.UserId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid user ID",
                Detail = "UserId must not be empty."
            });
        }

        return Ok(tokenService.CreateToken(request.UserId));
    }
}
