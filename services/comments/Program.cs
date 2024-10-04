using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Meoworld.Mq;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using comments.Services;
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
        ServiceName = "comments",
        ServerAddress = "localhost",
        ServerPort = 5002
    };
    redisSubscriber.Publish(notificationChannel, Convert.ToBase64String(serviceStartedMessage.ToByteArray()));
}

void BuildAndRunServer()
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.ConfigureKestrel(
        options => options.ListenLocalhost(5002,
            listenOptions => listenOptions.Protocols = HttpProtocols.Http2)
    );
    builder.Services.AddGrpc();
    var app = builder.Build();
    app.MapGrpcService<CommentsService>();
    app.Run();
}