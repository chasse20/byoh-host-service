using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using Azure.Identity;
using System.Reflection;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Host
{
	public class Program
	{
		public static void Main( string[] tArgs )
		{
			// Generate URLs
			var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine( tArgs ).AddJsonFile( "appsettings.json" ).Build();
			string tempPrivateIP = null;
			string tempURL = configuration.GetValue<string>( "URL" );
			string[] tempURLs = new string[ 2 ];
			tempURLs[ 1 ] = string.Format( tempURL, "localhost" );

			using ( Socket tempSocket = new( AddressFamily.InterNetwork, SocketType.Dgram, 0 ) ) // get private URL on local gateway
			{
				tempSocket.Connect( "8.8.8.8", 65530 );
				tempPrivateIP = ( tempSocket.LocalEndPoint as IPEndPoint ).Address.ToString();
				tempURLs[ 0 ] = string.Format( tempURL, tempPrivateIP );
			}

			// Initialize
			Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder( tArgs ).ConfigureWebHostDefaults
			(
				tWebBuilder =>
				{
					tWebBuilder.UseStartup<Startup>();
					tWebBuilder.UseUrls( tempURLs );
				}
			).ConfigureAppConfiguration
			(
				( tContext, tConfig ) =>
				{
					LoggerFactory tempLogFactory = new();
					ILogger<Program> tempLogger = tempLogFactory.CreateLogger<Program>();

					try
					{
						Uri keyVaultEndpoint = new( configuration.GetValue<string>( "VaultUri" ) );
						tConfig.AddAzureKeyVault( keyVaultEndpoint, new DefaultAzureCredential() );
					}
					catch ( Exception tException )
					{
						tempLogger.LogError( tException.Message );
					};

					tContext.Configuration[ "PrivateIP" ] = tempPrivateIP;
				}
			).UseWindowsService
			(
				tConfig =>
				{
					Assembly tempAssembly = Assembly.GetExecutingAssembly();
					FileVersionInfo tempInfo = FileVersionInfo.GetVersionInfo( tempAssembly.Location );
					tConfig.ServiceName = tempInfo.ProductName;
				}
			).Build().Run();
		}
	}
}
