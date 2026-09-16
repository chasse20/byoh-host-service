using Microsoft.AspNetCore.Mvc;

namespace Host.Service.Controllers
{
	[ApiController]
	[Route( "[controller]" )]
	public class StatusController : ControllerBase
	{
		[HttpGet]
		public IActionResult Get()
		{
			return NoContent();
		}
	}
}
