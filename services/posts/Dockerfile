FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dotnet-sdk

WORKDIR /usr/src

COPY proto/ proto/
COPY services/shared services/shared
COPY services/posts services/posts

WORKDIR /usr/src/services/posts

RUN dotnet restore
RUN dotnet publish --configuration Release

FROM mcr.microsoft.com/dotnet/aspnet:8.0

STOPSIGNAL SIGINT
EXPOSE 5001

WORKDIR /usr/src/app
COPY --from=dotnet-sdk /usr/src/services/posts/bin/Release/net8.0/publish ./

ENTRYPOINT [ "dotnet", "posts.dll" ]