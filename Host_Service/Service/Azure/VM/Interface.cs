using System.Text.Json.Serialization;

namespace Host.Service.Azure.VM
{
	public class Interface
	{
		public IPv4 Ipv4 { get; }

		[JsonConstructor]
		public Interface( IPv4 ipv4 )
		{
			Ipv4 = ipv4;
		}
	}
}
