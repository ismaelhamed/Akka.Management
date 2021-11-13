using Akka.Annotations;

namespace Akka.Management.Cluster
{
    [InternalApi]
    internal static class ClusterReadViewAccess
    {
        /// <summary>
        /// Exposes the internal ReadView of the Akka Cluster, not reachable because it is internal.
        /// </summary>
        [InternalApi]
        internal static Akka.Cluster.ClusterReadView InternalReadView(Akka.Cluster.Cluster cluster) => cluster.ReadView;
    }
}
