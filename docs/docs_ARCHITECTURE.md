# Architecture – GS1 Digital Link Resolver CE (.NET 8)

This document describes the architecture and runtime behavior of the
.NET 8 port of the GS1 Digital Link Resolver CE.

---

## 🏗️ High-Level Overview

The system is implemented as **three stateless microservices**,
orchestrated via Docker Compose (or AKS in production):

1. **DataEntryService**
2. **WebResolverService**
3. **TestHarnessService**

Azure Cosmos DB is used as the persistent backend.
The official GS1 Node.js toolkit is executed as a real subprocess.

---

## 🧩 Services

### 1. DataEntryService (Port 3000)

**Purpose**
- Authenticated CRUD for GS1 linksets

**Responsibilities**
- Accept GS1 v3 JSON payloads
- Merge or append qualifier groups
- Upsert documents into Cosmos DB

**Security**
- Bearer token authentication (`SESSION_TOKEN`)

---

### 2. WebResolverService (Port 4000)

**Purpose**
- Public-facing GS1 Digital Link resolver

**Responsibilities**
- Parse GS1 paths
- Expand or compress identifiers
- Fetch linksets from Cosmos DB
- Apply content negotiation rules
- Return:
  - `307` redirect
  - `300` linkset JSON
  - `Link` headers

---

### 3. TestHarnessService (Port 5000)

**Purpose**
- UI for the official GS1 test suite

**Responsibilities**
- Execute GS1 compliance scenarios
- Validate resolver behavior end-to-end

---

## 🗄️ Data Layer

### Azure Cosmos DB

- Container: `resolver_test_v4`
- Partition key: `/Id`
- Document model mirrors GS1 CE Mongo schema
- Multiple `data[]` entries per anchor

---

## 🔧 GS1 Toolkit Integration

- Real Node.js GS1 Digital Link toolkit
- Executed via `Process.Start`
- Used for:
  - Path compression
  - Path expansion
  - Validation

**No mocks** are used in E2E tests.

---

## 🔁 Content Negotiation

Negotiation hierarchy exactly matches the GS1 CE:

1. Language (`Accept-Language`, q-values)
2. Context & media
3. `und`
4. First available link

---

## 🔄 End-to-End Flow (Example)

1. Client submits GS1 JSON via DataEntryService
2. Cosmos DB upsert (merge qualifiers)
3. Client resolves:
   `/01/{gtin}/10/{lot}?linktype=gs1:pip`
4. Toolkit expands path
5. Resolver loads document
6. Negotiation selects best link
7. `307 Location` returned

---

## ⚡ Performance Notes

- Performance characteristics are **theoretical**
- Stateless services allow horizontal scaling
- Expected low-latency resolution with proper indexing
- Cosmos DB RU provisioning determines throughput

---

## ☁️ Deployment

Recommended setup:

- Azure Cosmos DB
- AKS or Azure App Service
- NGINX as public resolver proxy

---

## 📜 Specification Compatibility

- GS1 Digital Link Resolver CE (archived Python reference)
- Linkset v3 JSON format
- Fully compatible with GS1 test harness
