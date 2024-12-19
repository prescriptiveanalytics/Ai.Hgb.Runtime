FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /Consumer

# Copy everything
COPY . ./

# Restore as distinct layers
RUN dotnet restore

# Build and publish a release
RUN dotnet publish -c Release -o out
COPY configurations /Consumer/out/configurations

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /Consumer
COPY --from=build-env /Consumer/out .
ENTRYPOINT ["dotnet", "ai.hgb.application.demoapps.Consumer.dll"]