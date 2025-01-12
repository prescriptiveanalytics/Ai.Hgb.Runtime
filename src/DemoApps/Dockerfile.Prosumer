FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR .

# Copy everything
COPY "Prosumer/*.csproj" "Prosumer/"
COPY "Common/*.csproj" "Common/"

# Restore as distinct layers
RUN dotnet restore "Prosumer/Prosumer.csproj"

# Build and publish a release
COPY "Prosumer/" "Prosumer/"
COPY "Common/" "Common/"
WORKDIR /Prosumer
RUN dotnet publish -c Release -o out
COPY Prosumer/configurations /Prosumer/out/configurations

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /Prosumer
COPY --from=build-env /Prosumer/out .
ENTRYPOINT ["dotnet", "ai.hgb.application.demoapps.Prosumer.dll"]