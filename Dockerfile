FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["PensionCalculationEngine/PensionCalculationEngine.csproj", "PensionCalculationEngine/"]
RUN dotnet restore "PensionCalculationEngine/PensionCalculationEngine.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/PensionCalculationEngine"
RUN dotnet publish "PensionCalculationEngine.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Configure port
ENV PORT=8080
ENV ASPNETCORE_URLS=http://+:${PORT}
EXPOSE 8080

ENTRYPOINT ["dotnet", "PensionCalculationEngine.dll"]
