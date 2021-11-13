using Akka.Actor;
using Akka.Configuration;
using Akka.Http.Dsl;
using Akka.Management.Cluster.Dsl;

namespace Akka.Management.Cluster
{
    public class ClusterHttpManagementRouteProvider : ExtensionIdProvider<ClusterHttpManagementRouteExt>
    {
        public override ClusterHttpManagementRouteExt CreateExtension(ExtendedActorSystem system) =>
            new ClusterHttpManagementRouteExt(system);
    }

    /// <summary>
    /// Provides an HTTP management interface for <see cref="Akka.Cluster.Cluster"/>.
    /// </summary>
    public class ClusterHttpManagementRouteExt : IManagementRouteProvider, IExtension
    {
        private readonly Akka.Cluster.Cluster _cluster;

        public ClusterHttpManagementSettings Settings { get; }

        public ClusterHttpManagementRouteExt(ExtendedActorSystem system)
        {
            system.Settings.InjectTopLevelFallback(DefaultConfiguration());
            _cluster = Akka.Cluster.Cluster.Get(system);

            Settings = new ClusterHttpManagementSettings(system.Settings.Config);            
        }

        public static Config DefaultConfiguration()
            => ConfigurationFactory.FromResource<ClusterHttpManagementRouteExt>("Akka.Management.Cluster.Http.Resources.reference.conf");

        public static ClusterHttpManagementRouteExt Get(ActorSystem system)
            => system.WithExtension<ClusterHttpManagementRouteExt, ClusterHttpManagementRouteProvider>();

        /// <summary>
        /// Routes to be exposed by Akka cluster management
        /// </summary>
        public Route[] Routes(ManagementRouteProviderSettings routeProviderSettings)
        {
            return routeProviderSettings.ReadOnly 
                ? new ClusterHttpManagementRoutes(_cluster).ReadOnlyRoutes 
                : new ClusterHttpManagementRoutes(_cluster).Routes;
        }
    }
}
