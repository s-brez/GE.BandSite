using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace GE.BandSite.Server;

[ApiController]
public class ApiController : ControllerBase
{

    [FromServices]
    public required IConfiguration Configuration { get; set; }


    public class DemoEndpointParameters
    {
        [JsonRequired]
        [JsonProperty("contact_email")]
        public string Email { get; set; } = null!;

        [JsonRequired]
        [JsonProperty("contact_name")]
        public object Name { get; set; } = null!;

    }

    [HttpPost]
    [Route("/api/DemoRoute")]
    public IActionResult DemoEndpoint([FromBody] DemoEndpointParameters messageRequest, CancellationToken cancellationToken)
    {
        return Ok();
    }
}