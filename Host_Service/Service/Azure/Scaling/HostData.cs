using System.Text.Json.Serialization;

namespace Host.Service.Azure.Scaling
{
	public class HostData
	{
		public Session[] Sessions { get; }
		public int InstancesPerGPU { get; }
		public int IdleTimeout { get; }
		public int Branch { get; }
		public string PublicURL { get; }
		public string BuildsFileShareDirectory { get; }
		public string ApplicationInsightsKey { get; }
		public string CoturnURL { get; }
		public string CoturnSecret { get; }
		public string SignalTokenSecret { get; }

		[JsonConstructor]
		public HostData( Session[] sessions, int instancesPerGPU, int idleTimeout, int branch, string publicURL, string buildsFileShareDirectory, string applicationInsightsKey, string coturnURL, string coturnSecret, string signalTokenSecret )
		{
			Sessions = sessions;
			InstancesPerGPU = instancesPerGPU;
			IdleTimeout = idleTimeout;
			Branch = branch;
			PublicURL = publicURL;
			BuildsFileShareDirectory = buildsFileShareDirectory;
			ApplicationInsightsKey = applicationInsightsKey;
			CoturnURL = coturnURL;
			CoturnSecret = coturnSecret;
			SignalTokenSecret = signalTokenSecret;
		}
	}
}
