using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Sharding;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Server;
using Akka.IO;
using Akka.Util;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using static Akka.Management.Cluster.ClusterHttpManagementHelper;

namespace Akka.Management.Cluster.Dsl
{
    public class ClusterHttpManagementRoutes
    {
        private readonly Akka.Cluster.Cluster _cluster;

        public ClusterHttpManagementRoutes(Akka.Cluster.Cluster cluster) => _cluster = cluster;

        /// <summary>
        /// Creates an instance of <see cref="ClusterHttpManagementRoutes"/> to manage the specified
        /// <seealso cref="Akka.Cluster.Cluster"/> instance. This version does not provide Basic Authentication.
        /// </summary>
        public Route[] Routes
        {
            get
            {
                return new Route[]
                {
                    async context =>
                    {
                        // TODO: PathPrefix "cluster"
                        return context.Request.Path.ToString() switch
                        {
                            "/cluster/members" => await RouteGetMembers()(context) ?? await RoutePostMembers()(context),
                            "/cluster/members/{address}" => await RouteFindMember(false)(context),
                            "/cluster" => await RoutePutCluster()(context),
                            "/cluster/shards/{shardRegionName}" => await RouteGetShardInfo("" /* shardRegionName */)(context),
                            _ => null
                        };
                    }
                };
            }
        }

        /// <summary>
        /// Creates an instance of <see cref="ClusterHttpManagementRoutes"/> with only the read only routes.
        /// </summary>
        public Route[] ReadOnlyRoutes
        {
            get
            {
                return new Route[]
                {
                    async context =>
                    {
                        // TODO: PathPrefix "cluster"
                        return context.Request.Path.ToString() switch
                        {
                            "/cluster/members" => await RouteGetMembers()(context),
                            "/cluster/members/{address}" => await RouteFindMember(true)(context),
                            "/cluster/shards/{shardRegionName}" => await RouteGetShardInfo("" /* shardRegionName */)(context),
                            _ => null
                        };
                    }
                };
            }
        }

        private Route RouteGetMembers()
        {
            return context =>
            {
                if (context.Request.Method == "Get")
                {
                    var readView = ClusterReadViewAccess.InternalReadView(_cluster);
                    var members = readView.State.Members.Select(MemberToClusterMember);

                    var unreachable = readView.Reachability.ObserversGroupedByUnreachable.Select(pair => { return new ClusterUnreachableMember(pair.Key.Address.ToString(), pair.Value.Select(address => address.Address.ToString())); });

                    var leader = readView.Leader.ToString();
                    var oldest = _cluster.State.Members.Where(node => node.Status == MemberStatus.Up)
                        .OrderBy(member => member, Member.AgeOrdering)
                        .Select(m => m.Address.ToString())
                        .Last(); // we are only interested in the oldest one that is still Up

                    var clusterMembers = new ClusterMembers(_cluster.SelfAddress.ToString(), members, unreachable, leader, oldest, OldestPerRole(_cluster.State.Members));

                    return Task.FromResult((RouteResult.IRouteResult)new RouteResult.Complete(HttpResponse.Create(
                        entity: new ResponseEntity(ContentTypes.ApplicationJson, ByteString.FromString(JsonConvert.SerializeObject(clusterMembers))))));
                }

                return null;
            };
        }

        private Route RoutePostMembers()
        {
            return context =>
            {
                if (context.Request.Method == "Post")
                {
                    var address = Address.Parse(""); // TODO: addressString, FormField("address")
                    _cluster.Join(address);
                    var clusterMessage = new ClusterHttpManagementMessage($"Joining {address}");

                    return Task.FromResult((RouteResult.IRouteResult)new RouteResult.Complete(HttpResponse.Create(
                        entity: new ResponseEntity(ContentTypes.ApplicationJson, ByteString.FromString(JsonConvert.SerializeObject(clusterMessage))))));
                }

                return null;
            };
        }

        private Route RouteGetMember(Member member)
        {
            return context =>
            {
                if (context.Request.Method != "Get")
                    return null;

                var clusterMember = MemberToClusterMember(member);
                return Task.FromResult((RouteResult.IRouteResult)new RouteResult.Complete(HttpResponse.Create(
                    entity: new ResponseEntity(ContentTypes.ApplicationJson, ByteString.FromString(JsonConvert.SerializeObject(clusterMember))))));
            };
        }

        private Route RouteDeleteMember(Member member)
        {
            return context =>
            {
                if (context.Request.Method != "Delete")
                    return null;

                _cluster.Leave(member.UniqueAddress.Address);
                var clusterMessage = new ClusterHttpManagementMessage($"Leaving {member.UniqueAddress.Address}");

                return Task.FromResult((RouteResult.IRouteResult)new RouteResult.Complete(HttpResponse.Create(
                    entity: new ResponseEntity(ContentTypes.ApplicationJson, ByteString.FromString(JsonConvert.SerializeObject(clusterMessage))))));
            };
        }

        private Route RoutePutMember(Member member)
        {
            return context =>
            {
                if (context.Request.Method != "Put")
                    return null;

                var operation = ""; // TODO: operation, FormField("operation")
                ClusterHttpManagementMessage clusterMessage;
                switch (ClusterHttpManagementMemberOperation.FromString(operation))
                {
                    case Down _:
                        _cluster.Down(member.UniqueAddress.Address);
                        clusterMessage = new ClusterHttpManagementMessage($"Downing {member.UniqueAddress.Address}");
                        break;
                    case Leave _:
                        _cluster.Leave(member.UniqueAddress.Address);
                        clusterMessage = new ClusterHttpManagementMessage($"Leaving {member.UniqueAddress.Address}");
                        break;
                    default:
                        clusterMessage = new ClusterHttpManagementMessage("Operation not supported");
                        // TODO: StatusCodes.BadRequest
                        break;
                }

                return Task.FromResult((RouteResult.IRouteResult)new RouteResult.Complete(HttpResponse.Create(
                    entity: new ResponseEntity(ContentTypes.ApplicationJson, ByteString.FromString(JsonConvert.SerializeObject(clusterMessage))))));
            };
        }

        private Route RoutePutCluster()
        {
            return context =>
            {
                if (context.Request.Method != "Put")
                    return null;

                var operation = ""; // TODO: operation, FormField("operation")
                ClusterHttpManagementMessage clusterMessage;
                if (operation.ToLowerInvariant() == "prepare-for-full-shutdown")
                {
                    // TODO: clusterMessage = _cluster.PrepareForFullClusterShutdown();
                    clusterMessage = new ClusterHttpManagementMessage("Operation not supported");
                }
                else
                {
                    clusterMessage = new ClusterHttpManagementMessage("Operation not supported");
                    // TODO: StatusCodes.BadRequest
                }

                return Task.FromResult((RouteResult.IRouteResult)new RouteResult.Complete(HttpResponse.Create(
                    entity: new ResponseEntity(ContentTypes.ApplicationJson, ByteString.FromString(JsonConvert.SerializeObject(clusterMessage))))));
            };
        }

        private Route RouteGetShardInfo(string shardRegionName)
        {
            return async context =>
            {
                if (context.Request.Method != "Get")
                    return null;

                ClusterHttpManagementMessage clusterMessage;
                try
                {
                    var shardDetails = await ClusterSharding.Get(_cluster.System)
                        .ShardRegion(shardRegionName)
                        .Ask<ShardRegionStats>(GetShardRegionStats.Instance, TimeSpan.FromSeconds(5))
                        .Map(stats => new ShardDetails(stats.Stats.Select(stat => new ShardRegionInfo(stat.Key, stat.Value)).ToArray()));

                    return new RouteResult.Complete(HttpResponse.Create(
                        entity: new ResponseEntity(ContentTypes.ApplicationJson, ByteString.FromString(JsonConvert.SerializeObject(shardDetails)))));
                }
                catch (AskTimeoutException)
                {
                    clusterMessage = new ClusterHttpManagementMessage($"Shard Region [{shardRegionName}] not responding, may have been terminated");
                }
                catch (Exception)
                {
                    clusterMessage = new ClusterHttpManagementMessage($"Shard type [{shardRegionName}] must be started first");
                }

                return new RouteResult.Complete(HttpResponse.Create(
                    entity: new ResponseEntity(ContentTypes.ApplicationJson, ByteString.FromString(JsonConvert.SerializeObject(clusterMessage)))));
            };
        }

        private static Option<Member> FindMember(Akka.Cluster.Cluster cluster, string memberAddress)
        {
            var readView = ClusterReadViewAccess.InternalReadView(cluster);
            return readView.Members.SingleOrDefault(m => m.UniqueAddress.Address.ToString() == memberAddress || m.Address.HostPort() == memberAddress) 
                   ?? Option<Member>.None;
        }

        private Route RouteFindMember(bool readOnly)
        {
            return async context =>
            {
                if (readOnly && context.Request.Method != "Get") // TODO: HttpMethods.GET
                    return new RouteResult.Complete(HttpResponse.Create(405)); // TODO: StatusCodes.MethodNotAllowed

                var memberAddress = ""; // TODO: RemainingDecoded, context.Request.QueryString
                var member = FindMember(_cluster, memberAddress);

                return member.HasValue
                    ? await RouteGetMember(member.Value)(context) ?? await RouteDeleteMember(member.Value)(context) ?? await RoutePutMember(member.Value)(context)
                    : new RouteResult.Complete(HttpResponse.Create(404 /* TODO: StatusCodes.NotFound */, entity: new ResponseEntity(ContentTypes.ApplicationJson,
                        ByteString.FromString(JsonConvert.SerializeObject(new ClusterHttpManagementMessage($"Member {memberAddress} not found"))))));
            };
        }
    }
}
