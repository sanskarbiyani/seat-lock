using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SeatLock.Api.Controllers
{
    [Route("api/shows/{showId: guid}")]
    [ApiController]
    public class ReservationController : ControllerBase
    {
    }
}
