using Host.Azure.Service.Signal;
using Host.Service.Azure.Scaling;
using Host.Service.Azure.VM;
using Host.Service.Controllers;
using Host.SystemInfo;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Host.Service
{
	public class Manager : IHostedService, IDisposable
	{
		private readonly IOptions<Settings> _settings;
		private readonly ILogger<Manager> _logger;
		private readonly TelemetryClient _telemetry;
		private readonly Azure.FileShare.FileShare _fileShare;
		private readonly Signal _signal;
		private readonly Scaling _scaling;
		private readonly VM _VM;
		private Data _VMData;
		private DirectoryInfo _currentUnrealVersion;
		private Dictionary<int, Session> _sessionsByLocalPort;
		private readonly System.Timers.Timer _unrealAutoUpdateTimer;
		private readonly System.Timers.Timer _healthProbeTimer;
		private readonly System.Timers.Timer _healthProbeDelayTimer;
		private CancellationTokenSource _cancel;
		private OnlineVariables _onlineVariables;
		private readonly string _privateIP;

		public Manager( IConfiguration tConfig, IOptions<Settings> tSettings, TelemetryClient tTelemetry, ILogger<Manager> tLogger, Azure.FileShare.FileShare tFileShare, Scaling tScaling, Signal tSignal, VM tVM )
		{
			// Variables
			_settings = tSettings;
			_telemetry = tTelemetry;
			_logger = tLogger;
			_fileShare = tFileShare;
			_scaling = tScaling;
			_signal = tSignal;
			_VM = tVM;
			_privateIP = tConfig.GetValue<string>( "PrivateIP" );

			// Timers
			_unrealAutoUpdateTimer = new System.Timers.Timer( tSettings.Value.UnrealUpdateCheckInterval );
			_unrealAutoUpdateTimer.Elapsed += async ( tSender, tEvent ) => await OnUnrealAutoUpdateAsync();

			_healthProbeTimer = new System.Timers.Timer( tSettings.Value.HealthProbeInterval );
			_healthProbeTimer.Elapsed += async ( tSender, tEvent ) => await OnHealthProbeAsync();

			_healthProbeDelayTimer = new System.Timers.Timer( tSettings.Value.HealthProbeStartDelay );
			_healthProbeDelayTimer.Elapsed += ( tSender, tEvent ) => OnHealthProbeDelay();
		}

		public void Dispose()
		{
			_cancel.Cancel();
			_unrealAutoUpdateTimer.Dispose();
			_healthProbeTimer.Dispose();
			_healthProbeDelayTimer.Dispose();

			// Shut down sessions
			if ( _sessionsByLocalPort != null )
			{
				foreach ( KeyValuePair<int, Session> tempKVP in _sessionsByLocalPort )
				{
					tempKVP.Value.Dispose();
				}

				_sessionsByLocalPort.Clear();
				_sessionsByLocalPort = null;
			}

			GC.SuppressFinalize( this );
		}

		public async Task StartAsync( CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested )
			{
				_cancel = new();

				// Get VM Data
				_VMData = await _VM.GetDataAsync( tCancel );

				Console.WriteLine( _VMData.Compute.Name );

				if ( _VMData != null && _VMData.Network == null )
				{
					_VMData = new
					(
						_VMData.Compute,
						new
						(
							new Interface[]
							{
								new
								(
									new
									(
										new IPAddress[]
										{
											new( _privateIP )
										}
									)
								)
							}
						)
					);
				}

				// Get latest Unreal version and online variables
				SetupCurrentUnrealVersion();
				HostData tempScaleData = await _scaling.ConnectHostAsync( _privateIP, _currentUnrealVersion?.Name, tCancel );
				Session tempSession;
				int tempGPUCount = GPU.GPUCount;

				// Setup Local Mode
				if ( tempScaleData == null )
				{
					int tempSessionCount = tempGPUCount * _settings.Value.LocalInstancesPerGPU;
					if ( tempSessionCount > _settings.Value.LocalSessionLimit )
					{
						tempSessionCount = _settings.Value.LocalSessionLimit;
					}

					// Populate
					_sessionsByLocalPort = new();

					for ( int i = 0; i < tempSessionCount; ++i )
					{
						tempSession = new
						(
							_settings.Value.SignalLocalPortStart + i,
							_settings.Value.SignalLocalPortStart + i,
							_settings.Value.UnrealPortStart + i,
							(int)Math.Ceiling( ( i + 1 ) / (float)_settings.Value.LocalInstancesPerGPU ) - 1,
							null,
							_currentUnrealVersion
						);

						_sessionsByLocalPort.Add( tempSession.localSignalPort, tempSession );

						tempSession.Start( _settings.Value, null, Log );
					}
				}
				// Setup Online Mode
				else if ( tempScaleData.Sessions != null )
				{
					_telemetry.InstrumentationKey = tempScaleData.ApplicationInsightsKey;
					_onlineVariables = new( tempScaleData.IdleTimeout, tempScaleData.Branch, tempScaleData.BuildsFileShareDirectory, tempScaleData.PublicURL, tempScaleData.CoturnURL, tempScaleData.CoturnSecret, tempScaleData.SignalTokenSecret );

					// Populate
					_sessionsByLocalPort = new();

					for ( int i = tempScaleData.Sessions.Length - 1; i >= 0; --i )
					{
						tempSession = new
						(
							tempScaleData.Sessions[ i ].Port,
							tempScaleData.Sessions[ i ].LocalPort,
							_settings.Value.UnrealPortStart + i,
							(int)Math.Ceiling( ( i + 1 ) / (float)tempScaleData.InstancesPerGPU ) - 1,
							tempScaleData.Sessions[ i ].User,
							_currentUnrealVersion
						);

						_sessionsByLocalPort.Add( tempSession.localSignalPort, tempSession );

						tempSession.Start( _settings.Value, _onlineVariables, Log );
					}
				}
				else
				{
					Log( LogLevel.Error, "Unable to configure sessions" );
				}

				// Start Timers
				if ( _settings.Value.IsAutoUpdates )
				{
					_unrealAutoUpdateTimer.Start();
				}

				if ( !_cancel.IsCancellationRequested )
				{
					_healthProbeDelayTimer.Start();
				}
			}
		}

		public async Task StopAsync( CancellationToken tCancel )
		{
			Console.WriteLine( "STOPPING" );

			// Clear timers
			_unrealAutoUpdateTimer.Stop();
			_healthProbeDelayTimer.Stop();
			_healthProbeTimer.Stop();
			_cancel.Cancel();

			// Shut down sessions
			if ( _sessionsByLocalPort != null )
			{
				foreach ( KeyValuePair<int, Session> tempKVP in _sessionsByLocalPort )
				{
					await tempKVP.Value.StopAsync( tCancel );
				}

				_sessionsByLocalPort.Clear();
				_sessionsByLocalPort = null;
			}
		}

		private void SetupCurrentUnrealVersion()
		{
			DirectoryInfo tempDirectory = new( _settings.Value.GetFullBuildsPath() );
			if ( tempDirectory.Exists )
			{
				// Get list of incomplete builds and remove their ZIP files; assumes any present zip files represent incomplete builds
				HashSet<string> tempIncompleteVersions = new();
				FileInfo[] tempIncompleteFiles = tempDirectory.GetFiles( "*.zip", SearchOption.TopDirectoryOnly );
				for ( int i = ( tempIncompleteFiles.Length - 1 ); i >= 0; --i )
				{
					tempIncompleteVersions.Add( tempIncompleteFiles[ i ].Name[ 0..^4 ] ); // remove .zip
					tempIncompleteFiles[ i ].Delete();
				}

				// Get list of all build version directories
				List<DirectoryInfo> tempVersions = new( tempDirectory.EnumerateDirectories( "*.*", SearchOption.TopDirectoryOnly ) );
				if ( tempVersions.Count > 0 )
				{
					// Remove all incomplete builds
					for ( int i = ( tempVersions.Count - 1 ); i >= 0; --i )
					{
						if ( tempIncompleteVersions.Contains( tempVersions[ i ].Name ) )
						{
							Firewall.Remove( tempVersions[ i ].Name );
							tempVersions[ i ].Delete( true );
							tempVersions.RemoveAt( i );
						}
					}

					// Sort remaining and remove old versions
					if ( tempVersions.Count > 0 )
					{
						tempVersions.Sort( ( tA, tB ) => -1 * string.Compare( tA.Name, tB.Name, true ) ); // sort by newest

						for ( int i = ( tempVersions.Count - 1 ); i > 0; --i )
						{
							Firewall.Remove( tempVersions[ i ].Name );
							tempVersions[ i ].Delete( true );
							tempVersions.RemoveAt( i );
						}

						_currentUnrealVersion = tempVersions[ 0 ];
						Firewall.Remove( _currentUnrealVersion.Name );
						Firewall.Add( _currentUnrealVersion.Name, _settings.Value.GetFullUnrealPath( _currentUnrealVersion ) );
					}
				}
			}
		}

		private async Task OnUnrealAutoUpdateAsync()
		{
			_unrealAutoUpdateTimer.Stop();

			// Install update
			DirectoryInfo tempVersion = await _fileShare.TryGetLatestUnrealVersionAsync( _currentUnrealVersion, _onlineVariables == null ? _settings.Value.LocalBuildsFileShareDirectory : _onlineVariables.buildsFileShareDirectory, _settings.Value.GetFullBuildsPath(), _cancel.Token );
			if ( tempVersion != null )
			{
				_currentUnrealVersion = tempVersion;

				// Register Firewall rules
				Firewall.Remove( _currentUnrealVersion.Name );
				Firewall.Add( _currentUnrealVersion.Name, _settings.Value.GetFullUnrealPath( _currentUnrealVersion ) );

				// Install Prerequisites
				Process tempPrerequisites = new();
				tempPrerequisites.StartInfo.FileName = _settings.Value.GetFullUnrealPrereqPath( _currentUnrealVersion );
				tempPrerequisites.StartInfo.Arguments = "/q";

				try
				{
					tempPrerequisites.Start();
					await tempPrerequisites.WaitForExitAsync( _cancel.Token );
				}
				catch ( Exception ) { }
			}

			// Apply to inactive sessions immediately
			if ( _currentUnrealVersion != null && _sessionsByLocalPort != null )
			{
				HashSet<DirectoryInfo> tempUsedVersions = new();
				HashSet<DirectoryInfo> tempOldVersions = new();
				bool tempIsVersion;

				foreach ( KeyValuePair<int, Session> tempKVP in _sessionsByLocalPort )
				{
					tempIsVersion = tempKVP.Value.Version != null;
					if ( tempKVP.Value.User == null )
					{
						if ( tempIsVersion )
						{
							tempOldVersions.Add( tempKVP.Value.Version );
						}

						await tempKVP.Value.TrySetVersionAsync( _scaling, _settings.Value, _onlineVariables, _currentUnrealVersion, Log, _cancel.Token );
						if ( tempKVP.Value.Version != null )
						{
							tempUsedVersions.Add( tempKVP.Value.Version );
						}
					}
					else if ( tempIsVersion )
					{
						tempUsedVersions.Add( tempKVP.Value.Version );
					}
				}

				// Delete old unused versions
				foreach ( DirectoryInfo tempOldVersion in tempOldVersions )
				{
					if ( !tempUsedVersions.Contains( tempOldVersion ) )
					{
						Firewall.Remove( tempOldVersion.Name );

						try
						{
							tempOldVersion.Delete( true );
						}
						catch ( Exception tException )
						{
							Log( LogLevel.Critical, $"Failed to remove older version while updating: { tException.Message }" );
						}
					}
				}
			}

			// Continue if not cancelled
			if ( !_cancel.IsCancellationRequested )
			{
				_unrealAutoUpdateTimer.Start();
			}
		}

		private async Task OnHealthProbeAsync()
		{
			_healthProbeTimer.Stop();

			Console.WriteLine( "CHECKING HEALTH STATUS" );

			// Check Session health
			if ( _sessionsByLocalPort != null )
			{
				foreach ( KeyValuePair<int, Session> tempKVP in _sessionsByLocalPort )
				{
					await tempKVP.Value.CheckHealthAsync( _signal, _settings.Value, _onlineVariables, Log, _cancel.Token );
				}
			}

			// Continue if not cancelled
			if ( !_cancel.IsCancellationRequested )
			{
				_healthProbeTimer.Start();
			}
		}

		private void OnHealthProbeDelay()
		{
			_healthProbeDelayTimer.Stop();
			_healthProbeTimer.Start();
		}

		public async Task<bool> OnSessionConnectAsync( int tLocalPort, SessionConnectData tData )
		{
			return _sessionsByLocalPort != null && _sessionsByLocalPort.TryGetValue( tLocalPort, out Session tempSession ) && await tempSession.TrySetUserAsync( _scaling, _signal, _settings.Value, _onlineVariables, tData.User, Log, _cancel.Token );
		}

		public async Task<bool> OnSessionDisconnectAsync( int tLocalPort )
		{
			return _sessionsByLocalPort != null && _sessionsByLocalPort.TryGetValue( tLocalPort, out Session tempSession ) && await tempSession.TrySetUserAsync( _scaling, _signal, _settings.Value, _onlineVariables, null, Log, _cancel.Token );
		}

		public bool OnSignalConnect( int tLocalPort )
		{
			if ( _sessionsByLocalPort != null && _sessionsByLocalPort.TryGetValue( tLocalPort, out Session tempSession ) )
			{
				tempSession.OnSignalConnect();
				return true;
			}

			return false;
		}

		private void Log( LogLevel tLevel, string tMessage, params object[] tArgs )
		{
			tMessage = "[{IP} {Branch} {InstanceId}] " + tMessage;
			object[] tempParams = new object[] { _onlineVariables == null ? _VMData.Network.Interfaces[ 0 ].Ipv4.IpAddress[ 0 ].PrivateIpAddress : _onlineVariables.publicURL, _onlineVariables == null ? -1 : _onlineVariables.branch, _VMData.Compute.Name };
			if ( tArgs != null )
			{
				tempParams = tempParams.Concat( tArgs ).ToArray();
			}

			if ( tLevel == LogLevel.Critical )
			{
				_logger.LogCritical( tMessage, tempParams );
			}
			else if ( tLevel == LogLevel.Error )
			{
				_logger.LogError( tMessage, tempParams );
			}
		}
	}
}
