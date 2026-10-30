using Google.Protobuf;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Longevia.Identity.Contracts.V1;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Progress.IntegrationTests.Metrics;

// Controlled faults only. Success E2E uses Identity's actual process and repository.
public sealed class ControlledHeightServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    public string Address => _app.Urls.Single();
    public Func<GetUserHeightRequest, ServerCallContext, Task<GetUserHeightResponse>> Handle { get; }
    private ControlledHeightServer(WebApplication app,
        Func<GetUserHeightRequest, ServerCallContext, Task<GetUserHeightResponse>> handle)
    { _app = app; Handle = handle; }

    public static async Task<ControlledHeightServer> StartAsync(
        Func<GetUserHeightRequest, ServerCallContext, Task<GetUserHeightResponse>> handle)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(System.Net.IPAddress.Loopback, 0, p => p.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<IServiceMethodProvider<ControlledHeightServer>, Methods>();
        ControlledHeightServer? server = null;
        builder.Services.AddSingleton(_ => server!);
        var app = builder.Build();
        server = new ControlledHeightServer(app, handle);
        app.MapGrpcService<ControlledHeightServer>();
        await app.StartAsync();
        return server;
    }

    public async ValueTask DisposeAsync() { await _app.StopAsync(); await _app.DisposeAsync(); }

    private sealed class Methods : IServiceMethodProvider<ControlledHeightServer>
    {
        public void OnServiceMethodDiscovery(ServiceMethodProviderContext<ControlledHeightServer> context) =>
            context.AddUnaryMethod(new Method<GetUserHeightRequest, GetUserHeightResponse>(MethodType.Unary,
                "longevia.identity.v1.IdentityService", "GetUserHeight",
                Marshallers.Create(x => x.ToByteArray(), GetUserHeightRequest.Parser.ParseFrom),
                Marshallers.Create(x => x.ToByteArray(), GetUserHeightResponse.Parser.ParseFrom)), [],
                (server, request, call) => server.Handle(request, call));
    }
}
