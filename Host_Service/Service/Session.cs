using Host.Azure.Service.Signal;
using Host.Service.Azure.Scaling;
using Host.SystemInfo;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Host.Service
{
	public class Session : IDisposable
	{
		public readonly int signalPort;
		public readonly int localSignalPort;
		public readonly int unrealPort;
		public readonly int GPU;
		private int _signalRetries = 1;
		private int _unrealRetries = 1;
		private SessionStatus _status;
		private Process _unrealProcess;
		private Process _signalProcess;
		private DateTime _unrealStartTime;
		private DateTime _signalStartTime;
		public DirectoryInfo Version { get; private set; }
		public string User { get; private set; }

		public Session( int tSignalPort, int tLocalSignalPort, int tUnrealPort, int tGPU, string tUser, DirectoryInfo tVersion )
		{
			signalPort = tSignalPort;
			localSignalPort = tLocalSignalPort;
			unrealPort = tUnrealPort;
			GPU = tGPU;
			User = tUser;
			Version = tVersion;
		}

		public void Dispose()
		{
			StopUnrealAsync( CancellationToken.None ).Wait();
			StopSignalAsync( CancellationToken.None ).Wait();

			GC.SuppressFinalize( this );
		}

		public void Start( Settings tSettings, OnlineVariables tOnlineVariables, Action<LogLevel, string, object[]> tLogging )
		{
			StartUnreal( tSettings, tLogging );
			StartSignal( tSettings, tOnlineVariables, tLogging );
		}

		public async Task StopAsync( CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested )
			{
				await StopUnrealAsync( tCancel );
				await StopSignalAsync( tCancel );
			}
		}

		private bool StartUnreal( Settings tSettings, Action<LogLevel,string,object[]> tLogging )
		{
			if ( Version != null && ( _unrealProcess == null || _unrealProcess.HasExited ) )
			{
				_status &= ~SessionStatus.UnrealConnected;
				_unrealRetries = 1;
				_unrealStartTime = DateTime.Now;

				_unrealProcess = new Process();
				_unrealProcess.StartInfo.FileName = tSettings.GetFullUnrealPath( Version );
				_unrealProcess.StartInfo.Arguments = string.Format( tSettings.UnrealLaunchOptions, GPU, unrealPort );
				_unrealProcess.StartInfo.UseShellExecute = true;

				try
				{
					_unrealProcess.Start();
				}
				catch ( Exception )
				{
					tLogging( LogLevel.Critical, "[{SignalPort}] Failed to start Unreal", new object[] { localSignalPort } );
					return false;
				};

				return true;
			}

			return false;
		}

		private async Task<bool> StopUnrealAsync( CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested && _unrealProcess != null )
			{
				_status &= ~SessionStatus.UnrealConnected;
				_unrealStartTime = DateTime.MaxValue;

				_unrealProcess.Kill();
				await _unrealProcess.WaitForExitAsync( tCancel );
				_unrealProcess = null;

				return true;
			}

			return false;
		}

		private bool StartSignal( Settings tSettings, OnlineVariables tOnlineVariables, Action<LogLevel, string, object[]> tLogging )
		{
			if ( Version != null && ( _signalProcess == null || _signalProcess.HasExited ) )
			{
				_status &= ~SessionStatus.SignalServiceConnected;
				_signalRetries = 1;
				_signalStartTime = DateTime.Now;

				_signalProcess = new Process();
				_signalProcess.StartInfo.FileName = tSettings.GetFullSignalPath();
				_signalProcess.StartInfo.UseShellExecute = true;

				if ( tOnlineVariables == null )
				{
					_signalProcess.StartInfo.Arguments = string.Format( tSettings.SignalLaunchOptions, null, null, null, tSettings.LocalIdleTimeout, User ?? "", "0.0.0.0:" + signalPort, localSignalPort, unrealPort );
				}
				else
				{
					_signalProcess.StartInfo.Arguments = string.Format( tSettings.SignalLaunchOptions, tOnlineVariables.coturnURL, tOnlineVariables.coturnSecret, tOnlineVariables.signalTokenSecret, tOnlineVariables.idleTimeout, User ?? "", tOnlineVariables.publicURL + ":" + signalPort, localSignalPort, unrealPort );
				}

				try
				{
					_signalProcess.Start();
				}
				catch ( Exception )
				{
					tLogging( LogLevel.Critical, "[{SignalPort}] Failed to start Signal Service", new object[] { localSignalPort } );
					return false;
				};

				return true;
			}

			return false;
		}

		private async Task<bool> StopSignalAsync( CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested && _signalProcess != null )
			{
				_status &= ~SessionStatus.SignalServiceConnected;
				_signalStartTime = DateTime.MaxValue;

				_signalProcess.KillWithChildren();
				await _signalProcess.WaitForExitAsync( tCancel );
				_signalProcess = null;

				return true;
			}

			return false;
		}

		public async Task<bool> TrySetVersionAsync( Scaling tScaling, Settings tSettings, OnlineVariables tOnlineVariables, DirectoryInfo tVersion, Action<LogLevel, string, object[]> tLogging, CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested )
			{
				bool tempIsChanged = false;

				if ( tVersion == null )
				{
					Version = null;
					await StopUnrealAsync( tCancel );

					tempIsChanged = true;
				}
				else if ( Version == null )
				{
					Version = tVersion;

					tempIsChanged = true;
				}
				else if ( Version.FullName != tVersion.FullName )
				{
					Version = tVersion;
					await StopUnrealAsync( tCancel );
					StartUnreal( tSettings, tLogging );

					tempIsChanged = true;
				}

				// Notify the Scaling Service
				if ( tempIsChanged && tOnlineVariables != null )
				{
					await tScaling.UpdateVersionAsync( signalPort, Version.Name, tCancel );
				}

				return tempIsChanged;
			}

			return false;
		}

		public async Task<bool> TrySetUserAsync( Scaling tScaling, Signal tSignal, Settings tSettings, OnlineVariables tOnlineVariables, string tUser, Action<LogLevel, string, object[]> tLogging, CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested && tUser != User )
			{
				// Notify the Signal Service of a user connect or change
				if ( tUser != null )
				{
					User = tUser;
					await tSignal.ConnectUserAsync( localSignalPort, User, tCancel );
				}
				// Notify the Scaling Service of a user disconnect
				else if ( tOnlineVariables != null )
				{
					await tScaling.DisconnectSessionAsync( signalPort, tUser, tCancel );
				}

				User = tUser;

				// Restart Unreal if exiting user
				if ( User == null )
				{
					await StopUnrealAsync( tCancel );
					StartUnreal( tSettings, tLogging );
				}

				return true;
			}

			return false;
		}

		public void OnSignalConnect()
		{
			_status |= SessionStatus.SignalServiceConnected;
			_signalRetries = 1;
		}

		public async Task CheckHealthAsync( Signal tSignal, Settings tSettings, OnlineVariables tOnlineVariables, Action<LogLevel, string, object[]> tLogging, CancellationToken tCancel )
		{
			if ( !tCancel.IsCancellationRequested )
			{
				// Restart Unreal if not running
				bool tempIsUnrealRunning = _unrealProcess != null && !_unrealProcess.HasExited;
				if ( !tempIsUnrealRunning )
				{
					tLogging( LogLevel.Critical, "[{SignalPort}] Health Probe failed for Unreal", new object[] { localSignalPort } );
					StartUnreal( tSettings, tLogging );
				}

				// Signal Status
				if ( _signalProcess == null || _signalProcess.HasExited )
				{
					tLogging( LogLevel.Critical, "[{SignalPort}] Health Probe failed for Signal Service A", new object[] { localSignalPort } );
					StartSignal( tSettings, tOnlineVariables, tLogging );
				}
				// Health Probe
				else if ( _status.HasFlag( SessionStatus.SignalServiceConnected ) )
				{
					StatusData tempStatus = await tSignal.GetStatus( localSignalPort, tCancel );
					if ( tempStatus == null )
					{
						tLogging( LogLevel.Critical, "[{SignalPort}] Health Probe failed to get status for Signal Service B", new object[] { localSignalPort } );
						
						await StopSignalAsync( tCancel );
						StartSignal( tSettings, tOnlineVariables, tLogging );
					}
					else
					{
						_signalRetries = 1;

						// Unreal is connected
						if ( tempStatus.IsUnrealConnected )
						{
							_status |= SessionStatus.UnrealConnected;
							_unrealRetries = 1;
						}
						// Restart Unreal if not connected
						else if ( tempIsUnrealRunning && ( DateTime.Now - _unrealStartTime ).TotalMilliseconds >= tSettings.UnrealMaxTimeout )
						{
							tLogging( LogLevel.Error, "[{SignalPort}] Health Probe timeout for Unreal", new object[] { localSignalPort } );

							--_unrealRetries;
							if ( _unrealRetries <= 0 )
							{
								await StopUnrealAsync( tCancel );
								StartUnreal( tSettings, tLogging );
							}
						}
					}
				}
				// Assumed failure if there is still no time after grace period
				else if ( ( DateTime.Now - _signalStartTime ).TotalMilliseconds >= tSettings.SignalMaxTimeout )
				{
					tLogging( LogLevel.Error, "[{SignalPort}] Health Probe timeout for  Signal Service", new object[] { localSignalPort } );

					--_signalRetries;
					if ( _signalRetries <= 0 )
					{
						await StopSignalAsync( tCancel );
						StartSignal( tSettings, tOnlineVariables, tLogging );
					}
				}
			}
		}
	}
}
