using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Host.Service.Azure.VM
{
	public class VM
	{
		private readonly IHttpClientFactory _httpClientFactory;
		protected readonly JsonSerializerOptions _JSONOptions = new( JsonSerializerDefaults.Web );

		public VM( IHttpClientFactory tHttpClientFactory )
		{
			_httpClientFactory = tHttpClientFactory;
		}

		public async Task<Data> GetDataAsync( CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested )
			{
				// Params
				HttpRequestMessage tempRequest = new( HttpMethod.Get, "http://169.254.169.254/metadata/instance?api-version=2019-02-01" );
				tempRequest.Headers.Add( "Metadata", "true" );

				// Send
				HttpClient tempClient = _httpClientFactory.CreateClient();

				try
				{
					HttpResponseMessage tempResponse = await tempClient.SendAsync( tempRequest, tCancel );

					// Process
					if ( tempResponse.IsSuccessStatusCode )
					{
						return JsonSerializer.Deserialize<Data>( await tempResponse.Content.ReadAsStringAsync( tCancel ), _JSONOptions );
					}
				}
				catch ( Exception ) { }
			}

			return new Data( new( "Local" ), new( null ) );
		}
	}
}
