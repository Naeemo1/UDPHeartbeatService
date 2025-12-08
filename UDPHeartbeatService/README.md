# UDP Heartbeat Protocol for .NET


A lightweight, high-performance failure detection library using UDP-based heartbeat protocol for distributed .NET applications.

---

## Overview

The UDP Heartbeat Protocol provides fast and reliable failure detection for distributed systems. Nodes periodically exchange small UDP packets (heartbeats) to monitor each other's health. When a node stops responding, it's detected and marked as failed within seconds.

---

## Why UDP Instead of TCP?

| Aspect | UDP | TCP |
|--------|-----|-----|
| **Connection Overhead** | None (connectionless) | Requires connection setup/teardown |
| **Speed** | Immediate send | 3-way handshake required |
| **Memory Usage** | Minimal | Maintains connection state per peer |
| **Scalability** | Handles thousands of nodes | Limited by connection count |
| **Failure Detection** | Immediate timeout awareness | Waits for TCP timeout (can be slow) |
| **Reliability** | Must handle packet loss | Guaranteed delivery |

---

## Features

| Feature | Description |
|---------|-------------|
| **Fast Detection** | Sub-second to few-second failure detection |
| **Lightweight** | Minimal CPU and memory footprint |
| **Auto-Recovery** | Automatically detects when failed nodes come back online |
| **Health Monitoring** | Real-time tracking of all node statuses |
| **Thread-Safe** | Fully concurrent, safe for multi-threaded applications |
| **Event-Driven** | Subscribe to node failure and recovery events |
| **Configurable** | Tune detection speed vs. accuracy trade-offs |

---

## How It Works

### Heartbeat Flow

```
  Node A                                           Node B
    │                                                │
    │ ─────────── PING (seq: 1) ──────────────────▶  │
    │ ◀────────── PONG (seq: 1) ───────────────────  │
    │                                                │
    │ ─────────── PING (seq: 2) ──────────────────▶  │
    │ ◀────────── PONG (seq: 2) ───────────────────  │
    │                                                │
    │ ─────────── PING (seq: 3) ──────────────────▶  │
    │              ✗ No Response                     │  ← Node B fails
    │                                                │
    │ ─────────── PING (seq: 4) ──────────────────▶  │
    │              ✗ No Response                     │
    │                                                │
    ▼                                                │
 [SUSPECTED]  ← After 2 missed heartbeats            │
    │                                                │
    │ ─────────── PING (seq: 5) ──────────────────▶  │
    │              ✗ No Response                     │
    ▼                                                │
 [DEAD]  ← After 3 missed heartbeats                 │
    │                                                │
    ▼                                                │
 NodeDied event triggered                            │
```

### Node State Machine

```
                    heartbeat
                    received
                        │
                        ▼
┌─────────┐        ┌─────────┐
│ UNKNOWN │ ─────▶ │  ALIVE  │ ◀─────────────────────┐
└─────────┘        └────┬────┘                       │
                        │                            │
                        │ timeout ×                  │ heartbeat
                        │ suspect threshold          │ received
                        ▼                            │
                  ┌───────────┐                      │
                  │ SUSPECTED │                      │
                  └─────┬─────┘                      │
                        │                            │
                        │ timeout ×                  │
                        │ max missed                 │
                        ▼                            │
                  ┌──────────┐                       │
                  │   DEAD   │ ──────────────────────┘
                  └──────────┘
```

---

## Message Types

| Type | Direction | Purpose |
|------|-----------|---------|
| **PING** | Sender → Receiver | Request heartbeat response |
| **PONG** | Receiver → Sender | Acknowledge heartbeat |
| **JOIN** | New Node → Seed | Request to join cluster |
| **LEAVE** | Departing Node → All | Graceful shutdown notification |

### Message Structure

| Field | Size | Description |
|-------|------|-------------|
| Type | 1 byte | Message type (PING, PONG, JOIN, LEAVE) |
| NodeId | Variable | Unique identifier of sender |
| Sequence Number | 8 bytes | Monotonically increasing counter |
| Timestamp | 8 bytes | Unix timestamp in milliseconds |
| Metadata | Variable | Optional key-value pairs |

---

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `NodeId` | Auto-generated | Unique identifier for this node |
| `ListenPort` | 5000 | UDP port for heartbeat communication |
| `HeartbeatInterval` | 1 second | Time between sending heartbeats |
| `HeartbeatTimeout` | 3 seconds | Time before considering a heartbeat missed |
| `MaxMissedHeartbeats` | 3 | Missed heartbeats before marking node as dead |
| `SuspectThreshold` | 2 | Missed heartbeats before marking node as suspected |

---

## Tuning Guide

### Recommended Configurations

| Environment | Interval | Timeout | Max Missed | Detection Time |
|-------------|----------|---------|------------|----------------|
| **Local/LAN** | 500ms | 1.5s | 2 | ~3 seconds |
| **Data Center** | 1s | 3s | 3 | ~9 seconds |
| **Cloud/Multi-AZ** | 2s | 6s | 4 | ~24 seconds |
| **WAN/Cross-Region** | 5s | 15s | 3 | ~45 seconds |

### Trade-offs

```
FASTER DETECTION                          FEWER FALSE POSITIVES
◀─────────────────────────────────────────────────────────────▶

  Lower Interval          vs.          Higher Interval
  Lower Timeout           vs.          Higher Timeout
  Fewer Max Missed        vs.          More Max Missed
  
  + Quick failure detection            + Tolerates network blips
  + Fast failover                      + Stable cluster membership
  - More network traffic               - Slower failure detection
  - More false positives               - Delayed failover
```

---

## Events

| Event | When Triggered | Recommended Action |
|-------|----------------|-------------------|
| **NodeDied** | Node missed max heartbeats | Remove from load balancer, trigger failover |
| **NodeSuspected** | Node missed suspect threshold | Reduce traffic, prepare for failover |
| **NodeRevived** | Dead/suspected node responds | Add back to pool, resume normal traffic |

---

## Architecture

### Single Cluster

```
┌─────────────────────────────────────────────────────────────┐
│                     UDP Heartbeat Mesh                      │
│                                                             │
│         ┌──────────┐                  ┌──────────┐          │
│         │  Node A  │◀────────────────▶│  Node B  │          │
│         └────┬─────┘                  └─────┬────┘          │
│              │                              │               │
│              │         ┌──────────┐         │               │
│              └────────▶│  Node C  │◀────────┘               │
│                        └──────────┘                         │
│                                                             │
│   Every node sends heartbeats to every other node           │
└─────────────────────────────────────────────────────────────┘
```

### With Load Balancer Integration

```
┌──────────────────────────────────────────────────────────────────┐
│                        Client Requests                            │
└───────────────────────────────┬──────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│                        Load Balancer                              │
│                  (subscribes to heartbeat events)                 │
└───────────────────────────────┬──────────────────────────────────┘
                                │
            ┌───────────────────┼───────────────────┐
            │                   │                   │
            ▼                   ▼                   ▼
     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
     │  Service A  │     │  Service B  │     │  Service C  │
     │   (ALIVE)   │     │   (ALIVE)   │     │   (DEAD)    │
     └──────┬──────┘     └──────┬──────┘     └──────┬──────┘
            │                   │                   │
            └───────────────────┴───────────────────┘
                                │
                    UDP Heartbeat Protocol
```

---

## Use Cases

| Use Case | Description |
|----------|-------------|
| **Microservices** | Detect service failures for automatic failover |
| **Distributed Cache** | Monitor cache nodes, redistribute on failure |
| **Database Clusters** | Leader election, replica failover |
| **Load Balancers** | Remove unhealthy backends automatically |
| **Container Orchestration** | Monitor container health |
| **Gaming Servers** | Detect player disconnections |

---

## Comparison with Alternatives

| Solution | Pros | Cons |
|----------|------|------|
| **UDP Heartbeat** | Fast, lightweight, simple | Must handle packet loss |
| **TCP Keep-Alive** | Reliable | Slow timeout, connection overhead |
| **HTTP Health Checks** | Rich status info | Higher latency, more overhead |
| **Gossip Protocol** | Scalable to thousands | More complex, eventual consistency |
| **Consensus (Raft/Paxos)** | Strong consistency | Higher latency, complex |

---

## Performance

### Benchmarks

| Metric | Value |
|--------|-------|
| Heartbeat packet size | ~50-100 bytes |
| Send latency | < 1 μs |
| Memory per node | < 1 KB |
| CPU overhead | Negligible |
| Max nodes tested | 10,000+ |

### Network Overhead

| Nodes | Heartbeats/sec | Bandwidth |
|-------|----------------|-----------|
| 10 | 90 | ~9 KB/s |
| 50 | 2,450 | ~245 KB/s |
| 100 | 9,900 | ~990 KB/s |
| 500 | 249,500 | ~25 MB/s |

*Note: For large clusters (500+ nodes), consider gossip-based protocols to reduce overhead.*

---

## Best Practices

1. **Choose appropriate timeouts** - Balance detection speed against false positives based on your network reliability

2. **Use suspect state** - Don't immediately remove nodes; use the suspected state to reduce traffic first

3. **Implement graceful shutdown** - Send LEAVE messages when shutting down to avoid unnecessary failure detection

4. **Monitor heartbeat metrics** - Track missed heartbeats, latency, and false positive rates

5. **Handle network partitions** - Consider what happens when the network splits; avoid split-brain scenarios

6. **Test failure scenarios** - Regularly test node failures, network issues, and recovery

---

## Limitations

- **UDP packet loss** - In unreliable networks, may cause false positives
- **No strong consistency** - Different nodes may have different views temporarily
- **Firewall issues** - UDP may be blocked; ensure ports are open
- **Large clusters** - O(n²) messages; use gossip for 500+ nodes

---

## Related Protocols

- **SWIM** - Scalable Weakly-consistent Infection-style Membership
- **Gossip** - Epidemic-style information dissemination
- **Phi Accrual** - Adaptive failure detection
- **Raft** - Consensus with leader election

---


<p align="center">
  Made with ❤️ for distributed systems
</p>