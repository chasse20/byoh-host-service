using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Host.Service.Azure.FileShare
{
	public class FileShare
	{
		private readonly IOptions<Settings> _settings;
		private readonly string _connection;

		public FileShare( IOptions<Settings> tSettings, IConfiguration tConfig )
		{
			_settings = tSettings;
			_connection = tConfig.GetValue<string>( _settings.Value.KeyStoreFileShareConnection );
		}

		public async Task<DirectoryInfo> TryGetLatestUnrealVersionAsync( DirectoryInfo tCurrentVersion, string tBuildsDirectory, string tDeployPath, CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested )
			{
				Console.WriteLine( "STARTING UNREAL UPDATE" );

				// Get the latest build from Azure
				ShareDirectoryClient tempDirectory = new( _connection, _settings.Value.AzureFileShareName, tBuildsDirectory );
				if ( await tempDirectory.ExistsAsync( tCancel ) )
				{
					// Iterate files to get list of .zip builds
					Console.WriteLine( "FINDING LATEST BUILD" );

					List<ShareFileItem> tempBuilds = new();
					AsyncPageable<ShareFileItem> tempFiles = tempDirectory.GetFilesAndDirectoriesAsync( options: null, tCancel );

					await foreach ( ShareFileItem tempFile in tempFiles )
					{
						if ( tempFile.Name.EndsWith( ".zip" ) )
						{
							tempBuilds.Add( tempFile );
						}
					}

					// Filter out most recent and check if up to date
					if ( tempBuilds.Count > 0 )
					{
						tempBuilds.Sort( ( tA, tB ) => -1 * string.Compare( tA.Name, tB.Name, true ) ); // descending order
						string tempBuildDirectoryName = tempBuilds[ 0 ].Name[ 0..^4 ]; // remove .zip

						if ( tCurrentVersion == null || tCurrentVersion.Name != tempBuildDirectoryName )
						{
							// Download latest
							Console.WriteLine( "DOWNLOADING LATEST BUILD" );

							string tempPath = tDeployPath + tempBuilds[ 0 ].Name;
							ShareFileClient tempFile = tempDirectory.GetFileClient( tempBuilds[ 0 ].Name );

							if ( await tempFile.ExistsAsync( tCancel ) )
							{
								ShareFileDownloadInfo tempDownload = await tempFile.DownloadAsync( default, false, null, tCancel );
								try
								{
									using ( FileStream tempStream = File.Create( tempPath ) )
									{
										await tempDownload.Content.CopyToAsync( tempStream, tCancel );
									}
								}
								catch ( Exception )
								{
									//Debug.Log( "Failed to download latest Unreal version: " + tException.Message, LogLevel.Critical );
									return null;
								}

								// Unzip
								if ( !tCancel.IsCancellationRequested )
								{
									Console.WriteLine( "UNZIPPING LATEST BUILD" );

									FileInfo tempWindowsFile = new( tempPath );
									bool tempIsUnzipFail = false;
									DirectoryInfo tempVersion;

									try
									{
										ZipFile.ExtractToDirectory( tempWindowsFile.FullName, tempWindowsFile.DirectoryName );
									}
									catch ( Exception )
									{
										//Debug.Log( "Failed to unzip latest Unreal version: " + tException.Message, LogLevel.Critical );
										tempIsUnzipFail = true;
									}

									tempWindowsFile.Delete();

									tempVersion = new( tDeployPath + tempBuildDirectoryName );
									if ( !tempVersion.Exists )
									{
										//Debug.Log( "Failed to update latest Unreal version: unexpected directory structure in zip file!", LogLevel.Critical );
									}
									else if ( tempIsUnzipFail )
									{
										tempVersion.Delete( true );
									}
									else
									{
										//Debug.Log( "Unzipped latest version: " + tempVersion.Name, LogLevel.Success );
										return tempVersion;
									}
								}
							}
						}
					}
				}
				else
				{
					//Debug.Log( "Azure File Share directory not found", LogLevel.Critical );
				}
			}

			return null;
		}
	}
}
