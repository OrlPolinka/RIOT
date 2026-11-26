using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace lab_1
{
    public class ClusterManager
    {
        public List<Node> Nodes { get; private set; } = new();
        public Node CurrentLeader { get; private set; }

        public ClusterManager()
        {
            for (int i = 1; i <= 5; i++)
                Nodes.Add(new Node(i));
        }

        private void Log(string msg) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");

        public void ElectLeader()
        {
            Log("Election started...");
            foreach (var node in Nodes)
                Log($"{node}");

            var availableNodes = Nodes.Where(n => n.Status == NodeStatus.AVAILABLE).ToList();
            if (!availableNodes.Any())
            {
                Log("No available nodes! Election failed.");
                CurrentLeader = null;
                return;
            }

            CurrentLeader = availableNodes.OrderByDescending(n => n.Id).First();

            foreach (var n in Nodes) n.Role = NodeRole.FOLLOWER;

            CurrentLeader.Role = NodeRole.LEADER;
            Log($"Node {CurrentLeader.Id} elected as LEADER");
        }

        public bool CheckLeaderHeartbeat()
        {
            if (CurrentLeader == null) return false;
            Log($"Heartbeat to Node {CurrentLeader.Id}...");

            if (CurrentLeader.Status == NodeStatus.FAILED)
            {
                Log($"Node {CurrentLeader.Id} → NO RESPONSE. Heartbeat timeout.");
                return false;
            }
            return true;
        }

        public bool ProposeValue(string newValue, out int yesVotes, out int majority)
        {
            yesVotes = 0;
            majority = (Nodes.Count / 2) + 1; 

            if (CurrentLeader == null || CurrentLeader.Status == NodeStatus.FAILED)
            {
                Log("CONSENSUS NOT ACHIEVED: Leader is dead or not elected! Elections required.");
                return false;
            }

            Log($"Leader Node {CurrentLeader.Id} → PROPOSE {newValue}");

            var availableNodes = Nodes.Where(n => n.Status == NodeStatus.AVAILABLE).ToList();
            var random = new Random();

            foreach (var node in availableNodes)
            {
                if (node.Id == CurrentLeader.Id)
                {
                    yesVotes++;
                    Log($"Node {node.Id} (Leader) → YES");
                    continue;
                }

                bool votesYes = random.Next(100) < 1;

                if (votesYes)
                {
                    yesVotes++;
                    Log($"Node {node.Id} → YES");
                }
                else
                {
                    Log($"Node {node.Id} → NO");
                }
            }

            Log($"YES = {yesVotes}, Majority = {majority}");

            if (yesVotes >= majority)
            {
                Log("CONSENSUS ACHIEVED!");
                foreach (var node in availableNodes)
                    node.RentalStatus = newValue;

                return true;
            }
            else
            {
                Log("CONSENSUS NOT ACHIEVED!");
                return false;
            }
        }

        public void FailNode(int id)
        {
            var node = Nodes.FirstOrDefault(n => n.Id == id);
            if (node != null)
            {
                node.Status = NodeStatus.FAILED;
                Log($"Node {id} → FAILED");
            }
        }

        public void RestoreNode(int id)
        {
            var node = Nodes.FirstOrDefault(n => n.Id == id);
            if (node != null)
            {
                node.Status = NodeStatus.AVAILABLE;
                Log($"Node {id} → AVAILABLE");
            }
        }
    }
}