using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDPHeartbeatService.Infrastructure.Enum
{
	public enum NodeStatus
	{
		Unknown,
		Alive,
		Suspected,
		Dead
	}
}
