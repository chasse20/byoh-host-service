using System.Text.Json.Serialization;

namespace Host.Service.Azure.VM
{
	public class Compute
	{
		public string Name { get; }

		[JsonConstructor]
		public Compute( string name )
		{
			Name = name;
		}
	}
}
