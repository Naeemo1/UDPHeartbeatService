using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDPHeartbeatService.Infrastructure.Configuration
{
	public class HeartbeatServerConfiguration
	{
		public int ListenPort { get; set; } = 5000;
		public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(3);
		public int MaxMissedHeartbeats { get; set; } = 3;
		public int SuspectThreshold { get; set; } = 2;
		public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(1);
	}

	public class HeartbeatClientConfiguration
	{
		public string NodeId { get; set; } = Guid.NewGuid().ToString("N")[..8];
		public string ServerAddress { get; set; } = "127.0.0.1";
		public int ServerPort { get; set; } = 5000;
		public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(1);
		public Dictionary<string, string> Metadata { get; set; } = new();
	}
}
