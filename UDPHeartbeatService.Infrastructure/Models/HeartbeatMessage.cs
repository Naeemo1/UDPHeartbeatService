using System.Text.Json;
using UDPHeartbeatService.Infrastructure.Enum;

namespace UDPHeartbeatService.Infrastructure.Models
{
    public class HeartbeatMessage : BaseEntity
    {
        public HeartbeatType Type { get; set; }
        public long SequenceNumber { get; set; }
        public long Timestamp { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();

        public byte[] Serialize()
        {
            try
            {
				return JsonSerializer.SerializeToUtf8Bytes(this);

			}
			catch (Exception)
            {

				return default!;
			}
		}

        public static HeartbeatMessage? Deserialize(byte[] data)
        {
            try
            {
                return JsonSerializer.Deserialize<HeartbeatMessage>(data);

            }
            catch (Exception)
            {

                return default!;
            }
        }

    }
}
