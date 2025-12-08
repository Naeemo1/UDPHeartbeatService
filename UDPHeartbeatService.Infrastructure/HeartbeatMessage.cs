using System.Text.Json;
using UDPHeartbeatService.Infrastructure.Enum;

namespace UDPHeartbeatService.Infrastructure
{
	public class HeartbeatMessage : BaseEntity
	{
		public HeartbeatType Type { get; set; }
		public long SequenceNumber { get; set; }
		public long Timestamp { get; set; }
		public Dictionary<string, string> MetaData { get; set; } = new();

		public byte[] Serialize()
		{
			return JsonSerializer.SerializeToUtf8Bytes(this);
		}

		public static HeartbeatMessage? Deserialize(byte[] data)
		{
			return JsonSerializer.Deserialize<HeartbeatMessage>(data);
		}

	}
}
