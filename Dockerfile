# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies (cached layer)
COPY ["PensionCalculationEngine/PensionCalculationEngine.csproj", "PensionCalculationEngine/"]
RUN dotnet restore "PensionCalculationEngine/PensionCalculationEngine.csproj"

# Copy source code
COPY ["PensionCalculationEngine/", "PensionCalculationEngine/"]

# Build and publish
WORKDIR "/src/PensionCalculationEngine"
RUN dotnet publish "PensionCalculationEngine.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Configure port (8080 as per requirements)
ENV PORT=8080
ENV ASPNETCORE_URLS=http://+:${PORT}
EXPOSE 8080

# Run application
ENTRYPOINT ["dotnet", "PensionCalculationEngine.dll"]
