using Grpc.Health.V1;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace comments;

public class HealthStatusManager
{
    private static readonly HealthStatusManager Instance = new();

    public static HealthStatusManager GetInstance()
    {
        return Instance;
    }

    // public HealthCheckResponse.Types.ServingStatus ServingStatus { get; set; } = HealthCheckResponse.Types.ServingStatus.Unknown;
    public HealthStatus HealthStatus { get; set; } = HealthStatus.Healthy;

    public HealthStatus GetHealthStatus()
    {
        // Console.WriteLine("Machine {0}. Get health status: {1}", System.Environment.MachineName, HealthStatus.ToString());
        return HealthStatus;
    }
}