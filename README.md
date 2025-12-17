# GS1 Digital Link Resolver CE (.NET 8 Port)

![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)
![Docker](https://img.shields.io/badge/Docker-Ready-blue)
![Tests](https://img.shields.io/badge/Tests-137%2B%20Passing-brightgreen)
![CosmosDB](https://img.shields.io/badge/Azure-Cosmos%20DB-blue)

**Official GS1 Digital Link Resolver CE — fully ported to .NET 8**  
From **Python / Flask / MongoDB / Node** to **ASP.NET Core / Azure Cosmos DB / real Node.js toolkit**.

✅ 100% behavioral parity with the archived GS1 reference implementation  
🧪 137+ tests passing (unit + E2E)  
🐳 Docker & cloud ready

---

## 🚀 Quickstart

```bash
git clone https://github.com/user/GS1DigitalLinkResolver.NET
cd GS1DigitalLinkResolver.NET
dotnet build
dotnet test
```

```bash
docker-compose up --build
curl localhost:8080/01/09506000134376
# → 307 redirect
```

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

- 137+ tests
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
