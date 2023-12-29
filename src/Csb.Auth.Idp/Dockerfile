FROM mcr.microsoft.com/dotnet/aspnet:5.0.7-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0.301-buster-slim AS build
COPY ["src/", "src/"]
COPY ["protos/", "protos/"]
RUN dotnet restore "src/Csb.Auth.Idp/Csb.Auth.Idp.csproj"
RUN dotnet build "src/Csb.Auth.Idp/Csb.Auth.Idp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Csb.Auth.Idp/Csb.Auth.Idp.csproj" -c Release -o /app/publish -r debian.10-x64 -p:PublishReadyToRun=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# The curl CLI is used by Docker to health check the container.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
ENTRYPOINT ["dotnet", "Csb.Auth.Idp.dll"]