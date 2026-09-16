using Host.Service;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Host.Azure.Service.Signal
{
	public class Signal
	{
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly IOptions<Settings> _settings;
		protected readonly JsonSerializerOptions _JSONOptions = new( JsonSerializerDefaults.Web );

		public Signal( IHttpClientFactory tHttpClientFactory, IOptions<Settings> tSettings )
		{
			_httpClientFactory = tHttpClientFactory;
			_settings = tSettings;
		}

		public async Task ConnectUserAsync( int tSessionLocalPort, string tUser, CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested )
			{
				// Params
				string tempURL = string.Format( _settings.Value.SignalSessionConnectQuery, tSessionLocalPort );
				HttpRequestMessage tempRequest = new( HttpMethod.Post, tempURL )
				{
					Content = new StringContent( JsonSerializer.Serialize( new { user = tUser }, _JSONOptions ), Encoding.UTF8, "application/json" )
				};

				// Send
				HttpClient tempClient = _httpClientFactory.CreateClient();
				await tempClient.SendAsync( tempRequest, tCancel );
			}
		}

		public async Task<StatusData> GetStatus( int tSessionLocalPort, CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested )
			{
				// Params
				string tempURL = string.Format( _settings.Value.SignalStatusQuery, tSessionLocalPort );
				HttpRequestMessage tempRequest = new( HttpMethod.Get, tempURL );

				// Send
				HttpClient tempClient = _httpClientFactory.CreateClient();
				HttpResponseMessage tempResponse = await tempClient.SendAsync( tempRequest, tCancel );

				// Process
				if ( tempResponse.IsSuccessStatusCode )
				{
					return JsonSerializer.Deserialize<StatusData>( await tempResponse.Content.ReadAsStringAsync( tCancel ), _JSONOptions );
				}
			}

			return null;
		}
	}
}
