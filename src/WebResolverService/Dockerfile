# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY GS1DigitalLinkResolver.sln .
COPY src/GS1Resolver.Shared/GS1Resolver.Shared.csproj src/GS1Resolver.Shared/
COPY src/WebResolverService/WebResolverService.csproj src/WebResolverService/
RUN dotnet restore src/WebResolverService/WebResolverService.csproj

# Copy source code
COPY src/GS1Resolver.Shared/ src/GS1Resolver.Shared/
COPY src/WebResolverService/ src/WebResolverService/
WORKDIR /src/src/WebResolverService
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install Node.js 18.x for GS1 toolkit
RUN apt-get update && apt-get install -y curl && \
    curl -sL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && rm -rf /var/lib/apt/lists/*

# Copy GS1 toolkit
COPY gs1-digitallink-toolkit /app/gs1-digitallink-toolkit
WORKDIR /app/gs1-digitallink-toolkit
RUN npm ci --only=production

# Copy static files
COPY  src/WebResolverService/public /app/public

# Copy published app
WORKDIR /app
COPY --from=publish /app/publish .

ENV GS1TOOLKIT_TOOLKITPATH=/app/gs1-digitallink-toolkit

EXPOSE 4000
ENTRYPOINT ["dotnet", "WebResolverService.dll"]
