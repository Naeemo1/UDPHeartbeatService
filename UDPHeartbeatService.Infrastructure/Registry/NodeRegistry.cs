using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UDPHeartbeatService.Infrastructure.Enum;
using UDPHeartbeatService.Infrastructure.Models;

namespace UDPHeartbeatService.Infrastructure.Registry
{
    public class NodeRegistry
    {
        private readonly ConcurrentDictionary<string, NodeState> _nodes = new();

        public event EventHandler<NodeState>? NodeAdded;
        public event EventHandler<NodeState>? NodeUpdated;
        public event EventHandler<NodeState>? NodeRemoved;

        public void AddOrUpdate(string nodeId, string address, int port, Dictionary<string, string>? metadata = null)
        {
            bool isNew = !_nodes.ContainsKey(nodeId);

            _nodes.AddOrUpdate(nodeId,
                _ =>
                {
                    var node = new NodeState
                    {
                        NodeId = nodeId,
                        Address = address,
                        Port = port,
                        Status = NodeStatus.Alive,
                        LastHeartbeat = DateTime.UtcNow,
                        MissedHeartbeats = 0,
                        Metadata = metadata ?? new()
                    };
                    return node;
                },
                (_, existing) =>
                {
                    existing.Status = NodeStatus.Alive;
                    existing.LastHeartbeat = DateTime.UtcNow;
                    existing.MissedHeartbeats = 0;
                    existing.Address = address;
                    existing.Port = port;
                    if (metadata != null)
                        existing.Metadata = metadata;
                    return existing;
                });

            var nodeState = _nodes[nodeId];

            if (isNew)
                NodeAdded?.Invoke(this, nodeState);
            else
                NodeUpdated?.Invoke(this, nodeState);
        }

        public void IncrementMissedHeartbeat(string nodeId)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
            {
                node.MissedHeartbeats++;
            }
        }

        public void SetStatus(string nodeId, NodeStatus status)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
            {
                node.Status = status;
                NodeUpdated?.Invoke(this, node);
            }
        }

        public void Remove(string nodeId)
        {
            if (_nodes.TryRemove(nodeId, out var node))
            {
                NodeRemoved?.Invoke(this, node);
            }
        }

        public NodeState? Get(string nodeId) =>
            _nodes.TryGetValue(nodeId, out var node) ? node : null;

        public IEnumerable<NodeState> GetAll() => _nodes.Values;

        public int Count => _nodes.Count;

    }
}
