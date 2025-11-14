# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["ImageTextExtractor.csproj", "./"]
RUN dotnet restore "ImageTextExtractor.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "ImageTextExtractor.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ImageTextExtractor.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install required system dependencies
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Create directories for output
RUN mkdir -p /app/OutputFiles /app/Prompts /app/Schemas

# Copy prompt templates and schemas
COPY ["Prompts/", "/app/Prompts/"]

# Set environment variables (override at runtime)
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "ImageTextExtractor.dll"]
