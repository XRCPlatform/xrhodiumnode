using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using BRhodium.Node.Builder;
using BRhodium.Node.Builder.Feature;
using BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers;
using BRhodium.Node.Interfaces;
using BRhodium.Node.Utilities;

namespace BRhodium.Node.IntegrationTests
{
    public static class FullNodeTestBuilderExtension
    {
        /// <summary>
        /// Substitute the <see cref="IDateTimeProvider"/> for a given feature.
        /// </summary>
        /// <typeparam name="T">The feature to substitute the provider for.</typeparam>
        public static IFullNodeBuilder SubstituteDateTimeProviderFor<T>(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                var feature = features.FeatureRegistrations.FirstOrDefault(f => f.FeatureType == typeof(T));
                if (feature != null)
                {
                    feature.FeatureServices(services =>
                    {
                        ServiceDescriptor service = services.FirstOrDefault(s => s.ServiceType == typeof(IDateTimeProvider));
                        if (service != null)
                            services.Remove(service);

                        services.AddSingleton<IDateTimeProvider, GenerateCoinsFastDateTimeProvider>();
                    });
                }
            });

            return fullNodeBuilder;
        }

        public static IFullNodeBuilder MockIBD(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                {
                    feature.FeatureServices(services =>
                    {
                        // Get default IBD implementation and replace it with the mock.
                        ServiceDescriptor ibdService = services.FirstOrDefault(x => x.ServiceType == typeof(IInitialBlockDownloadState));

                        if (ibdService != null)
                        {
                            services.Remove(ibdService);
                            services.AddSingleton<IInitialBlockDownloadState, InitialBlockDownloadStateMock>();
                        }
                    });
                }
            });

            return fullNodeBuilder;
        }
    }
}