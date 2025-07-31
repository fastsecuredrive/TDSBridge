using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TDSBridge.Common
{
    public class SniBridge
    {
        private object _operationLock = new object();
        private SNIHandle _nativeSni;

        public void Initialize(out byte[] instanceName, System.Net.IPEndPoint sqlServerEndpoint = null)
        {
            var info = CreateConsumerInfo(false /*unused...*/);
            var spnBuffer = new byte[1][];

            // Determine the server name to connect to
            string serverName;
            if (sqlServerEndpoint != null)
            {
                // For TCP/IP connections (including LocalDB with TCP enabled)
                serverName = $"{sqlServerEndpoint.Address},{sqlServerEndpoint.Port}";
            }
            else
            {
                // Default to LocalDB - you can change this to your LocalDB instance
                serverName = "(localdb)\\MSSQLLocalDB";
                // Alternative formats:
                // serverName = "localhost\\Brevium_2012";  // Original hardcoded instance
                // serverName = "(localdb)\\v11.0";         // Version-specific LocalDB
            }

            lock (_operationLock)
            {
                Console.WriteLine($"Attempting to connect to: {serverName}");
                
                try
                {
                    NativeSni.SNIInitialize(); // Dunno about this...

                    _nativeSni = new SNIHandle(
                        info,
                        serverName,
                        spnBuffer[0] /* used for integrated security - not used */,
                        true,
                        10000,
                        out instanceName,
                        false,
                        true,
                        false,
                        SqlConnectionIPAddressPreference.IPv4First,
                        null);
                    Console.WriteLine($"Successfully connected to: {serverName}");
                    Console.WriteLine($"Got back instance name: {Encoding.ASCII.GetString(instanceName)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to connect to {serverName}: {ex.Message}");
                    Console.WriteLine($"Exception details: {ex}");
                    throw;
                }
                //NativeSni.EnableSsl(_nativeSni);
                //var sslRetVal = NativeSni.SNIWaitForSSLHandshakeToComplete(_nativeSni, 30000, out uint protocolVersion);
                //Console.WriteLine($"SSL Handshake done with version {protocolVersion} and ret val {sslRetVal}");
            }
        }

        public void Send(byte[] inputBuffer, int inputLength)
        {
            Interlocked.MemoryBarrier();

            //lock (_operationLock)
            {
                Console.WriteLine($"Sending {inputLength} bytes");
                var packet = new SNIPacket(_nativeSni);
                if (IntPtr.Zero == packet.DangerousGetHandle())
                    throw new Exception("Dangerous packet returned zero...?");
                NativeSni.SNIPacketSetData(packet, inputBuffer, inputLength);
                var writeResult = NativeSni.SNIWritePacket(_nativeSni, packet, true);
                Console.WriteLine($"Wrote {inputLength} bytes with result {writeResult}");
            }
        }

        public bool Connected => false;

        public void Disconnect(bool b)
        {
            throw new NotImplementedException();
        }

        public int Receive(byte[] outputBuffer)
        {
            Interlocked.MemoryBarrier();

            //lock (_operationLock)
            {
                IntPtr readPacketPtr = IntPtr.Zero;
                var error = NativeSni.SNIReadSyncOverAsync(_nativeSni, ref readPacketPtr, 250);
                Console.WriteLine($"Received bytes with error {error}");
                //if (error == 997) return -42; /* Operation in progress */
                //if (error == 233) return -42; /* No process on the other end of the pipe. */
                //if (error != 0) throw new Exception("SNIReadAsync threw error " + error);
                if (error != 0 && error != 997 && error != 258)
                {
                    throw new Exception("SNIReadAsync threw error " + error);
                }
                else
                {
                    Console.WriteLine("ReadAsync returned code " + error);
                    if (error == 258) return -42;
                }

                if (readPacketPtr == IntPtr.Zero)
                {
                    throw new Exception($"Code was {error} but pointer was still zero");
                }

                uint dataSize = default;
                error = NativeSni.SNIPacketGetData(readPacketPtr, outputBuffer, ref dataSize);
                if (error != 0) throw new Exception("SNIPacketGetData threw error " + error);
                Console.WriteLine($"Turns out there were {dataSize} bytes.");
                NativeSni.SNIPacketRelease(readPacketPtr);
                return (int)dataSize;
            }
        }

        // ReSharper disable once UnusedParameter.Local
#pragma warning disable IDE0060 // Remove unused parameter
        private NativeSni.ConsumerInfo CreateConsumerInfo(bool async)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            var myInfo = new NativeSni.ConsumerInfo { defaultBufferSize = 4096 };

            // This code will be necessary should we ever use async.
            //if (async)
            //{
            //    myInfo.readDelegate = SNILoadHandle.SingletonInstance.ReadAsyncCallbackDispatcher;
            //    myInfo.writeDelegate = SNILoadHandle.SingletonInstance.WriteAsyncCallbackDispatcher;
            //    _gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
            //    myInfo.key = (IntPtr)_gcHandle;
            //}

            return myInfo;
        }
    }
}
