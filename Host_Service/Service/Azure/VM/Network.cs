using System.Text.Json.Serialization;

namespace Host.Service.Azure.VM
{
	public class Network
	{
		[JsonPropertyName( "interface" )]
		public Interface[] Interfaces { get; }

		[JsonConstructor]
		public Network( Interface[] interfaces )
		{
			Interfaces = interfaces;
		}
	}
}
