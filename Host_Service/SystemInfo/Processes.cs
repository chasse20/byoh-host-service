using System;
using System.Diagnostics;
using System.Management;

namespace Host.SystemInfo
{
	/// <summary>
	/// Utility class for handling Windows processes
	/// </summary>
	public static class Processes
	{
		/// <summary>
		/// Extension method for a <see cref="Process"/> object that will kill itself and any children
		/// </summary>
		/// <param name="tProcess">Process object</param>
		public static void KillWithChildren( this Process tProcess )
		{
			// Kill children
#pragma warning disable CA1416 // Validate platform compatibility
			ManagementObjectCollection tempProcesses = new ManagementClass( "Win32_Process" ).GetInstances();
			foreach ( ManagementObject tempProcessObject in tempProcesses )
			{
				if ( Convert.ToInt32( tempProcessObject[ "ParentProcessId" ] ) == tProcess.Id )
				{
					try
					{
						Process.GetProcessById( Convert.ToInt32( tempProcessObject[ "ProcessId" ] ) ).KillWithChildren();
					}
					catch ( Exception ) { }
				}
			}
#pragma warning restore CA1416 // Validate platform compatibility

			// Kill
			try
			{
				tProcess.Kill();
			}
			catch ( Exception ) { }
		}
	}
}
