using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using posts;
using posts.Services;

var serviceBroadcaster = new ServiceBroadcaster("meoworld.v1.blog.Blog", new DnsEndPoint(GetListeningAddress(), GetListeningPort()));
serviceBroadcaster.Start();

InitLogger();
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

void InitLogger()
{
    var config = new NLog.Config.LoggingConfiguration();

    var fileTarget = new NLog.Targets.FileTarget("logfile")
    {
        FileName = $"logs/posts-service-{System.Environment.MachineName}.log",
        Layout = "{\"level\":\"${level}\",\"time\":\"${longdate}\",\"msg\":\"${message}\",\"service\":\"posts\", \"hostname\":\"${hostname}\"}"
    };

    var consoleTarget = new NLog.Targets.ConsoleTarget("logconsole")
    {
        Layout = "${level} ${message}"
    };

    config.AddTarget(fileTarget);
    config.AddTarget(consoleTarget);

    config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, fileTarget);
    config.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, consoleTarget);

    NLog.LogManager.Configuration = config;
}

string GetListeningAddress()
{
    return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
        ? "localhost"
        : Environment.MachineName;
}

int GetListeningPort()
{
    return 5001;
}