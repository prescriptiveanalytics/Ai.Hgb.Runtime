FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR .

# Copy everything
COPY "Consumer/*.csproj" "Consumer/"
COPY "Common/*.csproj" "Common/"

# Restore as distinct layers
RUN dotnet restore "Consumer/Consumer.csproj"

# Build and publish a release
COPY "Consumer/" "Consumer/"
COPY "Common/" "Common/"
WORKDIR /Consumer
RUN dotnet publish -c Release -o out
COPY Consumer/configurations /Consumer/out/configurations

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /Consumer
COPY --from=build-env /Consumer/out .
ENTRYPOINT ["dotnet", "ai.hgb.application.demoapps.Consumer.dll"]