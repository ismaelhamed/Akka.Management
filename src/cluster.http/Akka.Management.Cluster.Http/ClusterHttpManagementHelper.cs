using System.Collections.Generic;
using System.Linq;
using Akka.Cluster;

namespace Akka.Management.Cluster
{
    internal static class ClusterHttpManagementHelper
    {
        public static ClusterMember MemberToClusterMember(Member member) =>
            new ClusterMember(member.UniqueAddress.Address.ToString(), member.UniqueAddress.Uid.ToString(), member.Status.ToString(), member.Roles);

        public static Dictionary<string, string> OldestPerRole(IEnumerable<Member> thisDcMembers)
        {
            var roles = thisDcMembers.SelectMany(m => m.Roles);
            return roles.ToDictionary(role => role, role => OldestPerRole(thisDcMembers, role));
        }

        private static string OldestPerRole(IEnumerable<Member> cluster, string role)
        {
            var forRole = cluster.Where(m => m.Roles.Contains(role)).ToList();
            return forRole.Any() ? forRole.OrderBy(member => member, Member.AgeOrdering).Select(m => m.Address.ToString()).Last() : "<unknown>";
        }
    }
}
