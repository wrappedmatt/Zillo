# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS with-node
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

FROM with-node AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["LemonadeApp.Dashboard.Server/LemonadeApp.Dashboard.Server.csproj", "LemonadeApp.Dashboard.Server/"]
COPY ["LemonadeApp.Domain/LemonadeApp.Domain.csproj", "LemonadeApp.Domain/"]
COPY ["LemonadeApp.Application/LemonadeApp.Application.csproj", "LemonadeApp.Application/"]
COPY ["LemonadeApp.Infrastructure/LemonadeApp.Infrastructure.csproj", "LemonadeApp.Infrastructure/"]
COPY ["lemonadeapp.dashboard.client/lemonadeapp.dashboard.client.esproj", "lemonadeapp.dashboard.client/"]
RUN dotnet restore "./LemonadeApp.Dashboard.Server/LemonadeApp.Dashboard.Server.csproj"
COPY . .
WORKDIR "/src/LemonadeApp.Dashboard.Server"
RUN dotnet build "./LemonadeApp.Dashboard.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./LemonadeApp.Dashboard.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app

# Copy certificates for wallet functionality
COPY --from=publish /app/publish .
COPY LemonadeApp.Dashboard.Server/Certs ./Certs

# Set environment variables for production
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "LemonadeApp.Dashboard.Server.dll"]
