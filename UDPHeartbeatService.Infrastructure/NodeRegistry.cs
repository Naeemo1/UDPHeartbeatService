using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UDPHeartbeatService.Infrastructure.Enum;

namespace UDPHeartbeatService.Infrastructure
{
	public class NodeRegistry
	{
		private readonly ConcurrentDictionary<string, NodeState> _nodes = new();

		public void UpdateAddNode(string nodeId, string address, int port)
		{
			_nodes.AddOrUpdate(
				nodeId,
				_ = new NodeState
				{
					NodeId = nodeId,
					Address = address,
					Port = port,
					Status = NodeStatus.Alive,
					LastHeartbeat = DateTime.UtcNow
				},
				(_, existing) =>
				{
					existing.Status = NodeStatus.Alive;
					existing.LastHeartbeat = DateTime.UtcNow;
					existing.MissedHeartbeats = 0;
					return existing;
				});
		}

		public void IncrementMissedHeartbeat(string nodeId)
		{
			if (_nodes.TryGetValue(nodeId, out var node))
			{
				node.MissedHeartbeats++;
			}
		}

		public void UpdateStatus(string nodeId, NodeStatus status)
		{
			if (_nodes.TryGetValue(nodeId, out var node))
			{
				node.Status = status;
			}
		}

		public void RemoveNode(string nodeId) => _nodes.TryRemove(nodeId, out _);

		public IEnumerable<NodeState> GetAllNodes() => _nodes.Values;

		public NodeState? GetNode(string nodeId) =>	_nodes.TryGetValue(nodeId, out var node) ? node : null;

	}
}
