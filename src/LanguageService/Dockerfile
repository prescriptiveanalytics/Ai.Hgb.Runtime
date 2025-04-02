#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
RUN dotnet dev-certs https

WORKDIR /src
COPY ["LanguageService/LanguageService.csproj", "LanguageService/"]
RUN dotnet restore "./LanguageService/./LanguageService.csproj"
COPY . .
WORKDIR "/src/LanguageService"
RUN dotnet build "./LanguageService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./LanguageService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /root/.dotnet/corefx/cryptography/x509stores/my/* /root/.dotnet/corefx/cryptography/x509stores/my/
COPY --from=publish /app/publish .

EXPOSE 7003

ENTRYPOINT ["dotnet", "Ai.Hgb.Runtime.LanguageService.dll"]