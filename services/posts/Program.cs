using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using posts;
using posts.Services;

var serviceBroadcaster = new ServiceBroadcaster("blog", new DnsEndPoint(GetListeningAddress(), GetListeningPort()));
serviceBroadcaster.Start();

BuildAndRunServer();

void BuildAndRunServer()
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.ConfigureKestrel(
        options => options.Listen(System.Net.IPAddress.Any, GetListeningPort())
    );
    builder.Services.AddGrpc().AddServiceOptions<PostsService>(options =>
    {
        options.Interceptors.Add<ServiceLoggerInterceptor>();
        options.Interceptors.Add<ConcurrencyLimitingInterceptor>();
    });
    builder.Services.AddSingleton(new ConcurrencyLimitingInterceptor(5));
    builder.Services.AddGrpcReflection();
    builder.Services.AddGrpcHealthChecks()
        .AddCheck("Check", () => new HealthCheckResult(HealthStatusManager.GetInstance().HealthStatus));

    var app = builder.Build();
    app.MapGrpcService<PostsService>();
    app.MapGrpcHealthChecksService();
    if (app.Environment.IsDevelopment())
    {
        app.MapGrpcReflectionService();
    }

    Console.WriteLine("Running on machine: {0}", System.Environment.MachineName);
    app.Run();
}

string GetListeningAddress()
{
    return System.Environment.MachineName;
}

int GetListeningPort()
{
    return 5001;
}