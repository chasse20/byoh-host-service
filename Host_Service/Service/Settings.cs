using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Host.Service
{
	public class Settings
	{
		public int HealthProbeInterval { get; set; }
		public int HealthProbeStartDelay { get; set; }
		public int LocalInstancesPerGPU { get; set; }
		public int LocalIdleTimeout { get; set; }
		public int LocalSessionLimit { get; set; }
		public int SignalMaxTimeout { get; set; }
		public int SignalLocalPortStart { get; set; }
		public int UnrealMaxTimeout { get; set; }
		public int UnrealPortStart { get; set; }
		public int UnrealUpdateCheckInterval { get; set; }
		public bool IsAutoUpdates { get; set; }
		public string BuildsPath { get; set; }
		public string ServicesPath { get; set; }
		public string KeyStoreScalingServiceCode { get; set; }
		public string KeyStoreFileShareConnection { get; set; }
		public string LocalBuildsFileShareDirectory { get; set; }
		public string AzureFileShareName { get; set; }
		public string ScalingServiceHostConnectQuery { get; set; }
		public string ScalingServiceSessionPutQuery { get; set; }
		public string SignalLaunchOptions { get; set; }
		public string SignalPath { get; set; }
		public string SignalSessionConnectQuery { get; set; }
		public string SignalStatusQuery { get; set; }
		public string UnrealLaunchOptions { get; set; }
		public string UnrealPath { get; set; }
		public string UnrealPrereqPath { get; set; }
		public string URL { get; set; }
		private static string _rootPath;

		public static string RootPath
		{
			get
			{
				if ( _rootPath == null )
				{
					FileVersionInfo tempInfo = FileVersionInfo.GetVersionInfo( Assembly.GetExecutingAssembly().Location );
					_rootPath = Path.GetPathRoot( Environment.SystemDirectory ) + tempInfo.ProductName;
				}

				return _rootPath;
			}
		}

		public string GetFullBuildsPath()
		{
			return string.Format( BuildsPath, RootPath );
		}

		public string GetFullServicesPath()
		{
			return string.Format( ServicesPath, RootPath );
		}

		public string GetFullUnrealPath( DirectoryInfo tVersion )
		{
			return string.Format( UnrealPath, tVersion.FullName );
		}

		public string GetFullUnrealPrereqPath( DirectoryInfo tVersion )
		{
			return string.Format( UnrealPrereqPath, tVersion.FullName );
		}

		public string GetFullSignalPath()
		{
			return string.Format( SignalPath, GetFullServicesPath() );
		}
	}
}
