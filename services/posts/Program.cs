using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Meoworld.Mq;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using posts;
using posts.Services;
using StackExchange.Redis;

NotifyAboutStart();
BuildAndRunServer();

void NotifyAboutStart()
{
    var serviceStartedEvent = EventType.ServiceStarted;
    var serviceStartedAttribute = serviceStartedEvent.GetType().GetMember(serviceStartedEvent.ToString()).Single().GetCustomAttribute<OriginalNameAttribute>();
    var notificationChannel = new RedisChannel(serviceStartedAttribute?.Name.ToString() ?? "", RedisChannel.PatternMode.Literal);

    var redis = ConnectionMultiplexer.Connect("localhost:6379");
    var redisSubscriber = redis.GetSubscriber();

    var serviceStartedMessage = new Meoworld.Mq.ServiceStarted
    {
        ServiceName = "blog",
        ServerAddress = "localhost",
        ServerPort = 5001
    };
    redisSubscriber.Publish(notificationChannel, Convert.ToBase64String(serviceStartedMessage.ToByteArray()));
}

void BuildAndRunServer()
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.ConfigureKestrel(
        options => options.ListenLocalhost(5001,
            listenOptions => listenOptions.Protocols = HttpProtocols.Http2)
    );
    builder.Services.AddGrpc().AddServiceOptions<PostsService>(options =>
    {
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

    app.Run();
}