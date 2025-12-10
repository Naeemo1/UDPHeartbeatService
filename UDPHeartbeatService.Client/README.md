# UDP Heartbeat Protocol for .NET

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A lightweight, high-performance failure detection system using UDP-based heartbeat protocol for distributed .NET applications. This implementation separates the Server and Client into distinct components for flexible deployment.

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Components](#components)
- [Installation](#installation)
- [Configuration](#configuration)
- [How to Run](#how-to-run)
- [Events](#events)
- [Message Types](#message-types)
- [Node States](#node-states)
- [Failure Detection Flow](#failure-detection-flow)
- [Use Cases](#use-cases)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

---

## Overview

The UDP Heartbeat Protocol provides fast and reliable failure detection for distributed systems. It consists of two main components:

| Component | Role |
|-----------|------|
| **Server** | Central hub that receives heartbeats, tracks node health, and detects failures |
| **Client** | Sends periodic heartbeats to the server and reports health metrics |

Nodes (clients) periodically send small UDP packets (heartbeats) to the server. When a node stops responding, the server detects the failure and raises appropriate events.

---

## Features

| Feature | Description |
|---------|-------------|
| **Separated Architecture** | Server and Client are independent components |
| **Fast Detection** | Sub-second to few-second failure detection |
| **Lightweight** | Minimal CPU and memory footprint using UDP |
| **Event-Driven** | Subscribe to node lifecycle events (joined, left, died, revived) |
| **Health Reporting** | Clients can send custom health metrics |
| **Graceful Shutdown** | Clients send LEAVE message before disconnecting |
| **Auto-Recovery** | Automatically detects when failed nodes come back online |
| **Thread-Safe** | All components are safe for concurrent access |
| **Configurable** | Tune detection speed vs. accuracy trade-offs |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│                         HEARTBEAT SERVER                            │
│                         (Port 5000)                                 │
│                                                                     │
│   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐             │
│   │   Receive   │    │    Node     │    │   Health    │             │
│   │   Messages  │───▶│  Registry   │◀───│   Checker   │             │
│   └─────────────┘    └─────────────┘    └─────────────┘             │
│          │                  │                  │                    │
│          │                  ▼                  │                    │
│          │         ┌─────────────┐             │                    │
│          └────────▶│   Events    │◀────────────┘                    │
│                    └─────────────┘                                  │
│                          │                                          │
│     ┌────────────────────┼────────────────────┐                     │
│     ▼                    ▼                    ▼                     │
│ NodeJoined          NodeDied            NodeRevived                 │
│ NodeLeft            NodeSuspected                                   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
         ▲                   ▲                   ▲
         │                   │                   │
    UDP PING             UDP PING             UDP PING
         │                   │                   │
         │                   │                   │
┌────────┴───────┐  ┌────────┴───────┐  ┌────────┴───────┐
│                │  │                │  │                │
│   CLIENT 1     │  │   CLIENT 2     │  │   CLIENT 3     │
│   (node-1)     │  │   (node-2)     │  │   (node-3)     │
│                │  │                │  │                │
└────────────────┘  └────────────────┘  └────────────────┘
```

---

## Project Structure

```
UDPHeartbeatService/
│
├── UDPHeartbeatService.Infrastructure/                # Shared library
│   │
│   ├── Models/
│   │   ├── HeartbeatMessage.cs       # Message structure for UDP packets
│   │   └── NodeState.cs              # Node information and status
│   │
│   ├── Configuration/
│   │   └── HeartbeatConfiguration.cs # Server and Client configurations
│   │
│   └── Registry/
│       └── NodeRegistry.cs           # Thread-safe node storage
│
├── UDPHeartbeatService.Server/              # Server application
│   │
│   ├── HeartbeatServer.cs            # Main server logic
│   └── Program.cs                    # Server entry point
│
├── UDPHeartbeatService.Client/              # Client application
│   │
│   ├── HeartbeatClient.cs            # Main client logic
│   └── Program.cs                    # Client entry point
│
├── UDPHeartbeatService.sln           # Solution file
├── README.md                         # This file

```

---

## Components

### UDPHeartbeatService.Infrastructure

The shared library containing common models, configurations, and utilities used by both server and client.

| Class | Description |
|-------|-------------|
| `HeartbeatMessage` | Defines the structure of UDP packets exchanged between server and clients |
| `NodeState` | Represents the current state of a connected node |
| `HeartbeatServerConfiguration` | Configuration options for the server |
| `HeartbeatClientConfiguration` | Configuration options for clients |
| `NodeRegistry` | Thread-safe dictionary for tracking connected nodes |

### UDPHeartbeatService.Server

The central server that listens for heartbeats and tracks node health.

| Responsibility | Description |
|----------------|-------------|
| Listen for UDP messages | Receives heartbeats on configured port |
| Track node registry | Maintains list of all connected nodes |
| Detect failures | Identifies nodes that stop responding |
| Raise events | Notifies subscribers of node state changes |
| Send acknowledgments | Responds to client heartbeats with PONG |

### UDPHeartbeatService.Client

The client component that sends heartbeats to the server.

| Responsibility | Description |
|----------------|-------------|
| Send heartbeats | Periodic PING messages to server |
| Handle responses | Process PONG acknowledgments |
| Report health | Send custom health metrics |
| Graceful shutdown | Send LEAVE message before stopping |
| Connection management | Track connection state |

---

## Installation

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 / VS Code / Rider

### Build Solution

```
dotnet build
```


---

## Configuration

### Server Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `ListenPort` | 5000 | UDP port to listen on |
| `HeartbeatTimeout` | 3 seconds | Time before a heartbeat is considered missed |
| `MaxMissedHeartbeats` | 3 | Missed heartbeats before marking node as dead |
| `SuspectThreshold` | 2 | Missed heartbeats before marking node as suspected |
| `HealthCheckInterval` | 1 second | Interval for checking node health |

### Client Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `NodeId` | Auto-generated | Unique identifier for this client |
| `ServerAddress` | 127.0.0.1 | Server IP address |
| `ServerPort` | 5000 | Server UDP port |
| `HeartbeatInterval` | 1 second | Interval between heartbeats |
| `Metadata` | Empty | Custom key-value pairs sent with heartbeats |

### Tuning Guide

| Environment | Interval | Timeout | Max Missed | Detection Time |
|-------------|----------|---------|------------|----------------|
| Local/LAN | 500ms | 1.5s | 2 | ~3 seconds |
| Data Center | 1s | 3s | 3 | ~9 seconds |
| Cloud/Multi-AZ | 2s | 6s | 4 | ~24 seconds |
| WAN/Cross-Region | 5s | 15s | 3 | ~45 seconds |

---

## How to Run

### Step 1: Start the Server

Open a terminal and navigate to the server project:

```
cd UDPHeartbeatService.Server
dotnet run
```

Expected output:

```
=== Heartbeat Server Started ===
Listening on port 5000
Press Ctrl+C to stop
```

### Step 2: Start Client(s)

Open additional terminal(s) for each client:

**Client 1:**
```
cd UDPHeartbeatService.Client
dotnet run -- client-1 127.0.0.1 5000
```

**Client 2:**
```
cd UDPHeartbeatService.Client
dotnet run -- client-2 127.0.0.1 5000
```

**Client 3:**
```
cd UDPHeartbeatService.Client
dotnet run -- client-3 127.0.0.1 5000
```

### Command Line Arguments (Client)

| Argument | Position | Description | Example |
|----------|----------|-------------|---------|
| NodeId | 1 | Unique client identifier | client-1 |
| ServerAddress | 2 | Server IP address | 127.0.0.1 |
| ServerPort | 3 | Server UDP port | 5000 |

### Step 3: Test Failure Detection

1. Start the server
2. Start 2-3 clients
3. Observe server showing connected nodes
4. Kill one client (Ctrl+C or close terminal)
5. Watch server detect the failure and raise events

### Expected Server Output

```
=== Heartbeat Server Started ===
Listening on port 5000
Press Ctrl+C to stop

[+] Node JOINED: client-1 (127.0.0.1:54321) - Alive
[+] Node JOINED: client-2 (127.0.0.1:54322) - Alive

=== Connected Nodes: 2 ===
  client-1 (127.0.0.1:54321) - Alive - Last seen: 0.5s ago
  client-2 (127.0.0.1:54322) - Alive - Last seen: 0.3s ago

[?] Node SUSPECTED: client-2 (127.0.0.1:54322)
[X] Node DEAD: client-2 (127.0.0.1:54322)

=== Connected Nodes: 1 ===
  client-1 (127.0.0.1:54321) - Alive - Last seen: 0.2s ago
```

### Expected Client Output

```
=== Heartbeat Client Started ===
Node ID: client-1
Server: 127.0.0.1:5000
Press Ctrl+C to stop

[✓] Connected to server!
  <- PONG received (seq: 1)
  <- PONG received (seq: 2)
  -> Health update sent
  <- PONG received (seq: 3)
```

---

## Events

### Server Events

| Event | When Triggered | Use Case |
|-------|----------------|----------|
| `NodeJoined` | New client sends JOIN message | Add to load balancer, log connection |
| `NodeLeft` | Client sends LEAVE message (graceful) | Remove from pool, no alert needed |
| `NodeSuspected` | Client missed suspect threshold heartbeats | Reduce traffic, prepare failover |
| `NodeDied` | Client missed max heartbeats | Trigger failover, send alert |
| `NodeRevived` | Dead/suspected node starts responding | Add back to pool, clear alerts |

### Client Events

| Event | When Triggered | Use Case |
|-------|----------------|----------|
| `Connected` | First PONG received from server | Enable service, start processing |
| `Disconnected` | LEAVE message sent | Cleanup, stop processing |
| `PongReceived` | Server acknowledges heartbeat | Track latency, connection health |

---

## Message Types

| Type | Direction | Description |
|------|-----------|-------------|
| `PING` | Client → Server | Regular heartbeat |
| `PONG` | Server → Client | Heartbeat acknowledgment |
| `JOIN` | Client → Server | Initial connection request |
| `LEAVE` | Client → Server | Graceful disconnection |
| `HEALTH` | Client → Server | Custom health metrics |

### Message Structure

| Field | Description |
|-------|-------------|
| Type | Message type (PING, PONG, JOIN, LEAVE, HEALTH) |
| NodeId | Unique identifier of the sender |
| SequenceNumber | Incrementing message counter |
| Timestamp | Unix timestamp in milliseconds |
| Metadata | Optional key-value pairs (health data, etc.) |

---

## Node States

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

| State | Description |
|-------|-------------|
| `Unknown` | Initial state, no heartbeat received yet |
| `Alive` | Node is responding normally |
| `Suspected` | Node missed some heartbeats, may be failing |
| `Dead` | Node is considered failed |

---

## Failure Detection Flow

```
Timeline:
────────────────────────────────────────────────────────────────────▶

  │         │         │         │         │         │
  HB        HB        HB       MISS      MISS      MISS
  ✓         ✓         ✓         ✗         ✗         ✗
  │         │         │         │         │         │
ALIVE     ALIVE     ALIVE   SUSPECTED SUSPECTED   DEAD
                               │         │         │
                               ▼         │         ▼
                          Event:     Event:    Event:
                       NodeSuspected  (none)  NodeDied
```

---

## Use Cases

| Use Case | Description |
|----------|-------------|
| **Microservices** | Detect service failures for automatic failover |
| **Load Balancers** | Remove unhealthy backends automatically |
| **Distributed Cache** | Monitor cache nodes, redistribute on failure |
| **Database Clusters** | Support leader election and replica failover |
| **Container Orchestration** | Monitor container health |
| **Gaming Servers** | Detect player disconnections |
| **IoT Systems** | Monitor device connectivity |

---

## Best Practices

| Practice | Description |
|----------|-------------|
| **Tune for your network** | Adjust timeouts based on network reliability |
| **Use suspect state** | Don't immediately remove nodes; reduce traffic first |
| **Implement graceful shutdown** | Always send LEAVE message when stopping |
| **Monitor metrics** | Track missed heartbeats, latency, and false positives |
| **Handle network partitions** | Consider split-brain scenarios |
| **Test failure scenarios** | Regularly test node failures and recovery |
| **Use meaningful NodeIds** | Use descriptive IDs for easier debugging |
| **Send health metrics** | Include CPU, memory, and custom metrics in heartbeats |

---

## Troubleshooting

| Issue | Possible Cause | Solution |
|-------|----------------|----------|
| Client can't connect | Firewall blocking UDP | Open UDP port in firewall |
| High false positives | Timeout too aggressive | Increase timeout and max missed |
| Slow detection | Timeout too conservative | Decrease timeout values |
| Memory growth | Node registry not cleaned | Ensure dead nodes are removed |
| Duplicate nodes | NodeId collision | Use unique NodeIds |
| Messages not received | Port already in use | Check if port is available |
| Connection drops | Network instability | Increase timeout tolerance |

### Diagnostic Commands

Check if port is in use:
```
netstat -an | grep 5000
```

Test UDP connectivity:
```
nc -u -v 127.0.0.1 5000
```

Monitor network traffic:
```
tcpdump -i any udp port 5000
```

---

## Performance

| Metric | Value |
|--------|-------|
| Heartbeat packet size | ~100-200 bytes |
| Memory per node | < 1 KB |
| CPU overhead | Negligible |
| Network overhead per client | ~100 bytes/second |

---


<p align="center">
  Made with ❤️ for distributed systems. Naeem Hisham
</p>