# Error Handling Contract

This document defines the error handling contract for the GS1 Digital Link Resolver services.

## Exception Classes

All custom exceptions inherit from `ResolverException` and include an HTTP status code.

### Base Exception

**`ResolverException`** - Base class for all resolver exceptions
- **StatusCode**: `int` - HTTP status code
- **Message**: `string` - Error message
- **InnerException**: `Exception?` - Optional inner exception

### Typed Exceptions

**`NotFoundException`** - Resource not found (404)
```csharp
throw new NotFoundException("Resource not found");
```

**`ConflictException`** - Resource conflict (409)
```csharp
throw new ConflictException("Resource already exists");
```

**`ValidationException`** - Validation error (400)
```csharp
throw new ValidationException("Invalid input data");
```

## Error Response Format

All error responses follow the [RFC 7807 Problem Details](https://tools.ietf.org/html/rfc7807) format.

### Response Structure

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.{statusCode}",
  "title": "Error Title",
  "status": 400,
  "detail": "Detailed error message"
}
```

### Response Headers

- **Content-Type**: `application/json`
- **Status Code**: Matches the `status` field in the response body

## Error Types

### 400 Bad Request (ValidationException)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.400",
  "title": "Resolver Error",
  "status": 400,
  "detail": "Invalid GTIN format"
}
```

### 404 Not Found (NotFoundException)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.404",
  "title": "Resolver Error",
  "status": 404,
  "detail": "Entry not found for identifier: 01/09506000134352"
}
```

### 409 Conflict (ConflictException)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.409",
  "title": "Resolver Error",
  "status": 409,
  "detail": "Entry already exists for identifier: 01/09506000134352"
}
```

### 500 Internal Server Error (Unhandled Exception)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred. Please try again later."
}
```

### Database Errors (CosmosException)

Cosmos DB exceptions are automatically converted to structured error responses with appropriate status codes:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.{statusCode}",
  "title": "Database Error",
  "status": 503,
  "detail": "Service temporarily unavailable"
}
```

## Exception Middleware

The `ExceptionMiddleware` is registered in both services (`DataEntryService` and `WebResolverService`) and automatically translates all exceptions into the standardized error response format.

### Middleware Registration

In `Program.cs`, the exception middleware is registered **before** other middleware to ensure all exceptions are caught:

```csharp
// Add exception middleware BEFORE other middleware
app.UseMiddleware<ExceptionMiddleware>();
```

### Exception Handling Flow

1. **ResolverException** (and subclasses) - Logged as warnings, returned with custom status code
2. **CosmosException** - Logged as errors, returned with Cosmos DB status code
3. **Generic Exception** - Logged as errors, returned as 500 Internal Server Error

## Usage in Controllers

Controllers should throw typed exceptions instead of returning error results:

```csharp
// Good - Throw typed exception
if (entry == null)
{
    throw new NotFoundException($"Entry not found for identifier: {identifier}");
}

// Avoid - Returning error results directly
if (entry == null)
{
    return NotFound(new { error = "Entry not found" });
}
```

## Usage in Repositories

Repositories should throw typed exceptions for business logic errors:

```csharp
public async Task<ResolverEntry> GetEntryAsync(string identifier)
{
    var entry = await FindEntryAsync(identifier);

    if (entry == null)
    {
        throw new NotFoundException($"Entry not found for identifier: {identifier}");
    }

    return entry;
}
```

## Integration Guidelines

When integrating with these services, clients should:

1. Check the HTTP status code for error type classification
2. Parse the JSON response body for detailed error information
3. Use the `detail` field for user-facing error messages
4. Use the `type` field for error categorization and documentation links
5. Handle standard HTTP status codes (400, 404, 409, 500, 503)

## Error Logging

- **ResolverException** and subclasses are logged as **Warnings** with exception details
- **CosmosException** and generic exceptions are logged as **Errors** with full stack traces
- All error responses are sanitized to prevent information leakage in production
