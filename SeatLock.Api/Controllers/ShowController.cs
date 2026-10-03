using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SeatLock.Api.DTOs.Shows;
using SeatLock.Api.Interfaces.Services;

namespace SeatLock.Api.Controllers
{
    [Route("api/shows")]
    [ApiController]
    public class ShowController : ControllerBase
    {
        private readonly IShowService _showService;
        public ShowController(IShowService showService)
        {
            _showService = showService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateShow([FromBody] CreateShowRequest request)
        {
            try
            {
                var response = await _showService.CreateShowAsync(request);
                if(response.Seats.Count == 0)
                {
                    return StatusCode(500, new { error = "Failed to create seats for the show." });
                }
                return CreatedAtAction(nameof(CreateShow), new { id = response.ShowId }, response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{showId:guid}")]
        public async Task<IActionResult> GetShow([FromRoute] Guid showId)
        {
            // Implementation for fetching a show by ID
            return NotFound();
        }
    }
}
