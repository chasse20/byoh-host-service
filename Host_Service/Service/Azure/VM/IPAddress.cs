using System.Text.Json.Serialization;

namespace Host.Service.Azure.VM
{
	public class IPAddress
	{
		public string PrivateIpAddress { get; }

		[JsonConstructor]
		public IPAddress( string privateIpAddress )
		{
			PrivateIpAddress = privateIpAddress;
		}
	}
}
