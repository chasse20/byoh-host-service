using System.Text.Json.Serialization;

namespace Host.Azure.Service.Signal
{
	public class StatusData
	{
		public bool IsUnrealConnected { get; }

		[JsonConstructor]
		public StatusData( bool isUnrealConnected )
		{
			IsUnrealConnected = isUnrealConnected;
		}
	}
}
