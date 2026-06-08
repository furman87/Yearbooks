# Use the official ASP.NET Core runtime image for Linux
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
# Changed from 8080 to 8387
EXPOSE 8387

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files (adjust path since build context is parent directory)
COPY ["src/YearbookViewer/YearbookViewer.csproj", "YearbookViewer/"]

# Restore dependencies
RUN dotnet restore "YearbookViewer/YearbookViewer.csproj"

# Copy source code
COPY ["src/YearbookViewer/", "YearbookViewer/"]
WORKDIR "/src/YearbookViewer"

# Build the application
RUN dotnet build "YearbookViewer.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "YearbookViewer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage - runtime image
FROM base AS final
WORKDIR /app

# Install necessary packages for image processing and health checks
RUN apk add --no-cache \
    libgdiplus-dev \
    curl \
    tzdata \
    && rm -rf /var/cache/apk/*

# Set timezone (optional - adjust as needed)
ENV TZ=America/New_York
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Copy the published application
COPY --from=publish /app/publish .

# Create directory for yearbook data and logs
RUN mkdir -p /app/yearbooks /app/logs

# Set environment variables - Changed to port 8387
ENV ASPNETCORE_URLS=http://+:8387
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV YearbookPath=/app/yearbooks

# Create a non-root user for security and set proper permissions
RUN addgroup -g 1000 appgroup && \
    adduser -u 1000 -G appgroup -s /bin/sh -D appuser && \
    chown -R appuser:appgroup /app && \
    chmod -R 755 /app

# Ensure the dotnet runtime has execute permissions (fix for Linux)
RUN chmod +x /usr/bin/dotnet || true

# Switch to non-root user
USER appuser

# Health check - Updated to use port 8387
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8387/health || exit 1

# Start the application
ENTRYPOINT ["dotnet", "YearbookViewer.dll"]
