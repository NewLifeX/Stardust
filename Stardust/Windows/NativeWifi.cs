using System.Runtime.InteropServices;
using System.Text;

namespace Stardust.Windows;

/// <summary>
/// A managed implementation of Native Wifi API
/// </summary>
public class NativeWifi
{
    #region Win32
#pragma warning disable CS1591
    [DllImport("Wlanapi.dll")]
    public static extern UInt32 WlanOpenHandle(
        UInt32 dwClientVersion,
        IntPtr pReserved,
        out UInt32 pdwNegotiatedVersion,
        out IntPtr phClientHandle);

    [DllImport("Wlanapi.dll")]
    public static extern UInt32 WlanCloseHandle(
        IntPtr hClientHandle,
        IntPtr pReserved);

    [DllImport("Wlanapi.dll")]
    public static extern void WlanFreeMemory(IntPtr pMemory);

    [DllImport("Wlanapi.dll")]
    public static extern UInt32 WlanEnumInterfaces(
        IntPtr hClientHandle,
        IntPtr pReserved,
        out IntPtr ppInterfaceList);

    [DllImport("Wlanapi.dll")]
    public static extern UInt32 WlanGetAvailableNetworkList(
        IntPtr hClientHandle,
        [MarshalAs(UnmanagedType.LPStruct)] Guid pInterfaceGuid,
        UInt32 dwFlags,
        IntPtr pReserved,
        out IntPtr ppAvailableNetworkList);

    [DllImport("Wlanapi.dll")]
    public static extern UInt32 WlanQueryInterface(
        IntPtr hClientHandle,
        [MarshalAs(UnmanagedType.LPStruct)] Guid pInterfaceGuid,
        WLAN_INTF_OPCODE OpCode,
        IntPtr pReserved,
        out UInt32 pdwDataSize,
        ref IntPtr ppData,
        IntPtr pWlanOpcodeValueType);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public String strInterfaceDescription;

        public WLAN_INTERFACE_STATE isState;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WLAN_INTERFACE_INFO_LIST
    {
        public UInt32 dwNumberOfItems;
        public UInt32 dwIndex;
        public WLAN_INTERFACE_INFO[] InterfaceInfo;

        public WLAN_INTERFACE_INFO_LIST(IntPtr ppInterfaceList)
        {
            dwNumberOfItems = (UInt32)Marshal.ReadInt32(ppInterfaceList, 0);
            dwIndex = (UInt32)Marshal.ReadInt32(ppInterfaceList, 4 /* Offset for dwNumberOfItems */);
            InterfaceInfo = new WLAN_INTERFACE_INFO[dwNumberOfItems];

            for (var i = 0; i < dwNumberOfItems; i++)
            {
                var interfaceInfo = new IntPtr(ppInterfaceList.ToInt64()
                    + 8 /* Offset for dwNumberOfItems and dwIndex */
                    + (Marshal.SizeOf(typeof(WLAN_INTERFACE_INFO)) * i) /* Offset for preceding items */);

                InterfaceInfo[i] = (WLAN_INTERFACE_INFO)Marshal.PtrToStructure(interfaceInfo, typeof(WLAN_INTERFACE_INFO));
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WLAN_AVAILABLE_NETWORK
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public String strProfileName;

        public DOT11_SSID dot11Ssid;
        public DOT11_BSS_TYPE dot11BssType;
        public UInt32 uNumberOfBssids;
        public Boolean bNetworkConnectable;
        public UInt32 wlanNotConnectableReason;
        public UInt32 uNumberOfPhyTypes;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public DOT11_PHY_TYPE[] dot11PhyTypes;

        public Boolean bMorePhyTypes;
        public UInt32 wlanSignalQuality;
        public Boolean bSecurityEnabled;
        public DOT11_AUTH_ALGORITHM dot11DefaultAuthAlgorithm;
        public DOT11_CIPHER_ALGORITHM dot11DefaultCipherAlgorithm;
        public UInt32 dwFlags;
        public UInt32 dwReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WLAN_AVAILABLE_NETWORK_LIST
    {
        public UInt32 dwNumberOfItems;
        public UInt32 dwIndex;
        public WLAN_AVAILABLE_NETWORK[] Network;

        public WLAN_AVAILABLE_NETWORK_LIST(IntPtr ppAvailableNetworkList)
        {
            dwNumberOfItems = (UInt32)Marshal.ReadInt32(ppAvailableNetworkList, 0);
            dwIndex = (UInt32)Marshal.ReadInt32(ppAvailableNetworkList, 4 /* Offset for dwNumberOfItems */);
            Network = new WLAN_AVAILABLE_NETWORK[dwNumberOfItems];

            for (var i = 0; i < dwNumberOfItems; i++)
            {
                var availableNetwork = new IntPtr(ppAvailableNetworkList.ToInt64()
                    + 8 /* Offset for dwNumberOfItems and dwIndex */
                    + (Marshal.SizeOf(typeof(WLAN_AVAILABLE_NETWORK)) * i) /* Offset for preceding items */);

                Network[i] = (WLAN_AVAILABLE_NETWORK)Marshal.PtrToStructure(availableNetwork, typeof(WLAN_AVAILABLE_NETWORK));
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DOT11_SSID
    {
        public UInt32 uSSIDLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public Byte[] ucSSID;

        public readonly Byte[]? ToBytes() => ucSSID?.Take((Int32)uSSIDLength).ToArray();

        private static readonly Encoding _encoding =
            Encoding.GetEncoding(65001, // UTF-8 code page
                EncoderFallback.ReplacementFallback,
                DecoderFallback.ExceptionFallback);

        public override String ToString()
        {
            if (ucSSID == null)
                return null;

            try
            {
                return _encoding.GetString(ToBytes());
            }
            catch (DecoderFallbackException)
            {
                return null;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DOT11_MAC_ADDRESS
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public Byte[] ucDot11MacAddress;

        public readonly Byte[]? ToBytes() => ucDot11MacAddress?.ToArray();

        public override readonly String ToString()
        {
            return ucDot11MacAddress != null
                ? BitConverter.ToString(ucDot11MacAddress).Replace('-', ':')
                : null;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WLAN_CONNECTION_ATTRIBUTES
    {
        public WLAN_INTERFACE_STATE isState;
        public WLAN_CONNECTION_MODE wlanConnectionMode;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public String strProfileName;

        public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
        public WLAN_SECURITY_ATTRIBUTES wlanSecurityAttributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WLAN_ASSOCIATION_ATTRIBUTES
    {
        public DOT11_SSID dot11Ssid;
        public DOT11_BSS_TYPE dot11BssType;
        public DOT11_MAC_ADDRESS dot11Bssid;
        public DOT11_PHY_TYPE dot11PhyType;
        public UInt32 uDot11PhyIndex;
        public UInt32 wlanSignalQuality;
        public UInt32 ulRxRate;
        public UInt32 ulTxRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WLAN_SECURITY_ATTRIBUTES
    {
        [MarshalAs(UnmanagedType.Bool)]
        public Boolean bSecurityEnabled;

        [MarshalAs(UnmanagedType.Bool)]
        public Boolean bOneXEnabled;

        public DOT11_AUTH_ALGORITHM dot11AuthAlgorithm;
        public DOT11_CIPHER_ALGORITHM dot11CipherAlgorithm;
    }

    public enum WLAN_INTERFACE_STATE
    {
        NotReady = 0,
        Connected = 1,
        AdHocNetworkFormed = 2,
        Disconnecting = 3,
        Disconnected = 4,
        Associating = 5,
        Discovering = 6,
        Authenticating = 7
    }

    public enum WLAN_CONNECTION_MODE
    {
        Profile,
        TemporaryProfile,
        DiscoverySecure,
        DiscoveryUnsecure,
        Auto,
        Invalid
    }

    public enum DOT11_BSS_TYPE
    {
        /// <summary>
        /// Infrastructure BSS network
        /// </summary>
        Infrastructure = 1,

        /// <summary>
        /// Independent BSS (IBSS) network
        /// </summary>
        Independent = 2,

        /// <summary>
        /// Either infrastructure or IBSS network
        /// </summary>
        Any = 3,
    }

    public enum DOT11_PHY_TYPE : UInt32
    {
        Unknown = 0,
        Any = 0,
        Fhss = 1,
        Dsss = 2,
        Irbaseband = 3,
        Ofdm = 4,
        Hrdsss = 5,
        Erp = 6,
        Ht = 7,
        Vht = 8,
        IHV_Start = 0x80000000,
        IHV_End = 0xffffffff
    }

    public enum DOT11_AUTH_ALGORITHM : UInt32
    {
        _80211_OPEN = 1,
        _80211_SHARED_KEY = 2,
        WPA = 3,
        WPA_PSK = 4,
        WPA_NONE = 5,
        RSNA = 6,
        RSNA_PSK = 7,
        IHV_START = 0x80000000,
        IHV_END = 0xffffffff
    }

    public enum DOT11_CIPHER_ALGORITHM : UInt32
    {
        NONE = 0x00,
        WEP40 = 0x01,
        TKIP = 0x02,
        CCMP = 0x04,
        WEP104 = 0x05,
        WPA_USE_GROUP = 0x100,
        RSN_USE_GROUP = 0x100,
        WEP = 0x101,
        IHV_START = 0x80000000,
        IHV_END = 0xffffffff
    }

    public enum WLAN_INTF_OPCODE : UInt32
    {
        AutoconfStart = 0x000000000,
        AutoconfEnabled,
        BackgroundScanEnabled,
        MediaStreamingMode,
        RadioState,
        BssType,
        InterfaceState,
        CurrentConnection,
        ChannelNumber,
        SupportedInfrastructureAuthCipherPairs,
        SupportedAdhocAuthCipherPairs,
        SupportedCountryOrRegionStringList,
        CurrentOperationMode,
        SupportedSafeMode,
        CertifiedSafeMode,
        HostedNetworkCapable,
        ManagementFrameProtectionCapable,
        AutoconfEnd = 0x0fffffff,
        MsmStart = 0x10000100,
        Statistics,
        Rssi,
        MsmEnd = 0x1fffffff,
        SecurityStart = 0x20010000,
        SecurityEnd = 0x2fffffff,
        HivStart = 0x30000000,
        IhvEnd = 0x3fffffff
    }

    const UInt32 WLAN_AVAILABLE_NETWORK_INCLUDE_ALL_ADHOC_PROFILES = 0x00000001;
    const UInt32 WLAN_AVAILABLE_NETWORK_INCLUDE_ALL_MANUAL_HIDDEN_PROFILES = 0x00000002;

    const UInt32 ERROR_SUCCESS = 0;
#pragma warning restore CS1591
    #endregion

    /// <summary>
    /// Gets SSIDs of available wireless LANs.
    /// </summary>
    /// <returns>SSIDs</returns>
    public static IEnumerable<WLAN_AVAILABLE_NETWORK> GetAvailableNetworkSsids()
    {
        var clientHandle = IntPtr.Zero;
        var interfaceList = IntPtr.Zero;
        var availableNetworkList = IntPtr.Zero;

        try
        {
            if (WlanOpenHandle(
                2, // Client version for Windows Vista and Windows Server 2008
                IntPtr.Zero,
                out var negotiatedVersion,
                out clientHandle) != ERROR_SUCCESS)
                yield break;

            if (WlanEnumInterfaces(
                clientHandle,
                IntPtr.Zero,
                out interfaceList) != ERROR_SUCCESS)
                yield break;

            var interfaceInfoList = new WLAN_INTERFACE_INFO_LIST(interfaceList);

            //Console.WriteLine("Interface count: {0}", interfaceInfoList.dwNumberOfItems);

            foreach (var interfaceInfo in interfaceInfoList.InterfaceInfo)
            {
                if (WlanGetAvailableNetworkList(
                    clientHandle,
                    interfaceInfo.InterfaceGuid,
                    WLAN_AVAILABLE_NETWORK_INCLUDE_ALL_MANUAL_HIDDEN_PROFILES,
                    IntPtr.Zero,
                    out availableNetworkList) != ERROR_SUCCESS)
                    continue;

                var networkList = new WLAN_AVAILABLE_NETWORK_LIST(availableNetworkList);

                foreach (var network in networkList.Network)
                {
                    //Console.WriteLine("Interface: {0}, SSID: {1}, Signal: {2}",
                    //    interfaceInfo.strInterfaceDescription,
                    //    network.dot11Ssid,
                    //    network.wlanSignalQuality);

                    yield return network;
                }
            }
        }
        finally
        {
            if (availableNetworkList != IntPtr.Zero)
                WlanFreeMemory(availableNetworkList);

            if (interfaceList != IntPtr.Zero)
                WlanFreeMemory(interfaceList);

            if (clientHandle != IntPtr.Zero)
                WlanCloseHandle(clientHandle, IntPtr.Zero);
        }
    }

    /// <summary>
    /// Gets SSIDs of connected wireless LANs.
    /// </summary>
    /// <returns>SSIDs</returns>
    public static IEnumerable<WLAN_ASSOCIATION_ATTRIBUTES> GetConnectedNetworkSsids()
    {
        var clientHandle = IntPtr.Zero;
        var interfaceList = IntPtr.Zero;
        var queryData = IntPtr.Zero;

        try
        {
            if (WlanOpenHandle(
                2, // Client version for Windows Vista and Windows Server 2008
                IntPtr.Zero,
                out var negotiatedVersion,
                out clientHandle) != ERROR_SUCCESS)
                yield break;

            if (WlanEnumInterfaces(
                clientHandle,
                IntPtr.Zero,
                out interfaceList) != ERROR_SUCCESS)
                yield break;

            var interfaceInfoList = new WLAN_INTERFACE_INFO_LIST(interfaceList);

            //Console.WriteLine("Interface count: {0}", interfaceInfoList.dwNumberOfItems);

            foreach (var interfaceInfo in interfaceInfoList.InterfaceInfo)
            {
                if (WlanQueryInterface(
                    clientHandle,
                    interfaceInfo.InterfaceGuid,
                    WLAN_INTF_OPCODE.CurrentConnection,
                    IntPtr.Zero,
                    out var dataSize,
                    ref queryData,
                    IntPtr.Zero) != ERROR_SUCCESS) // If not connected to a network, ERROR_INVALID_STATE will be returned.
                    continue;

                var connection = (WLAN_CONNECTION_ATTRIBUTES)Marshal.PtrToStructure(queryData, typeof(WLAN_CONNECTION_ATTRIBUTES));
                if (connection.isState != WLAN_INTERFACE_STATE.Connected)
                    continue;

                var association = connection.wlanAssociationAttributes;

                //Console.WriteLine("Interface: {0}, SSID: {1}, BSSID: {2}, Signal: {3}",
                //    interfaceInfo.strInterfaceDescription,
                //    association.dot11Ssid,
                //    association.dot11Bssid,
                //    association.wlanSignalQuality);

                yield return association;
            }
        }
        finally
        {
            if (queryData != IntPtr.Zero)
                WlanFreeMemory(queryData);

            if (interfaceList != IntPtr.Zero)
                WlanFreeMemory(interfaceList);

            if (clientHandle != IntPtr.Zero)
                WlanCloseHandle(clientHandle, IntPtr.Zero);
        }
    }
}