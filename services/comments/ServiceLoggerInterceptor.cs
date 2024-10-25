using Grpc.Core;
using Grpc.Core.Interceptors;

namespace comments.Services;

public class ServiceLoggerInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        Console.WriteLine("Request intercepted");
        return await continuation(request, context);
    }
}