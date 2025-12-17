# GS1Resolver.Shared.Tests

Comprehensive test suite for the GS1Resolver.Shared project, focusing on the GS1ToolkitService infrastructure.

## Test Organization

### Unit Tests
Unit tests use mocked dependencies to test the GS1ToolkitService in isolation:

- **GS1ToolkitServiceTests.cs**: Tests all public methods of GS1ToolkitService
  - ValidateAiDataStringAsync tests
  - TestDigitalLinkSyntaxAsync tests
  - UncompressDigitalLinkAsync tests
  - CompressDigitalLinkAsync tests
  - AnalyzeDigitalLinkAsync tests
  - Constructor validation tests

These tests use Moq to mock the IProcessExecutor interface, allowing fast execution without requiring Node.js or actual GS1 toolkit scripts.

### Integration Tests
Integration tests run against actual Node.js processes and GS1 toolkit scripts:

- **GS1ToolkitServiceIntegrationTests.cs**: End-to-end validation tests
  - Real Node.js process execution
  - Actual GS1 toolkit script validation
  - Round-trip compression/uncompression
  - Process timeout handling
  - Error handling scenarios

- **GS1ResolverEndToEndTests.cs**: Full-stack end-to-end integration tests
  - Uses `WebApplicationFactory` to spin up both DataEntryService and WebResolverService in-memory
  - Tests complete CRUD operations on resolver data
  - Validates GS1 Digital Link resolution with qualifiers (serial, lot)
  - Tests compression/uncompression round-trips
  - Validates linkset format (JSON/linkset+json)
  - Tests language and linktype content negotiation
  - Validates error handling (400/404/300 status codes)
  - Tests GIAI asset resolution (fixed and variable)
  - Replicates all test scenarios from the Python test suite (`tests/setup_test.py`)

Integration tests are marked with `[Trait("Category", "Integration")]` and require:
- Node.js installed and available in PATH
- GS1 Digital Link toolkit scripts available at configured path
- Cosmos DB Emulator (localhost:8081) or Azure Cosmos DB test instance
- Tests automatically skip if prerequisites are not met

## Running Tests

### Run All Unit Tests (Fast)
```bash
dotnet test --filter "Category!=Integration"
```

### Run All Tests (Including Integration)
```bash
dotnet test
```

### Run Only Integration Tests
```bash
dotnet test --filter "Category=Integration"
```

### Run Only End-to-End Tests
```bash
dotnet test --filter "FullyQualifiedName~GS1ResolverEndToEndTests"
```

### Run Specific End-to-End Test
```bash
dotnet test --filter "FullyQualifiedName~GS1ResolverEndToEndTests.Test05_ResolveGtin_ShouldReturn307Redirect"
```

### Run Tests with Code Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Architecture

### Process Abstraction Layer
The test suite validates the process abstraction pattern:

- **IProcessExecutor**: Interface for executing external processes
- **ProcessExecutor**: Production implementation that spawns real processes
- **Mock<IProcessExecutor>**: Test implementation for unit testing

This architecture allows:
- Fast unit tests without spawning processes
- Controlled testing of error conditions
- Integration tests that validate actual Node.js behavior
- Easy mocking of process execution in consuming services

### Test Fixtures and Cleanup
- Unit tests create temporary script files for File.Exists validation
- Integration tests automatically detect Node.js and toolkit availability
- Tests clean up temporary resources via IDisposable pattern

## CI/CD Integration

### Recommended CI Pipeline Configuration

```yaml
# Run unit tests on every build (fast)
- name: Unit Tests
  run: dotnet test --filter "Category!=Integration" --logger "trx;LogFileName=unit-tests.trx"

# Run integration tests only on main branch or PR to main
- name: Integration Tests
  if: github.ref == 'refs/heads/main' || github.event_name == 'pull_request'
  run: dotnet test --filter "Category=Integration" --logger "trx;LogFileName=integration-tests.trx"
  env:
    GS1_TOOLKIT_PATH: /path/to/gs1-digitallink-toolkit
```

### Docker Environment
Integration tests are designed to work in Docker containers where the GS1 toolkit is mounted at `/app/gs1-digitallink-toolkit`.

### Local Development
For local development, integration tests will automatically search for the toolkit in:
1. `/app/gs1-digitallink-toolkit` (Docker default)
2. `../../data_entry_server/src/gs1-digitallink-toolkit` (relative to repository root)
3. `./gs1-digitallink-toolkit` (current directory)

## Test Coverage

Current test coverage includes:
- All public methods of GS1ToolkitService
- Success scenarios with valid inputs
- Error scenarios (null inputs, invalid data, process failures)
- Edge cases (empty strings, malformed JSON, timeouts)
- Constructor validation and initialization
- Process timeout and cancellation handling
- Real Node.js process execution (integration tests)

### Coverage Metrics
Run `dotnet test --collect:"XPlat Code Coverage"` to generate detailed coverage reports.

## Dependencies

- **xUnit**: Test framework
- **Moq**: Mocking library for unit tests
- **Microsoft.NET.Test.Sdk**: Test SDK
- **coverlet.collector**: Code coverage collection
- **Microsoft.AspNetCore.Mvc.Testing**: WebApplicationFactory for integration testing
- **Microsoft.AspNetCore.TestHost**: In-memory test server for ASP.NET Core
- **FluentAssertions**: Fluent assertion library for more readable tests

## End-to-End Test Configuration

### Environment Variables
The end-to-end tests can be configured using environment variables:

- `COSMOS_CONNECTION_STRING`: Connection string for Cosmos DB (defaults to localhost emulator)
- `SESSION_TOKEN`: Bearer token for Data Entry API authentication (defaults to "secret")
- `FQDN`: Fully qualified domain name for resolver (defaults to "localhost:8080")

### Test Data
The end-to-end tests use JSON test files located in `TestData/`:
- `test_01_09506000134376.json`: GTIN with PIL, certificationInfo, and registerProduct links
- `test_01_09506000134352.json`: GTIN with multiple linktypes and languages
- `test_8004_095060001343.json`: GIAI variable asset template
- `test_8004_0950600013430000001.json`: GIAI fixed asset

### Test Execution Flow
1. **InitializeAsync**: Loads test data files and spins up both services in-memory
2. **Test01-04**: CRUD cycle tests (Create, Read, Delete)
3. **Test05-07**: Basic GS1 Digital Link resolution tests
4. **Test08-09**: Compression/uncompression tests
5. **Test10**: Linkset format tests
6. **Test11-12**: Language and linktype negotiation tests
7. **Test13-15**: Error handling tests (400/404/300)
8. **Test16-17**: GIAI asset resolution tests
9. **DisposeAsync**: Cleans up services and resources

## Troubleshooting

### Integration Tests Skipping
If integration tests are being skipped, verify:
1. Node.js is installed: `node --version`
2. GS1 toolkit scripts exist at expected path
3. Check test output for specific skip reasons

### End-to-End Tests Failing
If end-to-end tests fail, verify:
1. Cosmos DB Emulator is running: `https://localhost:8081/_explorer/index.html`
2. Test data JSON files are copied to output directory
3. GS1 toolkit is available for compression tests
4. Check service logs for initialization errors

### Build Errors
Ensure all NuGet packages are restored:
```bash
dotnet restore
dotnet build
```

### Test Failures
Check that:
- GS1ToolkitService constructor receives IProcessExecutor dependency
- DI container registers IProcessExecutor -> ProcessExecutor mapping
- Test files are properly cleaned up after execution
