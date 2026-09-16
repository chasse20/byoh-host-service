using System.Management;

namespace Host.SystemInfo
{
	public static class GPU
	{
		public static int GPUCount
		{
#pragma warning disable CA1416 // Validate platform compatibility
			get
			{
				int tempGPUCount = 0;
				ManagementObjectCollection tempResults = new ManagementObjectSearcher( "select * from Win32_VideoController" ).Get();
				foreach ( ManagementObject tempObject in tempResults )
				{
					string tempName = tempObject[ "Name" ].ToString();
					if ( !tempName.Contains( "Hyper" ) && !tempName.Contains( "Remote" ) && tempObject[ "Status" ].ToString() == "OK" )
					{
						++tempGPUCount;
					}
				}

				return tempGPUCount;
			}
#pragma warning restore CA1416 // Validate platform compatibility
		}
	}
}
