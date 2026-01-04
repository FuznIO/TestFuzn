using Microsoft.AspNetCore.Mvc;

namespace SampleApp.WebApp.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class PingController : ControllerBase
{
    public PingController()
    {
    }

    [HttpGet]
    public ActionResult<string> Get()
    {
        return Ok("Pong");
    }
}
