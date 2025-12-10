using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UDPHeartbeatService.Infrastructure.Configuration;
using UDPHeartbeatService.Infrastructure.Enum;
using UDPHeartbeatService.Infrastructure.Models;

namespace UDPHeartbeatService.Client
{
    public class HeartbeatClient : BackgroundService
	{
		private readonly HeartbeatClientConfiguration _config;
		private readonly ILogger<HeartbeatClient> _logger;
		private UdpClient? _udpClient;
		private IPEndPoint? _serverEndpoint;
		private long _sequenceNumber;
		private bool _isConnected;

		public event EventHandler? Connected;
		public event EventHandler? Disconnected;
		public event EventHandler<HeartbeatMessage>? PongReceived;

		public string NodeId => _config.NodeId;
		public bool IsConnected => _isConnected;

		public HeartbeatClient(
			HeartbeatClientConfiguration config,
			ILogger<HeartbeatClient> logger)
		{
			_config = config;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_udpClient = new UdpClient();
			_serverEndpoint = new IPEndPoint(
				IPAddress.Parse(_config.ServerAddress),
				_config.ServerPort);

			_logger.LogInformation("Heartbeat Client {NodeId} started, connecting to {Server}:{Port}",
				_config.NodeId, _config.ServerAddress, _config.ServerPort);

			// Send Join message
			await SendJoinAsync();

			// Start receive and send loops
			var receiveTask = ReceiveLoopAsync(stoppingToken);
			var sendTask = SendHeartbeatLoopAsync(stoppingToken);

			await Task.WhenAll(receiveTask, sendTask);
		}

		private async Task SendJoinAsync()
		{
			var joinMessage = new HeartbeatMessage
			{
				Type = HeartbeatType.Join,
				NodeId = _config.NodeId,
				SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
				Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
				Metadata = _config.Metadata
			};

			await SendMessageAsync(joinMessage);
			_logger.LogInformation("Sent JOIN request to server");
		}

		private async Task SendHeartbeatLoopAsync(CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				try
				{
					await Task.Delay(_config.HeartbeatInterval, ct);

					var heartbeat = new HeartbeatMessage
					{
						Type = HeartbeatType.Ping,
						NodeId = _config.NodeId,
						SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
						Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
						Metadata = _config.Metadata
					};

					await SendMessageAsync(heartbeat);
					_logger.LogDebug("Sent heartbeat seq:{Seq}", heartbeat.SequenceNumber);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error sending heartbeat");
				}
			}
		}

		private async Task ReceiveLoopAsync(CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				try
				{
					var result = await _udpClient!.ReceiveAsync(ct);
					var message = HeartbeatMessage.Deserialize(result.Buffer);

					if (message == null)
						continue;

					ProcessServerResponse(message);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error receiving response");
				}
			}
		}

		private void ProcessServerResponse(HeartbeatMessage message)
		{
			switch (message.Type)
			{
				case HeartbeatType.Pong:
					if (!_isConnected)
					{
						_isConnected = true;
						_logger.LogInformation("Connected to server");
						Connected?.Invoke(this, EventArgs.Empty);
					}

					var latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - message.Timestamp;
					_logger.LogDebug("Received PONG seq:{Seq}, latency:{Latency}ms",
						message.SequenceNumber, latency);

					PongReceived?.Invoke(this, message);
					break;
			}
		}

		private async Task SendMessageAsync(HeartbeatMessage message)
		{
			var data = message.Serialize();
			await _udpClient!.SendAsync(data, data.Length, _serverEndpoint);
		}

		public async Task SendHealthUpdateAsync(Dictionary<string, string> healthData)
		{
			var message = new HeartbeatMessage
			{
				Type = HeartbeatType.Health,
				NodeId = _config.NodeId,
				SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
				Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
				Metadata = healthData
			};

			await SendMessageAsync(message);
		}

		public async Task DisconnectAsync()
		{
			var leaveMessage = new HeartbeatMessage
			{
				Type = HeartbeatType.Leave,
				NodeId = _config.NodeId,
				SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
				Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
			};

			await SendMessageAsync(leaveMessage);
			_isConnected = false;
			_logger.LogInformation("Sent LEAVE message, disconnecting");
			Disconnected?.Invoke(this, EventArgs.Empty);
		}

		public void UpdateMetadata(string key, string value)
		{
			_config.Metadata[key] = value;
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await DisconnectAsync();
			await base.StopAsync(cancellationToken);
		}

		public override void Dispose()
		{
			_udpClient?.Dispose();
			base.Dispose();
		}
	}
}
