using UDPHeartbeatService.Infrastructure.Configuration;
using UDPHeartbeatService.Infrastructure.Enum;
using UDPHeartbeatService.Infrastructure.Registry;
using UDPHeartbeatService.Server;

Console.Title = "UDP Heartbeat Server";

var builder = Host.CreateApplicationBuilder(args);

// Configuration - can be overridden by command line args
var port = args.Length > 0 ? int.Parse(args[0]) : 5000;

var config = new HeartbeatServerConfiguration
{
	ListenPort = port,
	HeartbeatTimeout = TimeSpan.FromSeconds(3),
	MaxMissedHeartbeats = 3,
	SuspectThreshold = 2,
	HealthCheckInterval = TimeSpan.FromSeconds(1)
};

// Register services
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<NodeRegistry>();
builder.Services.AddSingleton<HeartbeatServer>();
builder.Services.AddHostedService<HeartbeatServer>(sp => sp.GetRequiredService<HeartbeatServer>());

// Configure logging
builder.Logging.SetMinimumLevel(LogLevel.Information);

var host = builder.Build();

// Get server instance and subscribe to events
var server = host.Services.GetRequiredService<HeartbeatServer>();

server.NodeJoined += (_, node) =>
{
	Console.ForegroundColor = ConsoleColor.Green;
	Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ NODE JOINED: {node.NodeId} ({node.Address}:{node.Port})");
	Console.ResetColor();
	PrintNodeCount(server);
};

server.NodeLeft += (_, node) =>
{
	Console.ForegroundColor = ConsoleColor.Yellow;
	Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 👋 NODE LEFT: {node.NodeId} (graceful)");
	Console.ResetColor();
	PrintNodeCount(server);
};

server.NodeSuspected += (_, node) =>
{
	Console.ForegroundColor = ConsoleColor.DarkYellow;
	Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️  NODE SUSPECTED: {node.NodeId} (missed: {node.MissedHeartbeats})");
	Console.ResetColor();
};

server.NodeDied += (_, node) =>
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ NODE DEAD: {node.NodeId} (missed: {node.MissedHeartbeats})");
	Console.ResetColor();
	PrintNodeCount(server);
};

server.NodeRevived += (_, node) =>
{
	Console.ForegroundColor = ConsoleColor.Cyan;
	Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 NODE REVIVED: {node.NodeId}");
	Console.ResetColor();
	PrintNodeCount(server);
};

// Status display task
var statusCts = new CancellationTokenSource();
_ = Task.Run(async () =>
{
	while (!statusCts.Token.IsCancellationRequested)
	{
		await Task.Delay(10000, statusCts.Token);
		PrintAllNodes(server);
	}
}, statusCts.Token);

// Print startup info
PrintHeader(config);

// Handle graceful shutdown
Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	Console.WriteLine("\nShutting down...");
	statusCts.Cancel();
};

await host.RunAsync();

// Helper methods
static void PrintHeader(HeartbeatServerConfiguration config)
{
	Console.ForegroundColor = ConsoleColor.White;
	Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
	Console.WriteLine("║           UDP HEARTBEAT SERVER                        ║");
	Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
	Console.WriteLine($"║  Port:              {config.ListenPort,-35}║");
	Console.WriteLine($"║  Heartbeat Timeout: {config.HeartbeatTimeout.TotalSeconds,-35:F1}s║");
	Console.WriteLine($"║  Suspect After:     {config.SuspectThreshold,-35} missed║");
	Console.WriteLine($"║  Dead After:        {config.MaxMissedHeartbeats,-35} missed║");
	Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
	Console.WriteLine("║  Press Ctrl+C to stop                                 ║");
	Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
	Console.ResetColor();
	Console.WriteLine();
}

static void PrintNodeCount(HeartbeatServer server)
{
	Console.ForegroundColor = ConsoleColor.DarkGray;
	Console.WriteLine($"    Connected: {server.AliveNodesCount} alive, {server.ConnectedNodesCount} total");
	Console.ResetColor();
}

static void PrintAllNodes(HeartbeatServer server)
{
	var nodes = server.GetAllNodes().ToList();

	if (nodes.Count == 0)
	{
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] No connected nodes\n");
		Console.ResetColor();
		return;
	}

	Console.ForegroundColor = ConsoleColor.White;
	Console.WriteLine($"\n╔═══════════════════════════════════════════════════════╗");
	Console.WriteLine($"║  CONNECTED NODES ({nodes.Count})                                  ");
	Console.WriteLine($"╠═══════════════════════════════════════════════════════╣");

	foreach (var node in nodes.OrderBy(n => n.NodeId))
	{
		var statusIcon = node.Status switch
		{
			NodeStatus.Alive => "🟢",
			NodeStatus.Suspected => "🟡",
			NodeStatus.Dead => "🔴",
			_ => "⚪"
		};

		var statusColor = node.Status switch
		{
			NodeStatus.Alive => ConsoleColor.Green,
			NodeStatus.Suspected => ConsoleColor.Yellow,
			NodeStatus.Dead => ConsoleColor.Red,
			_ => ConsoleColor.Gray
		};

		Console.ForegroundColor = statusColor;
		Console.WriteLine($"║  {statusIcon} {node.NodeId,-15} {node.Address}:{node.Port,-10} {node.Status,-10} ({node.TimeSinceLastHeartbeat.TotalSeconds:F1}s ago)");
	}

	Console.ForegroundColor = ConsoleColor.White;
	Console.WriteLine($"╚═══════════════════════════════════════════════════════╝\n");
	Console.ResetColor();
}