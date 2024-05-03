using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace NuGetProxy;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var webappbuilder = WebApplication.CreateBuilder(args);
        webappbuilder.Configuration.AddUserSecrets<Program>();

        var config = webappbuilder.Configuration;
        var pathbase = config.GetValue<string>("PathBase") ?? string.Empty;

        webappbuilder
            .Services
            .AddReverseProxy()
            .ConfigureHttpClient((context, handler) =>
            {
                handler.AutomaticDecompression = DecompressionMethods.All;
            })
            .LoadFromConfig(config.GetSection("ReverseProxy"))
            .AddTransforms(transformContext =>
            {
                transformContext.AddResponseTransform(async responseContext =>
                {
                    // Only handle json responses and only if we have destinations
                    if (IsResponseOfType(responseContext.ProxyResponse, "application/json")
                        && TryGetDestinationConfig(transformContext, "NuGet", out var destconfig))
                    {
                        // Read response body
                        using var proxyresponse = await responseContext.ProxyResponse!.Content.ReadAsStreamAsync(responseContext.CancellationToken);
                        var response = responseContext.HttpContext.Response.Body;

                        responseContext.SuppressResponseBody = true;

                        responseContext.HttpContext.Response.Headers.Remove("Content-Length");

                        // Rewrite urls in response body
                        var baseuri = GetBaseUri(responseContext.HttpContext.Request, pathbase);
                        await JsonBodyRewriter.ReplaceStringInJsonAsync(
                            proxyresponse,
                            response,
                            destconfig.Address,
                            baseuri,
                            responseContext.CancellationToken
                        );
                    }
                });
            });

        var app = webappbuilder.Build();
        app.MapReverseProxy();
        app.UsePathBase(pathbase);

        await app.RunAsync();
    }

    public static string GetBaseUri(HttpRequest request, string path)
        => new UriBuilder
        {
            Scheme = request.Scheme,
            Host = request.Host.Host,
            Port = request.Host.Port ?? (request.IsHttps ? 443 : 80),
            Path = path
        }.Uri.AbsoluteUri.TrimEnd('/');

    private static bool IsResponseOfType(HttpResponseMessage? response, string contentType)
        => contentType.Equals(response?.Content?.Headers?.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase);

    public static bool TryGetDestinationConfig(TransformBuilderContext context, string key, [NotNullWhen(true)] out DestinationConfig? destinationConfig)
    {
        destinationConfig = default;
        return context.Cluster?.Destinations is not null && context.Cluster.Destinations.TryGetValue(key, out destinationConfig);
    }
}
