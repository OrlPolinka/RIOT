//using System;
//using System.Linq;
//using Xunit;
//using Xunit.Abstractions;

//namespace lab_1.Tests
//{
//    public class ClusterTests
//    {
//        private readonly ITestOutputHelper _output;

//        public ClusterTests(ITestOutputHelper output)
//        {
//            _output = output;
//        }


//        [Fact]
//        public void Test1_Election_SelectsHighestId()
//        {
//            var cluster = new ClusterManager();
//            cluster.ElectLeader();

//            Assert.NotNull(cluster.CurrentLeader);
//            Assert.Equal(5, cluster.CurrentLeader.Id);
//            Assert.Equal(NodeRole.LEADER, cluster.CurrentLeader.Role);

//            foreach (var n in cluster.Nodes.Where(n => n.Id != 5))
//                Assert.Equal(NodeRole.FOLLOWER, n.Role);

//            _output.WriteLine("Лидер выбран верно: Node 5.");
//        }

//        [Fact]
//        public void Test1b_Election_OnlyOneLeader()
//        {
//            var cluster = new ClusterManager();
//            cluster.ElectLeader();

//            int leaders = cluster.Nodes.Count(n => n.Role == NodeRole.LEADER);
//            Assert.Equal(1, leaders);
//            _output.WriteLine("Одновременно существует только один Leader.");
//        }

//        [Fact]
//        public void Test1c_Election_NeverSelectsFailedNode()
//        {
//            var cluster = new ClusterManager();
//            cluster.FailNode(5);  

//            cluster.ElectLeader();

//            Assert.NotNull(cluster.CurrentLeader);
//            Assert.NotEqual(5, cluster.CurrentLeader.Id);
//            Assert.Equal(NodeStatus.AVAILABLE, cluster.CurrentLeader.Status);
//            _output.WriteLine($"Недоступный Node 5 пропущен, выбран Node {cluster.CurrentLeader.Id}.");
//        }


//        [Fact]
//        public void Test2_LeaderFails_NewElection()
//        {
//            var cluster = new ClusterManager();
//            cluster.ElectLeader();
//            cluster.FailNode(5);

//            cluster.ElectLeader();

//            Assert.NotNull(cluster.CurrentLeader);
//            Assert.Equal(4, cluster.CurrentLeader.Id);
//            Assert.Equal(NodeRole.LEADER, cluster.CurrentLeader.Role);

//            var node5 = cluster.Nodes.First(n => n.Id == 5);
//            Assert.Equal(NodeStatus.FAILED, node5.Status);
//            Assert.Equal(NodeRole.FOLLOWER, node5.Role);

//            _output.WriteLine("Перевыборы прошли успешно: Node 4 → LEADER.");
//        }

//        [Fact]
//        public void Test2b_LeaderFails_HeartbeatReturnsFalse()
//        {
//            var cluster = new ClusterManager();
//            cluster.ElectLeader();

//            Assert.True(cluster.CheckLeaderHeartbeat(), "Лидер жив — heartbeat должен вернуть true");

//            cluster.FailNode(5);
//            Assert.False(cluster.CheckLeaderHeartbeat(), "Лидер упал — heartbeat должен вернуть false");

//            _output.WriteLine("Heartbeat корректно обнаруживает отказ лидера.");
//        }

//        [Fact]
//        public void Test2c_NoLeader_HeartbeatReturnsFalse()
//        {
//            var cluster = new ClusterManager();
//            Assert.Null(cluster.CurrentLeader);
//            Assert.False(cluster.CheckLeaderHeartbeat());
//            _output.WriteLine("Без лидера heartbeat = false.");
//        }

//        [Fact]
//        public void Test3_Consensus_Achieved_5Nodes()
//        {
//            var cluster = new ClusterManager();
//            cluster.ElectLeader();

//            bool achieved = cluster.ProposeValue("Completed", out int yes, out int majority);

//            Assert.True(achieved);
//            Assert.Equal(5, yes);
//            Assert.Equal(3, majority);

//            foreach (var n in cluster.Nodes)
//                Assert.Equal("Completed", n.RentalStatus);

//            _output.WriteLine($"Консенсус достигнут: {yes}/{majority}.");
//        }

//        [Fact]
//        public void Test3b_Consensus_Achieved_4Of5()
//        {
//            var cluster = new ClusterManager();
//            cluster.ElectLeader();
//            cluster.FailNode(5);     
//            cluster.ElectLeader();     

//            bool achieved = cluster.ProposeValue("Completed", out int yes, out int majority);

//            Assert.True(achieved);
//            Assert.Equal(4, yes);
//            Assert.Equal(3, majority);

//            _output.WriteLine($"Консенсус при 4/5: YES={yes}, majority={majority}.");
//        }

//        [Fact]
//        public void Test3c_Consensus_Achieved_ExactlyMajority_3Of5()
//        {
//            var cluster = new ClusterManager();
//            cluster.ElectLeader();
//            cluster.FailNode(4);
//            cluster.FailNode(5);      
//            cluster.ElectLeader(); 

//            bool achieved = cluster.ProposeValue("Completed", out int yes, out int majority);

//            Assert.True(achieved);
//            Assert.Equal(3, yes);
//            Assert.Equal(3, majority);
//            _output.WriteLine($"Консенсус ровно на границе: {yes}/{majority}.");
//        }


//        [Fact]
//        public void Test4_Consensus_Fails_WhenNoMajority()
//        {
//            var cluster = new ClusterManager();
//            cluster.ElectLeader();

//            cluster.FailNode(3);
//            cluster.FailNode(4);
//            cluster.FailNode(5); 

//            cluster.ElectLeader();
//            Assert.Equal(2, cluster.CurrentLeader.Id);

//            var before = cluster.Nodes.Select(n => n.RentalStatus).ToList();

//            bool achieved = cluster.ProposeValue("Cancelled", out int yes, out int majority);

//            Assert.False(achieved);
//            Assert.Equal(2, yes);
//            Assert.Equal(3, majority);

//            var after = cluster.Nodes.Select(n => n.RentalStatus).ToList();
//            Assert.Equal(before, after);

//            _output.WriteLine($"Консенсус не достигнут: {yes}/{majority}, значения не изменены.");
//        }

//        [Fact]
//        public void Test4b_Consensus_Fails_WithoutLeader()
//        {
//            var cluster = new ClusterManager();
//            bool achieved = cluster.ProposeValue("Completed", out int yes, out int majority);

//            Assert.False(achieved);
//            Assert.Equal(0, yes);
//            Assert.Equal(3, majority);

//            foreach (var n in cluster.Nodes)
//                Assert.Equal("Active", n.RentalStatus);

//            _output.WriteLine("Без лидера консенсус невозможен.");
//        }

//        [Fact]
//        public void Test4c_Consensus_Fails_WhenLeaderFailed()
//        {
//            var cluster = new ClusterManager();
//            cluster.ElectLeader();    
//            cluster.FailNode(5);    

//            bool achieved = cluster.ProposeValue("Completed", out int yes, out int majority);

//            Assert.False(achieved);
//            _output.WriteLine("Лидер упал, но перевыборов не было — консенсус невозможен.");
//        }



//        [Fact]
//        public void Test5_RestoreNode_ReturnsToAvailable()
//        {
//            var cluster = new ClusterManager();
//            cluster.FailNode(5);
//            Assert.Equal(NodeStatus.FAILED, cluster.Nodes.First(n => n.Id == 5).Status);

//            cluster.RestoreNode(5);
//            Assert.Equal(NodeStatus.AVAILABLE, cluster.Nodes.First(n => n.Id == 5).Status);

//            cluster.ElectLeader();
//            Assert.Equal(5, cluster.CurrentLeader.Id);

//            _output.WriteLine("Node 5 восстановлен и снова становится лидером.");
//        }

//    }
//}