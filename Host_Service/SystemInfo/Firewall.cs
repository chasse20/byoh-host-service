using System;
using NetFwTypeLib;

namespace Host.SystemInfo
{
	/// <summary>
	/// Utility class for managing Windows Firewall
	/// </summary>
	public static class Firewall
	{
		/// <summary>
		/// Returns Windows Firewall Policy API reference
		/// </summary>
		private static INetFwPolicy2 Policy
		{
			get
			{
				try
				{
#pragma warning disable CA1416 // Validate platform compatibility
					return (INetFwPolicy2)Activator.CreateInstance( Type.GetTypeFromProgID( "HNetCfg.FwPolicy2" ) );
#pragma warning restore CA1416 // Validate platform compatibility
				}
				catch ( Exception ) { }

				return null;
			}
		}

		/// <summary>
		/// Registers an application public rule with Windows Firewall
		/// </summary>
		/// <param name="tRuleName">Name of application rule</param>
		/// <param name="tPath">Path of application</param>
		public static void Add( string tRuleName, string tPath )
		{
			try
			{
#pragma warning disable CA1416 // Validate platform compatibility
				INetFwRule tempRule = (INetFwRule)Activator.CreateInstance( Type.GetTypeFromProgID( "HNetCfg.FWRule" ) );
#pragma warning restore CA1416 // Validate platform compatibility
				tempRule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
				tempRule.ApplicationName = tPath;
				tempRule.Name = tRuleName;
				tempRule.Enabled = true;
				tempRule.InterfaceTypes = "All";
				tempRule.Profiles |= 4; // NET_FW_PROFILE2_PUBLIC
				tempRule.Protocol = 256; // NET_FW_IP_PROTOCOL_ANY

				Policy.Rules.Add( tempRule );
			}
			catch ( Exception ) { }
		}

		/// <summary>
		/// Unregisters a rule with Windows Firewall
		/// </summary>
		/// <param name="tRuleName">Name of the rule</param>
		public static void Remove( string tRuleName )
		{
			try
			{
				Policy.Rules.Remove( tRuleName );
			}
			catch ( Exception ) { }
		}
	}
}
