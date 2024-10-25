using System.Net;
using System.Reflection;
using System.Timers;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Meoworld.Mq;
using StackExchange.Redis;
using Timer = System.Timers.Timer;

public class ServiceBroadcaster
{
    private readonly RedisChannel _notificationChannel;
    private readonly ServiceDiscoveryBeacon _serviceDiscoveryBeacon;
    private ConnectionMultiplexer? _redisConnection = null;
    private readonly string _redisAddress;
    private readonly Timer _notifyOnInactivityTimer;

    public ServiceBroadcaster(string serviceName, DnsEndPoint endPoint, TimeSpan? broadcastInterval = null)
    {
        broadcastInterval ??= TimeSpan.FromSeconds(60);
        const EventType serviceStartedEvent = EventType.ServiceDiscoveryBeacon;
        var serviceStartedAttribute = serviceStartedEvent.GetType().GetMember(serviceStartedEvent.ToString()).Single().GetCustomAttribute<OriginalNameAttribute>();
        _notificationChannel = new RedisChannel(serviceStartedAttribute?.Name.ToString() ?? "", RedisChannel.PatternMode.Literal);
        _redisAddress = Environment.GetEnvironmentVariable("REDIS_ADDRESS") ?? "localhost";
        _serviceDiscoveryBeacon = new ServiceDiscoveryBeacon
        {
            ServiceName = serviceName,
            ServerAddress = endPoint.Host,
            ServerPort = (uint)endPoint.Port
        };
        _notifyOnInactivityTimer = new Timer((TimeSpan)broadcastInterval);
        _notifyOnInactivityTimer.Elapsed += OnTimeoutElapsed;
    }

    private void OnTimeoutElapsed(object? _, ElapsedEventArgs __)
    {
        Broadcast();
    }

    private void Broadcast()
    {
        try
        {
            _redisConnection ??= ConnectionMultiplexer.Connect(_redisAddress + ":6379");
            _redisConnection?.GetSubscriber().Publish(_notificationChannel,
                Convert.ToBase64String(_serviceDiscoveryBeacon.ToByteArray()));
        }
        catch (RedisConnectionException)
        {
            _redisConnection = null;
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    public void Start()
    {
        Broadcast();
        _notifyOnInactivityTimer.Start();
    }
}