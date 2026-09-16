using System;

namespace Host.Service
{
	[Flags]
	public enum SessionStatus
	{
		None = 0,
		SignalServiceConnected = 1,
		UnrealConnected = 2
	}
}
