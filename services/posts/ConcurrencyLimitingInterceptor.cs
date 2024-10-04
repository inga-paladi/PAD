using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Health.V1;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace posts;

public class ConcurrencyLimitingInterceptor(int maxConcurrentCalls) : Interceptor
{
    private readonly int _maxConcurrentCalls = maxConcurrentCalls;
    private readonly SemaphoreSlim _semaphore = new(maxConcurrentCalls, maxConcurrentCalls);

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        if (!_semaphore.Wait(0))
        {
            // HealthStatusManager.GetInstance().ServingStatus = HealthCheckResponse.Types.ServingStatus.NotServing;
            HealthStatusManager.GetInstance().HealthStatus = HealthStatus.Unhealthy;
            Console.WriteLine("Too many concurrent requests.");
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "Too many concurrent requests."));
        }

        Console.WriteLine("Take one. Current Count = {0}", _semaphore.CurrentCount);
        try
        {
            if (_semaphore.CurrentCount == 0) // No more thread can enter, no places available
            {
                // HealthStatusManager.GetInstance().ServingStatus = HealthCheckResponse.Types.ServingStatus.NotServing;
                HealthStatusManager.GetInstance().HealthStatus = HealthStatus.Unhealthy;
                Console.WriteLine("NotServing requests.");
            }
            
            return await continuation(request, context);
        }
        finally
        {
            if (_semaphore.Release() == 0) // Was full, now one space is free
            {
                // HealthStatusManager.GetInstance().ServingStatus = HealthCheckResponse.Types.ServingStatus.Serving;
                HealthStatusManager.GetInstance().HealthStatus = HealthStatus.Healthy;
            }
            Console.WriteLine("Release one. Current Count = {0}", _semaphore.CurrentCount);
        }
    }
}