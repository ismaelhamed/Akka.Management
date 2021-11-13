using System.Collections.Generic;
using System.Collections.Immutable;
using Akka.Annotations;
using Akka.Util;

namespace Akka.Management.Cluster
{
    public class ClusterUnreachableMember
    {
        public string Node { get; }
        public ImmutableList<string> ObservedBy { get; }

        public ClusterUnreachableMember(string node, IEnumerable<string> observedBy)
        {
            Node = node;
            ObservedBy = observedBy?.ToImmutableList() ?? ImmutableList<string>.Empty;
        }
    }

    public class ClusterMember
    {
        public string Node { get; }
        public string NodeUid { get; }
        public string Status { get; }
        public ImmutableHashSet<string> Roles { get; }

        public ClusterMember(string node, string nodeUid, string status, IEnumerable<string> roles)
        {
            Node = node;
            NodeUid = nodeUid;
            Status = status;
            Roles = roles?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty;
        }
    }

    public class ClusterMembers
    {
        public string SelfNode { get; }
        public ImmutableHashSet<ClusterMember> Members { get; }
        public ImmutableList<ClusterUnreachableMember> Unreachable { get; }
        public string Leader { get; }
        public string Oldest { get; }
        public ImmutableDictionary<string, string> OldestPerRole { get; }

        public ClusterMembers(string selfNode, IEnumerable<ClusterMember> members, IEnumerable<ClusterUnreachableMember> unreachable, string leader, string oldest, IDictionary<string, string> oldestPerRole)
        {
            SelfNode = selfNode;
            Members = members?.ToImmutableHashSet() ?? ImmutableHashSet<ClusterMember>.Empty;
            Unreachable = unreachable?.ToImmutableList() ?? ImmutableList<ClusterUnreachableMember>.Empty;
            Leader = leader;
            Oldest = oldest;
            OldestPerRole = oldestPerRole?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty;
        }
    }

    public class ClusterHttpManagementMessage
    {
        public string Message { get; }
        public ClusterHttpManagementMessage(string message) => Message = message;
    }

    public class ShardRegionInfo
    {
        public string ShardId { get; }
        public int NumEntities { get; }

        public ShardRegionInfo(string shardId, int numEntities)
        {
            ShardId = shardId;
            NumEntities = numEntities;
        }
    }

    public class ShardDetails
    {
        public ImmutableList<ShardRegionInfo> Regions { get; }
        public ShardDetails(IEnumerable<ShardRegionInfo> regions)
        {
            Regions = regions?.ToImmutableList() ?? ImmutableList<ShardRegionInfo>.Empty;
        }
    }

    [InternalApi]
    public interface IClusterHttpManagementMemberOperation
    { }

    [InternalApi]
    public class Down : IClusterHttpManagementMemberOperation
    {
        public static readonly Down Instance = new Down();
        private Down() { }
    }

    [InternalApi]
    public class Leave : IClusterHttpManagementMemberOperation
    {
        public static readonly Leave Instance = new Leave();
        private Leave() { }
    }

    [InternalApi]
    public class Join : IClusterHttpManagementMemberOperation
    {
        public static readonly Join Instance = new Join();
        private Join() { }
    }

    [InternalApi]
    public static class ClusterHttpManagementMemberOperation
    {
        public static IClusterHttpManagementMemberOperation FromString(string value) => value.ToLowerInvariant() switch
        {
            "down" => Down.Instance,
            "leave" => Leave.Instance,
            "join" => Join.Instance,
            _ => null
        };
    }
}
