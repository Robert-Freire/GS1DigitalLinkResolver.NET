# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published app and wwwroot
COPY --from=publish /app/publish .

# Copy static files with proper directory structure
COPY test_suite/public/ /app/wwwroot/test-suites/resolver/1.0.0/public/
COPY test_suite/public/index.html /app/wwwroot/index.html

EXPOSE 5000
ENTRYPOINT ["dotnet", "TestHarnessService.dll"]
