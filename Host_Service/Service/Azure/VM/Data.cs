using System.Text.Json.Serialization;

namespace Host.Service.Azure.VM
{
	public class Data
	{
		public Compute Compute { get; }
		public Network Network { get; }

		[JsonConstructor]
		public Data( Compute compute, Network network )
		{
			Compute = compute;
			Network = network;
		}
	}
}
