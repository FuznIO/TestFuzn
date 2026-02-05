using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers.Api;

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
