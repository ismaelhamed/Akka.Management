using Akka.Actor;
using Akka.Annotations;
using Akka.Cluster;
using Akka.Configuration;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Akka.Management.Cluster.Dsl
{
    [InternalApi]
    public class ClusterMembershipCheck
    {
        private readonly ActorSystem _system;
        private readonly Func<MemberStatus> _selfStatus;
        private readonly ClusterMembershipCheckSettings _settings;

        public ClusterMembershipCheck(ActorSystem system)
            : this(system,
                () => Akka.Cluster.Cluster.Get(system).SelfMember.Status,
                ClusterMembershipCheckSettings.Create(system.Settings.Config.GetConfig("akka.management.cluster.health-check")))
        {
        }

        public ClusterMembershipCheck(ActorSystem system, Func<MemberStatus> selfStatus, ClusterMembershipCheckSettings settings)
        {
            _system = system;
            _selfStatus = selfStatus;
            _settings = settings;
        }

        public Task<bool> IsReady => Task.FromResult(_settings.ReadyStates.Contains(_selfStatus()));
    }
    
    [InternalApi]
    public class ClusterMembershipCheckSettings
    {
        public ImmutableHashSet<MemberStatus> ReadyStates { get; }

        public ClusterMembershipCheckSettings(ImmutableHashSet<MemberStatus> readyStates) => ReadyStates = readyStates;

        public static ClusterMembershipCheckSettings Create(Config config) =>
            new ClusterMembershipCheckSettings(config.GetStringList("ready-states").Select(GetMemberStatus).ToImmutableHashSet());

        private static MemberStatus GetMemberStatus(string status) => status.ToLowerInvariant() switch
        {
            "weaklyup" => MemberStatus.WeaklyUp,
            "up" => MemberStatus.Up,
            "exiting" => MemberStatus.Exiting,
            "down" => MemberStatus.Down,
            "joining" => MemberStatus.Joining,
            "leaving" => MemberStatus.Leaving,
            "removed" => MemberStatus.Removed,
            _ => throw new ArgumentOutOfRangeException($"'{status}' is not a valid MemberStatus. See reference.conf for valid values")
        };
    }
}