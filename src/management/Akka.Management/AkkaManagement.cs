﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Http.Dsl;
using Akka.Util;
using static Akka.Predef;

namespace Akka.Management
{
    using Route = Akka.Http.Dsl.Server.Route;
    
    public class AkkaManagement : IExtension
    {
        private readonly ILoggingAdapter _log;
        private readonly ExtendedActorSystem _system;
        private readonly ImmutableList<ManagementRouteProvider> _routeProviders;
        private readonly AtomicReference<Task<ServerBinding>> _bindingFuture = new AtomicReference<Task<ServerBinding>>();

        public AkkaManagementSettings Settings { get; }

        public AkkaManagement(ExtendedActorSystem system)
        {
            _system = system;
            _log = Logging.GetLogger(system, GetType());

            system.Settings.InjectTopLevelFallback(AkkaManagementProvider.DefaultConfiguration());
            Settings = new AkkaManagementSettings(system.Settings.Config);

            _routeProviders = LoadRouteProviders().ToImmutableList();
        }

        public static AkkaManagement Get(ActorSystem system) => system.WithExtension<AkkaManagement, AkkaManagementProvider>();

        /// <summary>
        /// <para>Get the routes for the HTTP management endpoint.</para>
        /// <para>This method can be used to embed the Akka management routes in an existing Akka HTTP server.</para>
        /// </summary>
        public Route[] Routes() => PrepareCombinedRoutes(ProviderSettings());

        /// <summary>
        /// <para>Amend the <see cref="ManagementRouteProviderSettings"/> and get the routes for the HTTP management endpoint.</para>
        /// <para>Use this when adding authentication and HTTPS.</para>
        /// <para>This method can be used to embed the Akka management routes in an existing Akka HTTP server.</para>
        /// </summary>
        public Route[] Routes(Func<ManagementRouteProviderSettings, ManagementRouteProviderSettings> transformSettings) =>
            PrepareCombinedRoutes(transformSettings(ProviderSettings()));

        /// <summary>
        /// Start an Akka HTTP server to serve the HTTP management endpoint.
        /// </summary>
        public Task Start() => Start(Identity);

        /// <summary>
        /// <para>Amend the <see cref="ManagementRouteProviderSettings"/> and start an Akka HTTP server to serve the HTTP management endpoint.</para>
        /// <para>Use this when adding authentication and HTTPS.</para>
        /// </summary>
        public async Task<Uri> Start(Func<ManagementRouteProviderSettings, ManagementRouteProviderSettings> transformSettings)
        {
            var serverBindingPromise = new TaskCompletionSource<ServerBinding>();

            if (!_bindingFuture.CompareAndSet(null, serverBindingPromise.Task)) 
                return null;
            
            try
            {
                var effectiveBindHostname = Settings.Http.EffectiveBindHostname;
                var effectiveBindPort = Settings.Http.EffectiveBindPort;
                var effectiveProviderSettings = transformSettings(ProviderSettings());

                _log.Info("Binding Akka Management (HTTP) endpoint to: {0}:{1}", effectiveBindHostname, effectiveBindPort);

                var combinedRoutes = PrepareCombinedRoutes(effectiveProviderSettings);
                    
                var serverBinding = await _system.Http().BindAndHandle(
                    combinedRoutes,
                    effectiveBindHostname,
                    effectiveBindPort);

                serverBindingPromise.SetResult(serverBinding);

                var boundPort = ((DnsEndPoint)serverBinding.LocalAddress).Port;
                _log.Info("Bound Akka Management (HTTP) endpoint to: {0}:{1}", effectiveBindHostname, boundPort);

                return effectiveProviderSettings.SelfBaseUri.WithPort(boundPort);
            }
            catch (Exception ex)
            {
                _log.Warning(ex.Message);
                throw new InvalidOperationException("Failed to start Akka Management HTTP endpoint.", ex);
            }
        }

        public Task<Done> Stop()
        {
            while (true)
            {
                var binding = _bindingFuture.Value;
                if (binding == null)
                {
                    return Task.FromResult(Done.Instance);
                }

                if (!_bindingFuture.CompareAndSet(binding, null))
                {
                    // retry, CAS was not successful, someone else completed the stop()
                    continue;
                }

                var stopFuture = binding.Map(_ => _.Unbind()).Map(_ => Done.Instance);
                return stopFuture;
            }
        }

        private ManagementRouteProviderSettings ProviderSettings()
        {
            // port is on purpose never inferred from protocol, because this HTTP endpoint is not the "main" one for the app
            const string protocol = "http"; // changed to "https" if ManagementRouteProviderSettings.withHttpsConnectionContext is use

            var basePath = !string.IsNullOrWhiteSpace(Settings.Http.BasePath) ? Settings.Http.BasePath + "/" : string.Empty;
            var selfBaseUri = new Uri($"{protocol}://{Settings.Http.Hostname}:{Settings.Http.Port}{basePath}");
            return ManagementRouteProviderSettings.Create(selfBaseUri, Settings.Http.RouteProvidersReadOnly);
        }

        private Route[] PrepareCombinedRoutes(ManagementRouteProviderSettings providerSettings)
        {
            // TODO
            static Route[] WrapWithAuthenticatorIfPresent(Route[] inner)
            {
                return inner;
            }

            var combinedRoutes = _routeProviders
                .Select(provider =>
                {
                    _log.Info("Including HTTP management routes for {0}", Logging.SimpleName(provider));
                    return provider.Routes(providerSettings);
                })
                .SelectMany(route => route)
                .ToArray();

            return combinedRoutes.Length > 0
                ? WrapWithAuthenticatorIfPresent(combinedRoutes)
                : throw new ArgumentException("No routes configured for akka management! Double check your `akka.management.http.routes` config.");
        }

        private IEnumerable<ManagementRouteProvider> LoadRouteProviders()
        {
            foreach (var (name, fqcn) in Settings.Http.RouteProviders)
            {
                var dynamic = DynamicAccess.CreateInstanceFor<ManagementRouteProvider>(fqcn, null);
                var instanceTry = dynamic.RecoverWith(ex => ex is TypeLoadException || ex is MissingMethodException
                    ? DynamicAccess.CreateInstanceFor<ManagementRouteProvider>(fqcn, _system)
                    : dynamic);

                yield return instanceTry.IsSuccess switch
                {
                    true => instanceTry.Get(),
                    false when instanceTry.Failure.Value is TypeLoadException || instanceTry.Failure.Value is MissingMethodException =>
                        throw new ArgumentException(nameof(fqcn), $"[{fqcn}] is not a 'ManagementRouteProvider'"),
                    _ => throw new Exception($"While trying to load route provider extension [{name} = {fqcn}]", instanceTry.Failure.Value)
                };
            }
        }
    }

    public class AkkaManagementProvider : ExtensionIdProvider<AkkaManagement>
    {
        public override AkkaManagement CreateExtension(ExtendedActorSystem system) => new AkkaManagement(system);

        /// <summary>
        /// Returns a default configuration for the Akka Management module.
        /// </summary>
        public static Config DefaultConfiguration() => ConfigurationFactory.FromResource<AkkaManagement>("Akka.Management.Resources.reference.conf");
    }
}