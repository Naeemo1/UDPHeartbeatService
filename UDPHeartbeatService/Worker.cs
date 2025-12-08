using System.Net;
using System.Net.Sockets;
using UDPHeartbeatService.Infrastructure;
using UDPHeartbeatService.Infrastructure.Enum;

namespace UDPHeartbeatService
{
	public class Worker : BackgroundService
	{
		private readonly HeartbeatConfiguration _config;
		private readonly NodeRegistry _registry;
		private readonly ILogger<Worker> _logger;
		private UdpClient? _udpClient;
		private long _sequenceNumber;

		public event EventHandler<NodeState>? NodeDied;
		public event EventHandler<NodeState>? NodeRevived;
		public event EventHandler<NodeState>? NodeSuspected;

		public Worker(HeartbeatConfiguration config, NodeRegistry registry, ILogger<Worker> logger)
		{
			_config = config;
			_registry = registry;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_udpClient = new UdpClient(_config.ListenPort);
			_logger.LogInformation("Heartbeat service started. NodeId: {NodeId}, Port: {Port}",	_config.NodeId, _config.ListenPort);

			// Start concurrent tasks
			var receiveTask = ReceiveHeartbeatsAsync(stoppingToken);
			var sendTask = SendHeartbeatsAsync(stoppingToken);
			var checkTask = CheckNodeHealthAsync(stoppingToken);

			await Task.WhenAll(receiveTask, sendTask, checkTask);
		}

		private async Task ReceiveHeartbeatsAsync(CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				try
				{
					var result = await _udpClient!.ReceiveAsync(ct);
					var message = HeartbeatMessage.Deserialize(result.Buffer);

					if (message == null || message.NodeId == _config.NodeId)
						continue;

					await ProcessHeartbeatAsync(message, result.RemoteEndPoint);
				}
				catch (OperationCanceledException) { break; }
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error receiving heartbeat");
				}
			}
		}

		private async Task ProcessHeartbeatAsync(HeartbeatMessage message, IPEndPoint sender)
		{
			var previousState = _registry.GetNode(message.NodeId);
			bool wasDeadOrSuspected = previousState?.Status is NodeStatus.Dead or NodeStatus.Suspected;

			_registry.UpdateAddNode(message.NodeId, sender.Address.ToString(), sender.Port);

			// Node came back to life
			if (wasDeadOrSuspected)
			{
				var node = _registry.GetNode(message.NodeId);
				if (node != null)
				{
					_logger.LogInformation("Node {NodeId} revived", message.NodeId);
					NodeRevived?.Invoke(this, node);
				}
			}

			// Respond to pings with pongs
			if (message.Type == HeartbeatType.Ping)
			{
				var pong = new HeartbeatMessage
				{
					Type = HeartbeatType.Pong,
					NodeId = _config.NodeId,
					SequenceNumber = message.SequenceNumber,
					Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
				};

				await SendMessageAsync(pong, sender);
			}

			_logger.LogDebug(
				"Received {Type} from {NodeId} (seq: {Seq})",
				message.Type, message.NodeId, message.SequenceNumber);
		}

		private async Task SendHeartbeatsAsync(CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				try
				{
					var heartbeat = new HeartbeatMessage
					{
						Type = HeartbeatType.Ping,
						NodeId = _config.NodeId,
						SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
						Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
					};

					// Send to all known nodes
					foreach (var node in _registry.GetAllNodes())
					{
						if (node.NodeId == _config.NodeId) continue;

						var endpoint = new IPEndPoint(
							IPAddress.Parse(node.Address),
							node.Port);

						await SendMessageAsync(heartbeat, endpoint);
					}

					await Task.Delay(_config.HeartbeatInterval, ct);
				}
				catch (OperationCanceledException) { break; }
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error sending heartbeats");
				}
			}
		}
		private async Task CheckNodeHealthAsync(CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				foreach (var node in _registry.GetAllNodes())
				{
					if (node.NodeId == _config.NodeId) continue;

					if (node.TimeSinceLastHeartbeat > _config.HeartbeatTimeout)
					{
						_registry.IncrementMissedHeartbeat(node.NodeId);

						if (node.MissedHeartbeats >= _config.MaxMissedHeartbeats
							&& node.Status != NodeStatus.Dead)
						{
							_registry.UpdateStatus(node.NodeId, NodeStatus.Dead);
							_logger.LogWarning("Node {NodeId} marked as DEAD", node.NodeId);
							NodeDied?.Invoke(this, node);
						}
						else if (node.MissedHeartbeats >= _config.SuspectThreshold
								 && node.Status == NodeStatus.Alive)
						{
							_registry.UpdateStatus(node.NodeId, NodeStatus.Suspected);
							_logger.LogWarning("Node {NodeId} is SUSPECTED", node.NodeId);
							NodeSuspected?.Invoke(this, node);
						}
					}
				}

				await Task.Delay(_config.HeartbeatInterval, ct);
			}
		}

		private async Task SendMessageAsync(HeartbeatMessage message, IPEndPoint endpoint)
		{
			var data = message.Serialize();
			await _udpClient!.SendAsync(data, data.Length, endpoint);
		}

		public async Task JoinClusterAsync(string seedAddress, int seedPort)
		{
			var join = new HeartbeatMessage
			{
				Type = HeartbeatType.Join,
				NodeId = _config.NodeId,
				Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
			};

			var endpoint = new IPEndPoint(IPAddress.Parse(seedAddress), seedPort);
			await SendMessageAsync(join, endpoint);

			_registry.UpdateAddNode("seed", seedAddress, seedPort);
			_logger.LogInformation("Joining cluster via {Address}:{Port}", seedAddress, seedPort);
		}

		public override void Dispose()
		{
			_udpClient?.Dispose();
			base.Dispose();
		}

	}
}
