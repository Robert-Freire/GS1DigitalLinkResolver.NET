# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY GS1DigitalLinkResolver.sln .
COPY src/GS1Resolver.Shared/GS1Resolver.Shared.csproj src/GS1Resolver.Shared/
COPY src/TestHarnessService/TestHarnessService.csproj src/TestHarnessService/
RUN dotnet restore src/TestHarnessService/TestHarnessService.csproj

# Copy source code
COPY src/GS1Resolver.Shared/ src/GS1Resolver.Shared/
COPY src/TestHarnessService/ src/TestHarnessService/
WORKDIR /src/src/TestHarnessService
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Copy static files (wwwroot) from build context
COPY src/TestHarnessService/wwwroot ./wwwroot

EXPOSE 5000
ENTRYPOINT ["dotnet", "TestHarnessService.dll"]
