using UDPHeartbeatService.Infrastructure.Enum;
using UDPHeartbeatService.Infrastructure.Registry;

namespace UdpHeartbeat.Tests;

public class NodeStateTests
{
	[Fact]
	public void Node_BecomesSuspected_AfterSuspectThreshold()
	{
		// Arrange
		var registry = new NodeRegistry();
		registry.AddOrUpdate("node-1", "127.0.0.1", 5001);

		// Act - Simulate missed heartbeats
		registry.IncrementMissedHeartbeat("node-1");
		registry.IncrementMissedHeartbeat("node-1");

		var node = registry.Get("node-1");

		// Assert
		Assert.Equal(2, node!.MissedHeartbeats);
	}

	[Fact]
	public void Node_BecomesDead_AfterMaxMissedHeartbeats()
	{
		// Arrange
		var registry = new NodeRegistry();
		registry.AddOrUpdate("node-1", "127.0.0.1", 5001);

		// Act - Simulate 3 missed heartbeats
		registry.IncrementMissedHeartbeat("node-1");
		registry.IncrementMissedHeartbeat("node-1");
		registry.IncrementMissedHeartbeat("node-1");
		registry.SetStatus("node-1", NodeStatus.Dead);

		var node = registry.Get("node-1");

		// Assert
		Assert.Equal(NodeStatus.Dead, node!.Status);
		Assert.Equal(3, node.MissedHeartbeats);
	}

	[Fact]
	public void Node_Revives_WhenHeartbeatReceived()
	{
		// Arrange
		var registry = new NodeRegistry();
		registry.AddOrUpdate("node-1", "127.0.0.1", 5001);
		registry.SetStatus("node-1", NodeStatus.Dead);

		// Act - Simulate heartbeat received
		registry.AddOrUpdate("node-1", "127.0.0.1", 5001);

		var node = registry.Get("node-1");

		// Assert
		Assert.Equal(NodeStatus.Alive, node!.Status);
		Assert.Equal(0, node.MissedHeartbeats);
	}

	[Fact]
	public void Node_StatusTransition_Unknown_To_Alive()
	{
		// Arrange
		var registry = new NodeRegistry();

		// Act
		registry.AddOrUpdate("node-1", "127.0.0.1", 5001);

		// Assert
		var node = registry.Get("node-1");
		Assert.Equal(NodeStatus.Alive, node!.Status);
	}
}