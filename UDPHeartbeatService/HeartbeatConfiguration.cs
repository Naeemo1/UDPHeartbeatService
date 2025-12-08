using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDPHeartbeatService
{
	public class HeartbeatConfiguration
	{
		public string NodeId { get; set; } = Guid.NewGuid().ToString("N")[..8];
		public int ListenPort { get; set; } = 5000;
		public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(1);
		public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(3);
		public int MaxMissedHeartbeats { get; set; } = 3;
		public int SuspectThreshold { get; set; } = 2;
	}
}
