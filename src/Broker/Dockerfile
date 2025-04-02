FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /Broker

# Copy everything
COPY . ./

# Restore as distinct layers
RUN dotnet restore

# Build and publish a release
RUN dotnet publish -c Release -o out
COPY configurations /Broker/out/configurations

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /Broker
COPY --from=build-env /Broker/out .
ENTRYPOINT ["dotnet", "Ai.Hgb.Runtime.Broker.dll"]