# Remote Administration Tool

Educational project to learn client-server architecture and network programming.

## Purpose

This project demonstrates how remote administration tools work. It is similar to commercial tools like TeamViewer or enterprise management systems. The goal is to learn network programming, secure communication, and system administration concepts.

## Features

**Server:**
- Listen for client connections
- View connected clients and their system information
- Send commands to clients
- SSL/TLS encrypted communication

**Client:**
- Connect to management server
- Send system information (OS, username, hardware ID)
- Auto-reconnect on disconnect
- Listen for commands

## Requirements

- .NET 6.0 SDK or later
- Windows OS
- Visual Studio or VS Code

## How to Run

### Start the Server

1. Create a new console project
2. Copy `Server/C2_Server_Educational.cs` as `Program.cs`
3. Add package: `dotnet add package System.Management`
4. Run: `dotnet run`

### Start the Client

1. Create a new console project
2. Copy `Client/MalwareClient_Test.cs` as `Program.cs`
3. Add packages:
   - `dotnet add package Microsoft.VisualBasic`
   - `dotnet add package System.Management`
   - `dotnet add package System.Windows.Forms`
4. Edit the `Config` class to set your server IP address
5. Run: `dotnet run`

## Configuration

In the client code, change these values:

```csharp
public static class Config
{
    public static string HOST = "127.0.0.1";  // Server IP address
    public static int PORT = 4444;             // Server port
}
```

## Architecture

The server listens on a TCP port. The client connects to the server. All communication is encrypted with SSL/TLS. Messages are serialized with MessagePack and compressed with GZip.

## What I Learned

- TCP socket programming
- SSL/TLS encryption
- Custom protocol design
- Multi-threaded server handling
- Windows system APIs (WMI, registry)
- Async/await patterns in C#

## Disclaimer

This project is for educational purposes only. Do not use it on systems you do not own or have permission to manage. Unauthorized access to computer systems is illegal.

## License

MIT License
