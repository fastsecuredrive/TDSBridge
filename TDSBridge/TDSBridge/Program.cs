using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TDSBridge.Common;

namespace TDSBridge
{
    class Program
    {
        static bool debugMode = false;
        
        public static bool DebugMode => debugMode;

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Usage();
                return;
            }

            // Check for debug flag
            debugMode = args.Contains("--debug") || args.Contains("-d");
            
            // Set the debug mode for the Common library
            TDSBridge.Common.DebugConfig.DebugMode = debugMode;

            // Check if user wants to use LocalDB
            System.Net.IPEndPoint sqlEndpoint = null;
            if (args[1].ToLower() != "localdb")
            {
                System.Net.IPHostEntry iphe = System.Net.Dns.GetHostEntry(args[1]);
                sqlEndpoint = new System.Net.IPEndPoint(iphe.AddressList[0], int.Parse(args[2]));
            }
            // If args[1] is "localdb", sqlEndpoint stays null and SniBridge will use LocalDB

            BridgeAcceptor b = new BridgeAcceptor(
                int.Parse(args[0]),
                sqlEndpoint
                );

            b.TDSMessageReceived += new TDSMessageReceivedDelegate(b_TDSMessageReceived);
            b.TDSPacketReceived += new TDSPacketReceivedDelegate(b_TDSPacketReceived);
            b.ConnectionAccepted += new ConnectionAcceptedDelegate(b_ConnectionAccepted);
            b.ConnectionDisconnected += new ConnectionDisconnectedDelegate(b_ConnectionClosed);
            b.BridgeException += new BridgeExceptionDelegate(b_BridgeException);
            b.ListeningThreadException += new ListeningThreadExceptionDelegate(b_ListeningThreadException);

            b.Start();

            if (debugMode)
            {
                Console.WriteLine($"Running on port {args[0]} in DEBUG mode. Press enter to kill this process...");
                Console.WriteLine("Debug mode: All TDS packets and SQL queries will be logged.");
            }
            else
            {
                Console.WriteLine($"Running on port {args[0]}. Press enter to kill this process...");
            }
            Console.ReadLine();

            b.Stop();
        }

        static void b_ConnectionClosed(object sender, BridgedConnection bc, ConnectionType ct)
        {
            if (debugMode)
                Console.WriteLine(FormatDateTime() + "|Connection " + ct + " closed (" + bc.SocketCouple + ")");
        }

        static void b_ConnectionAccepted(object sender, System.Net.Sockets.Socket sAccepted)
        {
            if (debugMode)
                Console.WriteLine(FormatDateTime() + "|New connection from " + sAccepted.RemoteEndPoint);
            else
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] New connection from {sAccepted.RemoteEndPoint}");
        }

        static void b_BridgeException(object sender, BridgedConnection bc, ConnectionType ct, Exception exce)
        {
            // Always show exceptions regardless of debug mode
            Console.WriteLine(FormatDateTime() + "|Bridge Exception (" + ct + "): " + exce.ToString());
        }

        static void b_ListeningThreadException(object sender, System.Net.Sockets.Socket sListening, Exception exce)
        {
            // Always show exceptions regardless of debug mode
            Console.WriteLine(FormatDateTime() + "|Listening Thread Exception: " + exce.ToString());
        }

        static void b_TDSPacketReceived(object sender, BridgedConnection bc, Common.Packet.TDSPacket packet)
        {
            if (debugMode)
                Console.WriteLine(FormatDateTime() + "|" + packet);
        }

        static void b_TDSMessageReceived(object sender, BridgedConnection bc, Common.Message.TDSMessage msg)
        {
            if (debugMode)
            {
                Console.WriteLine(FormatDateTime() + "|" + msg);
                if (msg is Common.Message.SQLBatchMessage)
                {
                    Console.Write("\tSQLBatch message ");
                    Common.Message.SQLBatchMessage b = (Common.Message.SQLBatchMessage)msg;
                    string strBatchText = b.GetBatchText();
                    Console.Write("({0:N0} chars worth of {1:N0} bytes of data)[", strBatchText.Length, strBatchText.Length * 2);
                    Console.Write(strBatchText);
                    Console.WriteLine("]");
                }
                else if (msg is Common.Message.RPCRequestMessage)
                {
                    try
                    {
                        Common.Message.RPCRequestMessage rpc = (Common.Message.RPCRequestMessage)msg;
                        byte[] bPayload = rpc.AssemblePayload();

                        #if DEBUG
                        //using (System.IO.FileStream fs = new System.IO.FileStream(
                        //    "C:\\temp\\dev\\" + (iRPC++) + ".raw",
                        //    System.IO.FileMode.Create,
                        //    System.IO.FileAccess.Write,
                        //    System.IO.FileShare.Read))
                        //{
                        //    fs.Write(bPayload, 0, bPayload.Length);
                        //}
                        #endif

                    }
                    catch (Exception exce)
                    {
                        Console.WriteLine("Exception: " + exce.ToString());
                    }
                }
            }
            else
            {
                // In non-debug mode, only show SQL queries for transparency
                if (msg is Common.Message.SQLBatchMessage)
                {
                    Common.Message.SQLBatchMessage sqlMsg = (Common.Message.SQLBatchMessage)msg;
                    string sqlText = sqlMsg.GetBatchText().Trim();
                    if (!string.IsNullOrEmpty(sqlText))
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SQL: {sqlText}");
                    }
                }
            }
        }

        static string FormatDateTime()
        {
            return Environment.NewLine + Environment.NewLine + DateTime.Now.ToString("yyyyMMdd HH:mm:ss.ffffff");

        }

        static void Usage()
        {
            Console.WriteLine(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " <listen port> <sql server address> <sql server port>");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " 1533 127.0.0.1 1433");
            Console.WriteLine("    - Listen on port 1533, forward to SQL Server on 127.0.0.1:1433");
            Console.WriteLine();
            Console.WriteLine("  " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " 1533 localdb dummy");
            Console.WriteLine("    - Listen on port 1533, forward to LocalDB (address/port ignored for LocalDB)");
            Console.WriteLine();
            Console.WriteLine("Note: When using 'localdb' as address, it will connect to (localdb)\\MSSQLLocalDB");
        }
    }
}
