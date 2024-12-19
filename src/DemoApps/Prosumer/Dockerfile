FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /Prosumer

# Copy everything
COPY . ./

# Restore as distinct layers
RUN dotnet restore

# Build and publish a release
RUN dotnet publish -c Release -o out
COPY configurations /Prosumer/out/configurations

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /Prosumer
COPY --from=build-env /Prosumer/out .
ENTRYPOINT ["dotnet", "ai.hgb.application.demoapps.Prosumer.dll"]