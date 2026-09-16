using System.Text.Json.Serialization;

namespace Host.Service.Azure.Scaling
{
	public class Session
	{
		public int Port { get; }
		public int LocalPort { get; }
		public string User { get; }

		[JsonConstructor]
		public Session( int port, int localPort, string user )
		{
			Port = port;
			LocalPort = localPort;
			User = user;
		}
	}
}
