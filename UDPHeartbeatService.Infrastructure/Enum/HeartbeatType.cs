using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDPHeartbeatService.Infrastructure.Enum
{
	public enum HeartbeatType
	{
		Ping = 1,
		Pong = 2,
		Join = 3,
		Leave = 4
	}
}
