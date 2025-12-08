using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UDPHeartbeatService.Infrastructure.Enum;

namespace UDPHeartbeatService.Infrastructure
{
	public class NodeState : BaseEntity
	{
		public string? Address { get; set; }
		public int Port { get; set; }
		public NodeStatus Status { get; set; } = NodeStatus.Unknown;
		public DateTime LastHeartbeat { get; set; }
		public long LastSequenceNumber { get; set; }
		public int MissedHeartbeats { get; set; }
		public TimeSpan TimeSinceLastHeartbeat => DateTime.UtcNow - LastHeartbeat;

	}
}
