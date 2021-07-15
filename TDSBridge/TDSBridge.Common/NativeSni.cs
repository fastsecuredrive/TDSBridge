using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace TDSBridge.Common
{
    #region deps
    internal sealed class SQLDNSInfo
    {
        public string FQDN { get; set; }
        public string AddrIPv4 { get; set; }
        public string AddrIPv6 { get; set; }
        public string Port { get; set; }

        internal SQLDNSInfo(string FQDN, string ipv4, string ipv6, string port)
        {
            this.FQDN = FQDN;
            AddrIPv4 = ipv4;
            AddrIPv6 = ipv6;
            Port = port;
        }
    }
    internal sealed class SNIHandle : SafeHandle
    {
        private readonly uint _status = unchecked((uint)-1); //TdsEnums.SNI_UNINITIALIZED;
        private readonly bool _fSync = false;

        // creates a physical connection
        internal SNIHandle(
            NativeSni.ConsumerInfo myInfo,
            string serverName,
            byte[] spnBuffer,
            bool ignoreSniOpenTimeout,
            int timeout,
            out byte[] instanceName,
            bool flushCache,
            bool fSync,
            bool fParallel,
            SqlConnectionIPAddressPreference ipPreference,
            SQLDNSInfo cachedDNSInfo)
            : base(IntPtr.Zero, true)
        {
            try
            { }
            finally
            {
                _fSync = fSync;
                instanceName = new byte[256]; // Size as specified by netlibs.
                if (ignoreSniOpenTimeout)
                {
                    timeout = Timeout.Infinite; // -1 == native SNIOPEN_TIMEOUT_VALUE / INFINITE
                }

                _status = NativeSni.SNIOpenSyncEx(myInfo, serverName, ref base.handle,
                            spnBuffer, instanceName, flushCache, fSync, timeout, fParallel, ipPreference, cachedDNSInfo);
            }
        }

        public override bool IsInvalid
        {
            get
            {
                return (IntPtr.Zero == base.handle);
            }
        }

        override protected bool ReleaseHandle()
        {
            // NOTE: The SafeHandle class guarantees this will be called exactly once.
            IntPtr ptr = base.handle;
            base.handle = IntPtr.Zero;
            if (IntPtr.Zero != ptr)
            {
                if (0 != NativeSni.SNIClose(ptr))
                {
                    return false;   // SNIClose should never fail.
                }
            }
            return true;
        }

        internal uint Status
        {
            get
            {
                return _status;
            }
        }
    }

    internal sealed class SNIPacket : SafeHandle
    {
        internal SNIPacket(SafeHandle sniHandle) : base(IntPtr.Zero, true)
        {
            NativeSni.SNIPacketAllocate(sniHandle, NativeSni.IOType.WRITE, ref base.handle);
            if (IntPtr.Zero == base.handle)
            {
                throw new Exception("Failed to allocate a SNI Packet"); // SQL.SNIPacketAllocationFailure();
            }
        }

        public override bool IsInvalid
        {
            get
            {
                return (IntPtr.Zero == base.handle);
            }
        }

        override protected bool ReleaseHandle()
        {
            // NOTE: The SafeHandle class guarantees this will be called exactly once.
            IntPtr ptr = base.handle;
            base.handle = IntPtr.Zero;
            if (IntPtr.Zero != ptr)
            {
                NativeSni.SNIPacketRelease(ptr);
            }
            return true;
        }
    }


    public enum SqlConnectionIPAddressPreference
    {
        IPv4First = 0,  // default
        IPv6First = 1,
        UsePlatformDefault = 2
    }
    #endregion

    class NativeSni
    {
        private const string SNI = "Microsoft.Data.SqlClient.SNI.dll";
        private const int SniOpenTimeOut = -1; // infinite

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void SqlAsyncCallbackDelegate(IntPtr m_ConsKey, IntPtr pPacket, uint dwError);

        #region Structs\Enums
        [StructLayout(LayoutKind.Sequential)]
        internal struct ConsumerInfo
        {
            internal int defaultBufferSize;
            internal SqlAsyncCallbackDelegate readDelegate;
            internal SqlAsyncCallbackDelegate writeDelegate;
            internal IntPtr key;
        }

        internal enum ConsumerNumber
        {
            SNI_Consumer_SNI,
            SNI_Consumer_SSB,
            SNI_Consumer_PacketIsReleased,
            SNI_Consumer_Invalid,
        }

        internal enum IOType
        {
            READ,
            WRITE,
        }

        internal enum PrefixEnum
        {
            UNKNOWN_PREFIX,
            SM_PREFIX,
            TCP_PREFIX,
            NP_PREFIX,
            VIA_PREFIX,
            INVALID_PREFIX,
        }

        internal enum ProviderEnum
        {
            HTTP_PROV,
            NP_PROV,
            SESSION_PROV,
            SIGN_PROV,
            SM_PROV,
            SMUX_PROV,
            SSL_PROV,
            TCP_PROV,
            VIA_PROV,
            MAX_PROVS,
            INVALID_PROV,
        }

        internal enum QTypes
        {
            SNI_QUERY_CONN_INFO,
            SNI_QUERY_CONN_BUFSIZE,
            SNI_QUERY_CONN_KEY,
            SNI_QUERY_CLIENT_ENCRYPT_POSSIBLE,
            SNI_QUERY_SERVER_ENCRYPT_POSSIBLE,
            SNI_QUERY_CERTIFICATE,
            SNI_QUERY_LOCALDB_HMODULE,
            SNI_QUERY_CONN_ENCRYPT,
            SNI_QUERY_CONN_PROVIDERNUM,
            SNI_QUERY_CONN_CONNID,
            SNI_QUERY_CONN_PARENTCONNID,
            SNI_QUERY_CONN_SECPKG,
            SNI_QUERY_CONN_NETPACKETSIZE,
            SNI_QUERY_CONN_NODENUM,
            SNI_QUERY_CONN_PACKETSRECD,
            SNI_QUERY_CONN_PACKETSSENT,
            SNI_QUERY_CONN_PEERADDR,
            SNI_QUERY_CONN_PEERPORT,
            SNI_QUERY_CONN_LASTREADTIME,
            SNI_QUERY_CONN_LASTWRITETIME,
            SNI_QUERY_CONN_CONSUMER_ID,
            SNI_QUERY_CONN_CONNECTTIME,
            SNI_QUERY_CONN_HTTPENDPOINT,
            SNI_QUERY_CONN_LOCALADDR,
            SNI_QUERY_CONN_LOCALPORT,
            SNI_QUERY_CONN_SSLHANDSHAKESTATE,
            SNI_QUERY_CONN_SOBUFAUTOTUNING,
            SNI_QUERY_CONN_SECPKGNAME,
            SNI_QUERY_CONN_SECPKGMUTUALAUTH,
            SNI_QUERY_CONN_CONSUMERCONNID,
            SNI_QUERY_CONN_SNIUCI,
            SNI_QUERY_CONN_SUPPORTS_EXTENDED_PROTECTION,
            SNI_QUERY_CONN_CHANNEL_PROVIDES_AUTHENTICATION_CONTEXT,
            SNI_QUERY_CONN_PEERID,
            SNI_QUERY_CONN_SUPPORTS_SYNC_OVER_ASYNC,
        }

        internal enum TransparentNetworkResolutionMode : byte
        {
            DisabledMode = 0,
            SequentialMode,
            ParallelMode
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct Sni_Consumer_Info
        {
            public int DefaultUserDataLength;
            public IntPtr ConsumerKey;
            public IntPtr fnReadComp;
            public IntPtr fnWriteComp;
            public IntPtr fnTrace;
            public IntPtr fnAcceptComp;
            public uint dwNumProts;
            public IntPtr rgListenInfo;
            public IntPtr NodeAffinity;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct SNI_CLIENT_CONSUMER_INFO
        {
            public Sni_Consumer_Info ConsumerInfo;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string wszConnectionString;
            public PrefixEnum networkLibrary;
            public byte* szSPN;
            public uint cchSPN;
            public byte* szInstanceName;
            public uint cchInstanceName;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fOverrideLastConnectCache;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fSynchronousConnection;
            public int timeout;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fParallel;
            public TransparentNetworkResolutionMode transparentNetworkResolution;
            public int totalTimeout;
            public bool isAzureSqlServerEndpoint;
            public SqlConnectionIPAddressPreference ipAddressPreference;
            public SNI_DNSCache_Info DNSCacheInfo;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SNI_DNSCache_Info
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string wszCachedFQDN;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string wszCachedTcpIPv4;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string wszCachedTcpIPv6;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string wszCachedTcpPort;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SNI_Error
        {
            internal ProviderEnum provider;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]
            internal string errorMessage;
            internal uint nativeError;
            internal uint sniError;
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string fileName;
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string function;
            internal uint lineNumber;
        }

        #endregion


        #region DLL Imports
        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIAddProviderWrapper")]
        internal static extern uint SNIAddProvider(SNIHandle pConn, ProviderEnum ProvNum, [In] ref uint pInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNICheckConnectionWrapper")]
        internal static extern uint SNICheckConnection([In] SNIHandle pConn);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNICloseWrapper")]
        internal static extern uint SNIClose(IntPtr pConn);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SNIGetLastError(out SNI_Error pErrorStruct);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SNIPacketRelease(IntPtr pPacket);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIPacketResetWrapper")]
        internal static extern void SNIPacketReset([In] SNIHandle pConn, IOType IOType, SNIPacket pPacket, ConsumerNumber ConsNum);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIQueryInfo(QTypes QType, ref uint pbQInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIQueryInfo(QTypes QType, ref IntPtr pbQInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIReadAsyncWrapper")]
        internal static extern uint SNIReadAsync(SNIHandle pConn, ref IntPtr ppNewPacket);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNIReadSyncOverAsync(SNIHandle pConn, ref IntPtr ppNewPacket, int timeout);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIRemoveProviderWrapper")]
        internal static extern uint SNIRemoveProvider(SNIHandle pConn, ProviderEnum ProvNum);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNISecInitPackage(ref uint pcbMaxToken);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNISetInfoWrapper")]
        internal static extern uint SNISetInfo(SNIHandle pConn, QTypes QType, [In] ref uint pbQInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SNITerminate();

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIWaitForSSLHandshakeToCompleteWrapper")]
        internal static extern uint SNIWaitForSSLHandshakeToComplete([In] SNIHandle pConn, int dwMilliseconds, out uint pProtocolVersion);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UnmanagedIsTokenRestricted([In] IntPtr token, [MarshalAs(UnmanagedType.Bool)] out bool isRestricted);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint GetSniMaxComposedSpnLength();

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIGetInfoWrapper([In] SNIHandle pConn, NativeSni.QTypes QType, out Guid pbQInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIGetInfoWrapper([In] SNIHandle pConn, NativeSni.QTypes QType, out ushort portNum);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern uint SNIGetPeerAddrStrWrapper([In] SNIHandle pConn, int bufferSize, StringBuilder addrBuffer, out uint addrLen);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIGetInfoWrapper([In] SNIHandle pConn, NativeSni.QTypes QType, out ProviderEnum provNum);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIInitialize([In] IntPtr pmo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIOpenSyncExWrapper(ref SNI_CLIENT_CONSUMER_INFO pClientConsumerInfo, out IntPtr ppConn);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIOpenWrapper(
            [In] ref Sni_Consumer_Info pConsumerInfo,
            [MarshalAs(UnmanagedType.LPWStr)] string szConnect,
            [In] SNIHandle pConn,
            out IntPtr ppConn,
            [MarshalAs(UnmanagedType.Bool)] bool fSync,
            SqlConnectionIPAddressPreference ipPreference,
            [In] ref SNI_DNSCache_Info pDNSCachedInfo);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SNIPacketAllocateWrapper([In] SafeHandle pConn, IOType IOType);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIPacketGetDataWrapper([In] IntPtr packet, [In, Out] byte[] readBuffer, uint readBufferLength, out uint dataSize);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void SNIPacketSetData(SNIPacket pPacket, [In] byte* pbBuf, uint cbBuf);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe uint SNISecGenClientContextWrapper(
            [In] SNIHandle pConn,
            [In, Out] byte[] pIn,
            uint cbIn,
            [In, Out] byte[] pOut,
            [In] ref uint pcbOut,
            [MarshalAsAttribute(UnmanagedType.Bool)] out bool pfDone,
            byte* szServerInfo,
            uint cbServerInfo,
            [MarshalAsAttribute(UnmanagedType.LPWStr)] string pwszUserName,
            [MarshalAsAttribute(UnmanagedType.LPWStr)] string pwszPassword);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIWriteAsyncWrapper(SNIHandle pConn, [In] SNIPacket pPacket);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SNIWriteSyncOverAsync(SNIHandle pConn, [In] SNIPacket pPacket);
        #endregion

        #region Additional Methods added as needed.
        internal static void SNIPacketAllocate(SafeHandle pConn, IOType IOType, ref IntPtr pPacket)
        {
            pPacket = SNIPacketAllocateWrapper(pConn, IOType);
        }

        internal static unsafe uint SNIOpenSyncEx(ConsumerInfo consumerInfo, string constring, ref IntPtr pConn, byte[] spnBuffer, byte[] instanceName, bool fOverrideCache,
                                   bool fSync, int timeout, bool fParallel, SqlConnectionIPAddressPreference ipPreference, SQLDNSInfo cachedDNSInfo)
        {
            fixed (byte* pin_instanceName = &instanceName[0])
            {
                SNI_CLIENT_CONSUMER_INFO clientConsumerInfo = new SNI_CLIENT_CONSUMER_INFO();

                // initialize client ConsumerInfo part first
                MarshalConsumerInfo(consumerInfo, ref clientConsumerInfo.ConsumerInfo);

                clientConsumerInfo.wszConnectionString = constring;
                clientConsumerInfo.networkLibrary = PrefixEnum.UNKNOWN_PREFIX;

                clientConsumerInfo.szInstanceName = pin_instanceName;
                clientConsumerInfo.cchInstanceName = (uint)instanceName.Length;
                clientConsumerInfo.fOverrideLastConnectCache = fOverrideCache;
                clientConsumerInfo.fSynchronousConnection = fSync;
                clientConsumerInfo.timeout = timeout;
                clientConsumerInfo.fParallel = fParallel;

                clientConsumerInfo.transparentNetworkResolution = TransparentNetworkResolutionMode.DisabledMode;
                clientConsumerInfo.totalTimeout = SniOpenTimeOut;
                clientConsumerInfo.isAzureSqlServerEndpoint = false; // ADP.IsAzureSqlServerEndpoint(constring);

                clientConsumerInfo.ipAddressPreference = ipPreference;
                clientConsumerInfo.DNSCacheInfo.wszCachedFQDN = cachedDNSInfo?.FQDN;
                clientConsumerInfo.DNSCacheInfo.wszCachedTcpIPv4 = cachedDNSInfo?.AddrIPv4;
                clientConsumerInfo.DNSCacheInfo.wszCachedTcpIPv6 = cachedDNSInfo?.AddrIPv6;
                clientConsumerInfo.DNSCacheInfo.wszCachedTcpPort = cachedDNSInfo?.Port;

                if (spnBuffer != null)
                {
                    fixed (byte* pin_spnBuffer = &spnBuffer[0])
                    {
                        clientConsumerInfo.szSPN = pin_spnBuffer;
                        clientConsumerInfo.cchSPN = (uint)spnBuffer.Length;
                        return SNIOpenSyncExWrapper(ref clientConsumerInfo, out pConn);
                    }
                }
                else
                {
                    // else leave szSPN null (SQL Auth)
                    return SNIOpenSyncExWrapper(ref clientConsumerInfo, out pConn);
                }
            }
        }

        private static void MarshalConsumerInfo(ConsumerInfo consumerInfo, ref Sni_Consumer_Info native_consumerInfo)
        {
            native_consumerInfo.DefaultUserDataLength = consumerInfo.defaultBufferSize;
            native_consumerInfo.fnReadComp = null != consumerInfo.readDelegate
                ? Marshal.GetFunctionPointerForDelegate(consumerInfo.readDelegate)
                : IntPtr.Zero;
            native_consumerInfo.fnWriteComp = null != consumerInfo.writeDelegate
                ? Marshal.GetFunctionPointerForDelegate(consumerInfo.writeDelegate)
                : IntPtr.Zero;
            native_consumerInfo.ConsumerKey = consumerInfo.key;
        }
        #endregion
    }
}
