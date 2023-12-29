FROM mcr.microsoft.com/dotnet/aspnet:5.0.7-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0.301-buster-slim AS build
COPY ["samples/", "samples/"]
RUN dotnet restore "samples/Csb.Auth.Samples.AuthorizationCodeMvc/Csb.Auth.Samples.AuthorizationCodeMvc.csproj"
RUN dotnet build "samples/Csb.Auth.Samples.AuthorizationCodeMvc/Csb.Auth.Samples.AuthorizationCodeMvc.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "samples/Csb.Auth.Samples.AuthorizationCodeMvc/Csb.Auth.Samples.AuthorizationCodeMvc.csproj" -c Release -o /app/publish -r debian.10-x64 -p:PublishReadyToRun=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Csb.Auth.Samples.AuthorizationCodeMvc.dll"]