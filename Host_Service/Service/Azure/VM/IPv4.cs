using System.Text.Json.Serialization;

namespace Host.Service.Azure.VM
{
	public class IPv4
	{
		public IPAddress[] IpAddress { get; }

		[JsonConstructor]
		public IPv4( IPAddress[] ipAddress )
		{
			IpAddress = ipAddress;
		}
	}
}
