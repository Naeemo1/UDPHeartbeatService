using System.Net;
using System.Net.Sockets;
using UDPHeartbeatService.Infrastructure.Configuration;
using UDPHeartbeatService.Infrastructure.Enum;
using UDPHeartbeatService.Infrastructure.Models;
using UDPHeartbeatService.Infrastructure.Registry;

namespace UDPHeartbeatService.Server;

public class HeartbeatServer : BackgroundService
{
	private readonly HeartbeatServerConfiguration _config;
	private readonly NodeRegistry _registry;
	private readonly ILogger<HeartbeatServer> _logger;
	private UdpClient? _udpClient;

	public event EventHandler<NodeState>? NodeJoined;
	public event EventHandler<NodeState>? NodeLeft;
	public event EventHandler<NodeState>? NodeDied;
	public event EventHandler<NodeState>? NodeSuspected;
	public event EventHandler<NodeState>? NodeRevived;

	public HeartbeatServer(
		HeartbeatServerConfiguration config,
		NodeRegistry registry,
		ILogger<HeartbeatServer> logger)
	{
		_config = config;
		_registry = registry;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_udpClient = new UdpClient(_config.ListenPort);

		_logger.LogInformation("Heartbeat Server started on port {Port}", _config.ListenPort);

		var receiveTask = ReceiveMessagesAsync(stoppingToken);
		var healthCheckTask = HealthCheckLoopAsync(stoppingToken);

		await Task.WhenAll(receiveTask, healthCheckTask);
	}

	private async Task ReceiveMessagesAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				var result = await _udpClient!.ReceiveAsync(ct);
				var message = HeartbeatMessage.Deserialize(result.Buffer);

				if (message == null)
					continue;

				await ProcessMessageAsync(message, result.RemoteEndPoint);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error receiving message");
			}
		}
	}

	private async Task ProcessMessageAsync(HeartbeatMessage message, IPEndPoint sender)
	{
		// IMPORTANT: Check previous state BEFORE updating registry
		var previousNode = _registry.Get(message.NodeId);
		var wasDeadOrSuspected = previousNode?.Status is NodeStatus.Dead or NodeStatus.Suspected;
		var isNewNode = previousNode == null;

		switch (message.Type)
		{
			case HeartbeatType.Join:
				_logger.LogInformation("Node {NodeId} JOIN from {Address}:{Port}",
					message.NodeId, sender.Address, sender.Port);

				// Update registry (resets status to Alive)
				_registry.AddOrUpdate(message.NodeId, sender.Address.ToString(), sender.Port, message.Metadata);

				// Send acknowledgment
				await SendResponseAsync(new HeartbeatMessage
				{
					Type = HeartbeatType.Pong,
					NodeId = "SERVER",
					SequenceNumber = message.SequenceNumber,
					Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
				}, sender);

				// FIX: Fire correct event based on previous state
				var joinedNode = _registry.Get(message.NodeId)!;

				if (wasDeadOrSuspected)
				{
					_logger.LogInformation("Node {NodeId} REVIVED (was {Status})",
						message.NodeId, previousNode!.Status);
					NodeRevived?.Invoke(this, joinedNode);
				}
				else
				{
					NodeJoined?.Invoke(this, joinedNode);
				}
				break;

			case HeartbeatType.Leave:
				_logger.LogInformation("Node {NodeId} LEFT gracefully", message.NodeId);
				var leavingNode = _registry.Get(message.NodeId);
				if (leavingNode != null)
				{
					_registry.Remove(message.NodeId);
					NodeLeft?.Invoke(this, leavingNode);
				}
				break;

			case HeartbeatType.Ping:
				// Update registry
				_registry.AddOrUpdate(message.NodeId, sender.Address.ToString(), sender.Port, message.Metadata);

				// Send Pong response
				await SendResponseAsync(new HeartbeatMessage
				{
					Type = HeartbeatType.Pong,
					NodeId = "SERVER",
					SequenceNumber = message.SequenceNumber,
					Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
				}, sender);

				// FIX: Check for revival on PING as well
				if (wasDeadOrSuspected)
				{
					var revivedNode = _registry.Get(message.NodeId);
					if (revivedNode != null)
					{
						_logger.LogInformation("Node {NodeId} REVIVED via PING", message.NodeId);
						NodeRevived?.Invoke(this, revivedNode);
					}
				}
				else if (isNewNode)
				{
					// New node connected via PING (without JOIN)
					var newNode = _registry.Get(message.NodeId);
					if (newNode != null)
					{
						_logger.LogInformation("Node {NodeId} connected via PING", message.NodeId);
						NodeJoined?.Invoke(this, newNode);
					}
				}
				break;

			case HeartbeatType.Health:
				_registry.AddOrUpdate(message.NodeId, sender.Address.ToString(), sender.Port, message.Metadata);

				// FIX: Check for revival on HEALTH message
				if (wasDeadOrSuspected)
				{
					_logger.LogInformation("Node {NodeId} REVIVED via HEALTH", message.NodeId);
					NodeRevived?.Invoke(this, _registry.Get(message.NodeId)!);
				}

				_logger.LogDebug("Health update from {NodeId}: {Metadata}",
					message.NodeId, string.Join(", ", message.Metadata.Select(kv => $"{kv.Key}={kv.Value}")));
				break;
		}
	}

	private async Task HealthCheckLoopAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			await Task.Delay(_config.HealthCheckInterval, ct);

			foreach (var node in _registry.GetAll().ToList())
			{
				if (node.TimeSinceLastHeartbeat > _config.HeartbeatTimeout)
				{
					_registry.IncrementMissedHeartbeat(node.NodeId);

					if (node.MissedHeartbeats >= _config.MaxMissedHeartbeats && node.Status != NodeStatus.Dead)
					{
						_registry.SetStatus(node.NodeId, NodeStatus.Dead);
						_logger.LogWarning("Node {NodeId} marked as DEAD (missed {Count} heartbeats)",
							node.NodeId, node.MissedHeartbeats);
						NodeDied?.Invoke(this, node);
					}
					else if (node.MissedHeartbeats >= _config.SuspectThreshold && node.Status == NodeStatus.Alive)
					{
						_registry.SetStatus(node.NodeId, NodeStatus.Suspected);
						_logger.LogWarning("Node {NodeId} is SUSPECTED (missed {Count} heartbeats)",
							node.NodeId, node.MissedHeartbeats);
						NodeSuspected?.Invoke(this, node);
					}
				}
			}
		}
	}

	private async Task SendResponseAsync(HeartbeatMessage message, IPEndPoint endpoint)
	{
		var data = message.Serialize();
		await _udpClient!.SendAsync(data, data.Length, endpoint);
	}

	public IEnumerable<NodeState> GetAllNodes() => _registry.GetAll();

	public NodeState? GetNode(string nodeId) => _registry.Get(nodeId);

	public int ConnectedNodesCount => _registry.Count;

	public int AliveNodesCount => _registry.GetAll().Count(n => n.Status == NodeStatus.Alive);

	public override void Dispose()
	{
		_udpClient?.Dispose();
		base.Dispose();
	}
}