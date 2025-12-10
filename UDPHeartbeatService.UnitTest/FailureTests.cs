using Xunit.Abstractions;
using Microsoft.Extensions.Logging;
using UDPHeartbeatService.Client;
using UDPHeartbeatService.Infrastructure.Configuration;
using UDPHeartbeatService.Infrastructure.Enum;
using UDPHeartbeatService.Infrastructure.Registry;
using UDPHeartbeatService.Server;

namespace UdpHeartbeat.Tests;

public class FailureTests : IAsyncLifetime
{
	private readonly ITestOutputHelper _output;
	private HeartbeatServer _server = null!;
	private NodeRegistry _registry = null!;
	private CancellationTokenSource _cts = null!;

	// Use unique port for each test run to avoid conflicts
	private readonly int _testPort;

	public FailureTests(ITestOutputHelper output)
	{
		_output = output;
		_testPort = Random.Shared.Next(10000, 60000);
	}

	public async Task InitializeAsync()
	{
		_cts = new CancellationTokenSource();

		var serverConfig = new HeartbeatServerConfiguration
		{
			ListenPort = _testPort,
			HeartbeatTimeout = TimeSpan.FromMilliseconds(300),
			MaxMissedHeartbeats = 3,
			SuspectThreshold = 2,
			HealthCheckInterval = TimeSpan.FromMilliseconds(100)
		};

		_registry = new NodeRegistry();

		// Create logger that outputs to test console
		var loggerFactory = LoggerFactory.Create(builder =>
		{
			builder.AddDebug();
			builder.SetMinimumLevel(LogLevel.Debug);
		});

		_server = new HeartbeatServer(
			serverConfig,
			_registry,
			loggerFactory.CreateLogger<HeartbeatServer>());

		await _server.StartAsync(_cts.Token);

		// Give server time to start
		await Task.Delay(200);

		_output.WriteLine($"Server started on port {_testPort}");
	}

	public async Task DisposeAsync()
	{
		_cts.Cancel();

		try
		{
			await _server.StopAsync(CancellationToken.None);
		}
		catch { }

		_server.Dispose();
		_cts.Dispose();

		_output.WriteLine("Server stopped");
	}

	private HeartbeatClient CreateClient(string nodeId)
	{
		var clientConfig = new HeartbeatClientConfiguration
		{
			NodeId = nodeId,
			ServerAddress = "127.0.0.1",
			ServerPort = _testPort,
			HeartbeatInterval = TimeSpan.FromMilliseconds(100)
		};

		var loggerFactory = LoggerFactory.Create(builder =>
		{
			builder.AddDebug();
			builder.SetMinimumLevel(LogLevel.Debug);
		});

		return new HeartbeatClient(clientConfig, loggerFactory.CreateLogger<HeartbeatClient>());
	}

	[Fact]
	public async Task Test1_Client_Joins_Successfully()
	{
		// Arrange
		var nodeId = $"join-test-{Guid.NewGuid():N}";
		var client = CreateClient(nodeId);
		var joinedEvent = new TaskCompletionSource<bool>();

		_server.NodeJoined += (_, node) =>
		{
			if (node.NodeId == nodeId)
			{
				_output.WriteLine($"NodeJoined event received for {nodeId}");
				joinedEvent.TrySetResult(true);
			}
		};

		try
		{
			// Act
			await client.StartAsync(_cts.Token);

			// Wait for join event with timeout
			var joined = await Task.WhenAny(
				joinedEvent.Task,
				Task.Delay(5000)
			) == joinedEvent.Task;

			// Assert
			Assert.True(joined, "Node should have joined within timeout");

			var node = _registry.Get(nodeId);
			Assert.NotNull(node);
			Assert.Equal(NodeStatus.Alive, node.Status);

			_output.WriteLine($"Test passed: Node {nodeId} joined successfully");
		}
		finally
		{
			await client.StopAsync(CancellationToken.None);
			client.Dispose();
		}
	}

	[Fact]
	public async Task Test2_Client_BecomesSuspected_WhenStopped()
	{
		// Arrange
		var nodeId = $"suspect-test-{Guid.NewGuid():N}";
		var client = CreateClient(nodeId);
		var suspectedEvent = new TaskCompletionSource<bool>();
		var joinedEvent = new TaskCompletionSource<bool>();

		_server.NodeJoined += (_, node) =>
		{
			if (node.NodeId == nodeId)
			{
				_output.WriteLine($"NodeJoined: {nodeId}");
				joinedEvent.TrySetResult(true);
			}
		};

		_server.NodeSuspected += (_, node) =>
		{
			if (node.NodeId == nodeId)
			{
				_output.WriteLine($"NodeSuspected: {nodeId}, missed: {node.MissedHeartbeats}");
				suspectedEvent.TrySetResult(true);
			}
		};

		try
		{
			// Start client
			await client.StartAsync(_cts.Token);

			// Wait for join
			var joined = await Task.WhenAny(joinedEvent.Task, Task.Delay(3000)) == joinedEvent.Task;
			Assert.True(joined, "Node should have joined");

			_output.WriteLine("Client joined, now disposing to simulate failure...");

			// Dispose client WITHOUT sending LEAVE (simulate crash)
			client.Dispose();

			// Wait for suspected event
			var suspected = await Task.WhenAny(
				suspectedEvent.Task,
				Task.Delay(5000)
			) == suspectedEvent.Task;

			// Assert
			Assert.True(suspected, "Node should have been suspected within timeout");

			var node = _registry.Get(nodeId);
			Assert.NotNull(node);
			Assert.True(
				node.Status == NodeStatus.Suspected || node.Status == NodeStatus.Dead,
				$"Node status should be Suspected or Dead, but was {node.Status}"
			);

			_output.WriteLine($"Test passed: Node {nodeId} was suspected");
		}
		finally
		{
			try { client.Dispose(); } catch { }
		}
	}

	[Fact]
	public async Task Test3_Client_BecomesDead_AfterMaxMissedHeartbeats()
	{
		// Arrange
		var nodeId = $"dead-test-{Guid.NewGuid():N}";
		var client = CreateClient(nodeId);
		var deadEvent = new TaskCompletionSource<bool>();
		var joinedEvent = new TaskCompletionSource<bool>();

		_server.NodeJoined += (_, node) =>
		{
			if (node.NodeId == nodeId)
			{
				_output.WriteLine($"NodeJoined: {nodeId}");
				joinedEvent.TrySetResult(true);
			}
		};

		_server.NodeDied += (_, node) =>
		{
			if (node.NodeId == nodeId)
			{
				_output.WriteLine($"NodeDied: {nodeId}, missed: {node.MissedHeartbeats}");
				deadEvent.TrySetResult(true);
			}
		};

		try
		{
			// Start client
			await client.StartAsync(_cts.Token);

			// Wait for join
			var joined = await Task.WhenAny(joinedEvent.Task, Task.Delay(3000)) == joinedEvent.Task;
			Assert.True(joined, "Node should have joined");

			_output.WriteLine("Client joined, now disposing to simulate crash...");

			// Kill client without LEAVE
			client.Dispose();

			// Wait for dead event (longer timeout)
			var died = await Task.WhenAny(
				deadEvent.Task,
				Task.Delay(10000)
			) == deadEvent.Task;

			// Assert
			Assert.True(died, "Node should have died within timeout");

			var node = _registry.Get(nodeId);
			Assert.NotNull(node);
			Assert.Equal(NodeStatus.Dead, node.Status);

			_output.WriteLine($"Test passed: Node {nodeId} is dead");
		}
		finally
		{
			try { client.Dispose(); } catch { }
		}
	}

	[Fact]
	public async Task Test4_Client_Revives_WhenReconnected()
	{
		// Arrange
		var nodeId = $"revive-test-{Guid.NewGuid():N}";
		var client1 = CreateClient(nodeId);

		var joinedEvent = new TaskCompletionSource<bool>();
		var deadEvent = new TaskCompletionSource<bool>();
		var revivedOrRejoinedEvent = new TaskCompletionSource<bool>();
		var isFirstJoin = true;

		_server.NodeJoined += (_, node) =>
		{
			if (node.NodeId == nodeId)
			{
				if (isFirstJoin)
				{
					_output.WriteLine($"NodeJoined (first): {nodeId}");
					isFirstJoin = false;
					joinedEvent.TrySetResult(true);
				}
				else
				{
					// Second join = revival
					_output.WriteLine($"NodeJoined (reconnect/revival): {nodeId}");
					revivedOrRejoinedEvent.TrySetResult(true);
				}
			}
		};

		_server.NodeDied += (_, node) =>
		{
			if (node.NodeId == nodeId)
			{
				_output.WriteLine($"NodeDied: {nodeId}");
				deadEvent.TrySetResult(true);
			}
		};

		_server.NodeRevived += (_, node) =>
		{
			if (node.NodeId == nodeId)
			{
				_output.WriteLine($"NodeRevived: {nodeId}");
				revivedOrRejoinedEvent.TrySetResult(true);
			}
		};

		try
		{
			// Step 1: Start client
			await client1.StartAsync(_cts.Token);
			var joined = await Task.WhenAny(joinedEvent.Task, Task.Delay(3000)) == joinedEvent.Task;
			Assert.True(joined, "Node should have joined");

			// Step 2: Kill client
			_output.WriteLine("Killing client...");
			client1.Dispose();

			// Step 3: Wait for dead
			var died = await Task.WhenAny(deadEvent.Task, Task.Delay(10000)) == deadEvent.Task;
			Assert.True(died, "Node should have died");

			var deadNode = _registry.Get(nodeId);
			Assert.Equal(NodeStatus.Dead, deadNode!.Status);

			// Step 4: Create new client with same ID
			_output.WriteLine("Creating new client with same ID...");
			var client2 = CreateClient(nodeId);
			await client2.StartAsync(_cts.Token);

			// Step 5: Wait for revived OR rejoined
			var revived = await Task.WhenAny(
				revivedOrRejoinedEvent.Task,
				Task.Delay(5000)
			) == revivedOrRejoinedEvent.Task;

			Assert.True(revived, "Node should have revived or rejoined");

			// Node should be alive again
			var aliveNode = _registry.Get(nodeId);
			Assert.Equal(NodeStatus.Alive, aliveNode!.Status);

			_output.WriteLine($"Test passed: Node {nodeId} is alive again");

			await client2.StopAsync(CancellationToken.None);
			client2.Dispose();
		}
		finally
		{
			try { client1.Dispose(); } catch { }
		}
	}

	[Fact]
	public async Task Test5_StateTransition_Alive_To_Suspected_To_Dead()
	{
		// Arrange
		var nodeId = $"transition-test-{Guid.NewGuid():N}";
		var client = CreateClient(nodeId);

		var stateChanges = new List<(NodeStatus Status, DateTime Time)>();
		var joinedEvent = new TaskCompletionSource<bool>();
		var deadEvent = new TaskCompletionSource<bool>();

		_server.NodeJoined += (_, node) =>
		{
			if (node.NodeId == nodeId)
			{
				stateChanges.Add((NodeStatus.Alive, DateTime.UtcNow));
				_output.WriteLine($"State: ALIVE at {DateTime.UtcNow:HH:mm:ss.fff}");
				joinedEvent.TrySetResult(true);
			}
		};

		_server.NodeSuspected += (_, node) =>
		{
			if (node.NodeId == nodeId)
			{
				stateChanges.Add((NodeStatus.Suspected, DateTime.UtcNow));
				_output.WriteLine($"State: SUSPECTED at {DateTime.UtcNow:HH:mm:ss.fff}");
			}
		};

		_server.NodeDied += (_, node) =>
		{
			if (node.NodeId == nodeId)
			{
				stateChanges.Add((NodeStatus.Dead, DateTime.UtcNow));
				_output.WriteLine($"State: DEAD at {DateTime.UtcNow:HH:mm:ss.fff}");
				deadEvent.TrySetResult(true);
			}
		};

		try
		{
			// Start client
			await client.StartAsync(_cts.Token);
			await Task.WhenAny(joinedEvent.Task, Task.Delay(3000));

			// Kill client
			_output.WriteLine("Disposing client...");
			client.Dispose();

			// Wait for dead
			await Task.WhenAny(deadEvent.Task, Task.Delay(10000));

			// Assert state transitions
			_output.WriteLine($"Total state changes: {stateChanges.Count}");
			foreach (var change in stateChanges)
			{
				_output.WriteLine($"  {change.Status} at {change.Time:HH:mm:ss.fff}");
			}

			Assert.True(stateChanges.Count >= 2, "Should have at least 2 state changes");
			Assert.Equal(NodeStatus.Alive, stateChanges[0].Status);
			Assert.Equal(NodeStatus.Dead, stateChanges[^1].Status);

			// Check order
			if (stateChanges.Count >= 3)
			{
				var suspectedIndex = stateChanges.FindIndex(s => s.Status == NodeStatus.Suspected);
				var deadIndex = stateChanges.FindIndex(s => s.Status == NodeStatus.Dead);
				Assert.True(suspectedIndex < deadIndex, "Suspected should come before Dead");
			}

			_output.WriteLine("Test passed: State transitions correct");
		}
		finally
		{
			try { client.Dispose(); } catch { }
		}
	}
}