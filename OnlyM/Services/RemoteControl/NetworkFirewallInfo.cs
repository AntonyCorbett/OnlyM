using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Serilog;

namespace OnlyM.Services.RemoteControl;

/// <summary>
/// Reads which connected networks Windows treats as Public, and which network profiles a firewall rule covers, so
/// Settings can explain why phones can't reach the remote (a Private-only rule blocks them on a Public network).
/// </summary>
internal static class NetworkFirewallInfo
{
    // NET_FW_PROFILE2_*: the profile bits of INetFwRule.Profiles
    internal const int FirewallProfileDomain = 1;
    internal const int FirewallProfilePrivate = 2;
    internal const int FirewallProfilePublic = 4;

    private const int NlmEnumNetworkConnected = 1;
    private const int NlmCategoryPublic = 0;

    /// <summary>
    /// Gets the names of the connected networks whose Windows network profile is Public (empty if none, or if
    /// they can't be read).
    /// </summary>
    public static IReadOnlyList<string> GetConnectedPublicNetworks()
    {
        var result = new List<string>();
        try
        {
            var manager = (INetworkListManager)new NetworkListManager();
            var networks = manager.GetNetworks(NlmEnumNetworkConnected);
            var buffer = new INetwork[1];
            while (networks.Next(1, buffer, out var fetched) == 0 && fetched == 1)
            {
                var network = buffer[0];
                if (network.GetCategory() == NlmCategoryPublic)
                {
                    result.Add(network.GetName());
                }
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Could not read the network profiles");
        }

        return result;
    }

    /// <summary>
    /// Gets the profile bits (<see cref="FirewallProfilePublic"/> etc.) of the named inbound firewall rule, or
    /// null if there is no such rule or the firewall can't be read.
    /// </summary>
    public static int? GetFirewallRuleProfiles(string ruleName)
    {
        try
        {
            var policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            if (policyType == null)
            {
                return null;
            }

            dynamic policy = Activator.CreateInstance(policyType)!;
            dynamic rule = policy.Rules.Item(ruleName); // throws when there is no such rule
            return (int)rule.Profiles;
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Could not read firewall rule {Rule}", ruleName);
            return null;
        }
    }

    /// <summary>
    /// Gets a value indicating whether a rule with these profile bits lets devices on a Public network connect
    /// (an unreadable or missing rule doesn't).
    /// </summary>
    internal static bool CoversPublic(int? profiles) => profiles is { } p && (p & FirewallProfilePublic) != 0;

    [ComImport]
    [Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B")]
    private class NetworkListManager
    {
    }

    // Only the leading members needed are declared; COM dispatches by vtable slot, so the order must match netlistmgr.h.
    [ComImport]
    [Guid("DCB00000-570F-4A9B-8D69-199FDBA5723B")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface INetworkListManager
    {
        IEnumNetworks GetNetworks(int flags);
    }

    [ComImport]
    [Guid("DCB00003-570F-4A9B-8D69-199FDBA5723B")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface IEnumNetworks
    {
        [DispId(-4)]
        object NewEnum
        {
            [return: MarshalAs(UnmanagedType.IUnknown)]
            get;
        }

        [PreserveSig]
        int Next(int celt, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] INetwork[] elements, out int fetched);
    }

    [ComImport]
    [Guid("DCB00002-570F-4A9B-8D69-199FDBA5723B")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface INetwork
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetName();

        void SetName([MarshalAs(UnmanagedType.BStr)] string name);

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetDescription();

        void SetDescription([MarshalAs(UnmanagedType.BStr)] string description);

        Guid GetNetworkId();

        int GetDomainType();

        [return: MarshalAs(UnmanagedType.IUnknown)]
        object GetNetworkConnections();

        void GetTimeCreatedAndConnected(out uint createdLow, out uint createdHigh, out uint connectedLow, out uint connectedHigh);

        bool IsConnectedToInternet { get; }

        bool IsConnected { get; }

        int GetConnectivity();

        int GetCategory();
    }
}
