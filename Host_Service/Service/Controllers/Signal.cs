using Microsoft.AspNetCore.Mvc;

namespace Host.Service.Controllers
{
	[ApiController]
	[Route( "[controller]" )]
	public class SignalController : ControllerBase
	{
		private readonly Manager _manager;

		public SignalController( Manager tManager )
		{
			_manager = tManager;
		}

		[HttpPost( "{localPort}/connect" )]
		public IActionResult Connect( int localPort )
		{
			if ( _manager.OnSignalConnect( localPort ) )
			{
				return NoContent();
			}

			return NotFound();
		}
	}
}
