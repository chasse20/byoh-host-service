using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Host.Service.Azure.Scaling
{
	public class Scaling
	{
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly IOptions<Settings> _settings;
		protected readonly JsonSerializerOptions _JSONOptions = new( JsonSerializerDefaults.Web );
		private readonly string _code;

		public Scaling( IHttpClientFactory tHttpClientFactory, IConfiguration tConfig, IOptions<Settings> tSettings )
		{
			_httpClientFactory = tHttpClientFactory;
			_settings = tSettings;
			_code = tConfig.GetValue<string>( _settings.Value.KeyStoreScalingServiceCode );
		}

		public async Task<HostData> ConnectHostAsync( string tPrivateIP, string tLatestVersion, CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested )
			{
				// Params
				string tempURL = string.Format( _settings.Value.ScalingServiceHostConnectQuery, _code );
				HttpRequestMessage tempRequest = new( HttpMethod.Post, tempURL )
				{
					Content = new StringContent( JsonSerializer.Serialize( new { privateIP = tPrivateIP, version = tLatestVersion }, _JSONOptions ), Encoding.UTF8, "application/json" )
				};

				// Send
				HttpClient tempClient = _httpClientFactory.CreateClient();
				HttpResponseMessage tempResponse = await tempClient.SendAsync( tempRequest, tCancel );

				if ( tempResponse.IsSuccessStatusCode )
				{
					return JsonSerializer.Deserialize<HostData>( await tempResponse.Content.ReadAsStringAsync( tCancel ), _JSONOptions );
				}
			}

			return null;
		}

		public async Task DisconnectSessionAsync( int tSessionPort, string tUser, CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested )
			{
				// Params
				string tempURL = string.Format( _settings.Value.ScalingServiceSessionPutQuery, tSessionPort, _code );
				HttpRequestMessage tempRequest = new( HttpMethod.Put, tempURL )
				{
					Content = new StringContent( JsonSerializer.Serialize( new { user = tUser }, _JSONOptions ), Encoding.UTF8, "application/json" )
				};

				// Send
				HttpClient tempClient = _httpClientFactory.CreateClient();
				await tempClient.SendAsync( tempRequest, tCancel );
			}
		}

		public async Task UpdateVersionAsync( int tSessionPort, string tVersion, CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested )
			{
				// Params
				string tempURL = string.Format( _settings.Value.ScalingServiceSessionPutQuery, tSessionPort, _code );
				HttpRequestMessage tempRequest = new( HttpMethod.Put, tempURL )
				{
					Content = new StringContent( JsonSerializer.Serialize( new { version = tVersion }, _JSONOptions ), Encoding.UTF8, "application/json" )
				};

				// Send
				HttpClient tempClient = _httpClientFactory.CreateClient();
				await tempClient.SendAsync( tempRequest, tCancel );
			}
		}
	}
}
