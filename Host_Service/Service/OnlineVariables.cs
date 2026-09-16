namespace Host.Service
{
	public class OnlineVariables
	{
		public readonly int idleTimeout;
		public readonly int branch;
		public readonly string buildsFileShareDirectory;
		public readonly string publicURL;
		public readonly string coturnURL;
		public readonly string coturnSecret;
		public readonly string signalTokenSecret;

		public OnlineVariables( int tIdleTimeout, int tBranch, string tBuildsFileShareDirectory, string tPublicURL, string tCoturnURL, string tCoturnSecret, string tSignalTokenSecret )
		{
			idleTimeout = tIdleTimeout;
			branch = tBranch;
			buildsFileShareDirectory = tBuildsFileShareDirectory;
			publicURL = tPublicURL;
			coturnURL = tCoturnURL;
			coturnSecret = tCoturnSecret;
			signalTokenSecret = tSignalTokenSecret;
		}
	}
}
