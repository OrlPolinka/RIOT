using System;

namespace lab_1
{
    public enum NodeStatus { AVAILABLE, FAILED }
    public enum NodeRole { LEADER, FOLLOWER }

    public class Node
    {
        public int Id { get; set; }
        public NodeStatus Status { get; set; } = NodeStatus.AVAILABLE;
        public NodeRole Role { get; set; } = NodeRole.FOLLOWER;

        public string RentalStatus { get; set; } = "Active";

        public Node(int id)
        {
            Id = id;
        }

        public override string ToString()
        {
            return $"Node {Id} [{Status}, {Role}]";
        }
    }
}