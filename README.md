# GS1 Digital Link Resolver CE (.NET 8 Port)

![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)
![Docker](https://img.shields.io/badge/Docker-Ready-blue)
![Tests](https://img.shields.io/badge/Tests-137%2B%20Passing-brightgreen)
![CosmosDB](https://img.shields.io/badge/Azure-Cosmos%20DB-blue)

**GS1 Digital Link Resolver CE — fully ported to .NET 8**  
From **Python / Flask / MongoDB / Node** to **ASP.NET Core / Azure Cosmos DB / Node**.

✅ 100% behavioral parity with the archived GS1 reference implementation  
🧪 137+ tests passing (unit + E2E)  
🐳 Docker & cloud ready

---
## Prerequisites

- **Node.js 20+** - Required for GS1 Digital Link Toolkit integration
- **.NET 8 SDK** - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- **Azure Cosmos DB** - Either:
  - Azure Cosmos DB account, or
  - [Cosmos DB Emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/how-to-develop-emulator?tabs=windows%2Ccsharp&pivots=api-nosql) (recommended for local development)

### Setting Up Cosmos DB Emulator for Docker (Windows)

If you're running Docker containers on Windows, the Cosmos DB emulator needs to accept network connections:

1. **Stop the emulator** (right-click system tray icon → Exit)

2. **Start with network access** (PowerShell as Administrator):
   ```powershell
   & "C:\Program Files\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe" /AllowNetworkAccess /Key=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==
   ```

3. **Verify network listening**:
   ```powershell
   netstat -an | findstr "8081"
   # Should show: TCP    0.0.0.0:8081    (not 127.0.0.1:8081)
   ```

The Docker services are already configured to connect to `host.docker.internal:8081`.

## 🚀 Quickstart

### Local Development

```bash
git clone https://github.com/user/GS1DigitalLinkResolver.NET
cd GS1DigitalLinkResolver.NET
dotnet build
dotnet test
```

### Docker Setup

1. **Start the services**:
   ```bash
   docker-compose up --build
   ```

2. **Add data to the resolver**:

   The database starts empty. You have several options to populate it:

   **Option 1: Use Swagger UI (Recommended for getting started)**
   - Open the Data Entry API Swagger: http://localhost:8080/api/v1/resolver/swagger
   - Use the POST `/api/v1/resolver` endpoint to add entries
   - Click "Try it out" and paste example JSON from `src/GS1Resolver.Shared.Tests/TestData/`

   **Option 2: Use curl or Postman**
   ```bash
   curl -X POST http://localhost:8080/api/v1/resolver \
     -H "Authorization: Bearer secret" \
     -H "Content-Type: application/json" \
     -d '{
       "anchor": "/01/09506000134376",
       "itemDescription": "Dal Giardino Medicinal Compound",
       "defaultLinktype": "gs1:pip",
       "links": [
         {
           "linktype": "gs1:pip",
           "href": "https://dalgiardino.com/medicinal-compound/pil.html",
           "title": "Product Information",
           "type": "text/html",
           "hreflang": ["en"]
         }
       ]
     }'
   ```
   See example data files in `src/GS1Resolver.Shared.Tests/TestData/` for complete examples with qualifiers, multiple links, and different GS1 key types.

3. **Test the resolver**:
   ```bash
   curl -i http://localhost:8080/01/09506000134376
   # → 307 redirect to product information
   ```

   Or visit:
   - Web Resolver Swagger: http://localhost:8080/swagger
   - Data Entry Swagger: http://localhost:8080/api/v1/resolver/swagger

### Test Harness Service (Optional)

The Test Harness Service is included in docker-compose and provides the official **GS1 Digital Link Resolver Test Suite** plus testing utilities.

**Access the Test Suite:**
- Open http://localhost:4001 in your browser
- The GS1 Digital Link Test Suite will load automatically
- Enter your resolver URL (e.g., `http://localhost:8080`) to run conformance tests
- Tests validate GS1 Digital Link 1.1 compliance including HTTP headers, redirects, and content negotiation

**Test API Endpoints:**

The service also provides programmatic test endpoints on port 4001:

```bash
# Test external URLs
curl "http://localhost:4001/api?test=getHTTPversion&testVal=https://example.com"

# Test the resolver (use internal Docker network names from inside Docker)
curl "http://localhost:4001/api?test=getAllHeaders&testVal=http://frontend-proxy-server/01/09506000134376"

# Or test the web service directly
curl "http://localhost:4001/api?test=getAllHeaders&testVal=http://resolver-web-server:4000/01/09506000134376"
```

**Available API tests:**
- `getHTTPversion` - Check HTTP protocol version (HTTP/1.1, HTTP/2, etc.)
- `getAllHeaders` - Get all HTTP headers from a response

This service is useful for verifying GS1 Digital Link conformance and checking HTTP headers, redirects, and protocol versions.

---

## ✨ Features

- Full GS1 Digital Link CE spec (GTIN / SSCC / GIAI)
- Qualifiers: serial, lot
- 307 redirect & 300 linkset JSON
- Compression / expansion
- Language, linktype, context & media negotiation
- Cosmos DB persistence
- Real Node.js GS1 toolkit (no mocks)

---

## 🧪 Tests

```bash
dotnet test
```

- 127+ tests
- Official GS1 test harness supported
- Cosmos emulator or InMemory fallback

---

## 📚 Documentation

- [Architecture](docs/ARCHITECTURE.md)
- Swagger:
  - https://localhost:3000/swagger
  - https://localhost:4000/swagger

---

## 🤝 Credits

- GS1 (spec & original CE)
- Traycer.AI (AI-assisted migration)
- You (port, tests, validation)

⭐ Issues & PRs welcome
