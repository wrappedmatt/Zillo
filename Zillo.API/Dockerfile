# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore
COPY ["Zillo.API/Zillo.API.csproj", "Zillo.API/"]
COPY ["Zillo.Domain/Zillo.Domain.csproj", "Zillo.Domain/"]
COPY ["Zillo.Application/Zillo.Application.csproj", "Zillo.Application/"]
COPY ["Zillo.Infrastructure/Zillo.Infrastructure.csproj", "Zillo.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "./Zillo.API/Zillo.API.csproj"

# Copy all source code
COPY ["Zillo.Domain/", "Zillo.Domain/"]
COPY ["Zillo.Application/", "Zillo.Application/"]
COPY ["Zillo.Infrastructure/", "Zillo.Infrastructure/"]
COPY ["Zillo.API/", "Zillo.API/"]

# Build
WORKDIR "/src/Zillo.API"
RUN dotnet build "./Zillo.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "./Zillo.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "Zillo.API.dll"]
