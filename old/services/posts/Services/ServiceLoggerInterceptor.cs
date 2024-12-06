using Grpc.Core;
using Grpc.Core.Interceptors;
using NLog;

namespace posts.Services;

public class ServiceLoggerInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        LogManager.GetCurrentClassLogger().Info("Request intercepted");
        return await continuation(request, context);
    }
}