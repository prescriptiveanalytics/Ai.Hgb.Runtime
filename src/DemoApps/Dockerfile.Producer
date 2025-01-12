FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR .

# Copy everything
COPY "Producer/*.csproj" "Producer/"
COPY "Common/*.csproj" "Common/"

# Restore as distinct layers
RUN dotnet restore "Producer/Producer.csproj"

# Build and publish a release
COPY "Producer/" "Producer/"
COPY "Common/" "Common/"
WORKDIR /Producer
RUN dotnet publish -c Release -o out
COPY Producer/configurations /Producer/out/configurations

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /Producer
COPY --from=build-env /Producer/out .
ENTRYPOINT ["dotnet", "ai.hgb.application.demoapps.Producer.dll"]