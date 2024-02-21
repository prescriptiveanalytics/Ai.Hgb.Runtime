FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /Producer

# Copy everything
COPY . ./

# Restore as distinct layers
RUN dotnet restore

# Build and publish a release
RUN dotnet publish -c Release -o out
COPY configurations /Producer/out/configurations

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /Producer
COPY --from=build-env /Producer/out .
ENTRYPOINT ["dotnet", "Producer.dll"]