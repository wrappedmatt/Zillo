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

# Copy project files for restore (use Docker-specific csproj without SPA reference)
COPY ["Zillo.Dashboard.Server/Zillo.Dashboard.Server.Docker.csproj", "Zillo.Dashboard.Server/Zillo.Dashboard.Server.csproj"]
COPY ["Zillo.Domain/Zillo.Domain.csproj", "Zillo.Domain/"]
COPY ["Zillo.Application/Zillo.Application.csproj", "Zillo.Application/"]
COPY ["Zillo.Infrastructure/Zillo.Infrastructure.csproj", "Zillo.Infrastructure/"]

# Copy frontend package files
COPY ["zillo.dashboard.client/package.json", "zillo.dashboard.client/"]
COPY ["zillo.dashboard.client/package-lock.json", "zillo.dashboard.client/"]

# Install frontend dependencies
WORKDIR "/src/zillo.dashboard.client"
RUN npm ci

# Copy frontend source and build
WORKDIR /src
COPY ["zillo.dashboard.client/", "zillo.dashboard.client/"]
WORKDIR "/src/zillo.dashboard.client"
RUN npm run build

# Copy the built frontend to wwwroot
RUN mkdir -p /src/Zillo.Dashboard.Server/wwwroot && \
    cp -r dist/* /src/Zillo.Dashboard.Server/wwwroot/

# Restore .NET dependencies
WORKDIR /src
RUN dotnet restore "./Zillo.Dashboard.Server/Zillo.Dashboard.Server.csproj"

# Copy backend source
COPY ["Zillo.Domain/", "Zillo.Domain/"]
COPY ["Zillo.Application/", "Zillo.Application/"]
COPY ["Zillo.Infrastructure/", "Zillo.Infrastructure/"]
COPY ["Zillo.Dashboard.Server/", "Zillo.Dashboard.Server/"]

# Use the Docker-specific csproj (overwrite the regular one)
COPY ["Zillo.Dashboard.Server/Zillo.Dashboard.Server.Docker.csproj", "Zillo.Dashboard.Server/Zillo.Dashboard.Server.csproj"]

# Build backend
WORKDIR "/src/Zillo.Dashboard.Server"
RUN dotnet build "./Zillo.Dashboard.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Zillo.Dashboard.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Copy certificates for wallet functionality
COPY Zillo.Dashboard.Server/Certs ./Certs

# Set environment variables for production
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Zillo.Dashboard.Server.dll"]
