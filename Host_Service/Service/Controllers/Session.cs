using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Host.Service.Controllers
{
	[ApiController]
	[Route( "[controller]" )]
	public class SessionController : ControllerBase
	{
		private readonly Manager _manager;

		public SessionController( Manager tManager )
		{
			_manager = tManager;
		}

		[HttpPost( "{localPort}/connect" )]
		public async Task<IActionResult> Connect( int localPort, SessionConnectData data )
		{
			if ( await _manager.OnSessionConnectAsync( localPort, data ) )
			{
				return NoContent();
			}

			return NotFound();
		}

		[HttpPost( "{localPort}/disconnect" )]
		public async Task<IActionResult> Disconnect( int localPort )
		{
			if ( await _manager.OnSessionDisconnectAsync( localPort ) )
			{
				return NoContent();
			}

			return NotFound();
		}
	}
}
