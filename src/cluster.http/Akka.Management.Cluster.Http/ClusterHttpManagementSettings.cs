using Akka.Configuration;

namespace Akka.Management.Cluster
{
    public class ClusterHttpManagementSettings
    {
        public ClusterHttpManagementSettings(Config config)
        {
            _ = config.GetConfig("akka.management.cluster");

            // placeholder for potential future configuration... currently nothing is configured here
        }
    }
}
