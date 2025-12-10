using UDPHeartbeatService.Client;
using UDPHeartbeatService.Infrastructure.Configuration;

var nodeId = args.Length > 0 ? args[0] : $"client-{Random.Shared.Next(1000, 9999)}";
var serverAddress = args.Length > 1 ? args[1] : "127.0.0.1";
var serverPort = args.Length > 2 ? int.Parse(args[2]) : 5000;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
var config = new HeartbeatClientConfiguration
{
	NodeId = nodeId,
	ServerAddress = serverAddress,
	ServerPort = serverPort,
	HeartbeatInterval = TimeSpan.FromSeconds(1),
	Metadata = new Dictionary<string, string>
	{
		["hostname"] = Environment.MachineName,
		["os"] = Environment.OSVersion.ToString(),
		["started"] = DateTime.UtcNow.ToString("O")
	}
};

// Register services
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<HeartbeatClient>();
builder.Services.AddHostedService<HeartbeatClient>(sp => sp.GetRequiredService<HeartbeatClient>());

var host = builder.Build();

// Get client instance and subscribe to events
var client = host.Services.GetRequiredService<HeartbeatClient>();

client.Connected += (_, _) =>
{
	Console.ForegroundColor = ConsoleColor.Green;
	Console.WriteLine("[✓] Connected to server!");
	Console.ResetColor();
};

client.Disconnected += (_, _) =>
{
	Console.ForegroundColor = ConsoleColor.Yellow;
	Console.WriteLine("[!] Disconnected from server");
	Console.ResetColor();
};

client.PongReceived += (_, msg) =>
{
	Console.WriteLine($"  <- PONG received (seq: {msg.SequenceNumber})");
};

// Health update task (example: send CPU/memory stats)
_ = Task.Run(async () =>
{
	while (true)
	{
		await Task.Delay(10000);

		if (client.IsConnected)
		{
			await client.SendHealthUpdateAsync(new Dictionary<string, string>
			{
				["memory_mb"] = (GC.GetTotalMemory(false) / 1024 / 1024).ToString(),
				["uptime_sec"] = Environment.TickCount64.ToString()
			});
			Console.WriteLine("  -> Health update sent");
		}
	}
});

Console.WriteLine($"=== Heartbeat Client Started ===");
Console.WriteLine($"Node ID: {nodeId}");
Console.WriteLine($"Server: {serverAddress}:{serverPort}");
Console.WriteLine("Press Ctrl+C to stop\n");

await host.RunAsync();
