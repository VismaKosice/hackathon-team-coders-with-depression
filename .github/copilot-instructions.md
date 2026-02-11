# GitHub Copilot Instructions — Pension Calculation Engine

## Purpose and Scope

This repository contains a **high-performance pension calculation engine** for the Visma Performance Hackathon. The engine processes ordered sequences of mutations (operations) to calculate pension entitlements for participants.

**What this project IS:**
- A stateless, compute-focused REST API (single endpoint: `POST /calculation-requests`)
- C# / ASP.NET Core 10 with Controllers (NOT Minimal APIs)
- Performance-critical — correctness first, then optimization
- Docker-deployable (must run on port 8080)
- OpenAPI 3.0 documented via Swashbuckle

**What this project IS NOT:**
- No database or persistence layer
- No CRUD operations
- No authentication or authorization
- No logging infrastructure (do NOT add ILogger calls, Serilog, or other logging frameworks unless explicitly requested)
- No background jobs or queues

---

## Architecture and Project Layout

Follow this structure for all new code:

```
/
├── Controllers/                # API controllers (thin, route to services)
├── Models/                     # DTOs, request/response models
│   ├── Request/                # Incoming request DTOs
│   ├── Response/               # Outgoing response DTOs
│   └── Domain/                 # Domain models (Dossier, Policy, Person, etc.)
├── Services/                   # Business logic and computation
│   ├── MutationHandlers/       # Individual mutation handlers
│   └── Validators/             # FluentValidation validators
├── Validators/                 # Alternative location for validators (align with existing)
├── Dockerfile                  # Docker deployment file
└── Program.cs                  # Application entry point
```

**Dependency Injection:**
- Register services in `Program.cs` using `builder.Services.AddScoped<T>()` or `AddSingleton<T>()` as appropriate
- Use constructor injection in controllers and services
- Prefer scoped lifetime for stateful services, singleton for stateless utilities

**Controller Conventions:**
- Use `[ApiController]` attribute
- Use attribute routing (e.g., `[Route("api/[controller]")]` or explicit routes)
- Keep controllers thin — delegate to services
- Use explicit `[FromBody]`, `[FromRoute]`, `[FromQuery]` attributes where helpful for clarity

---

## API Design Conventions

### Endpoint and Versioning

**Single endpoint (as per `api-spec.yaml`):**
- `POST /calculation-requests`

**Versioning strategy:**
- Currently no versioning (hackathon scope)
- If versioning is needed in future: use URL-based versioning (`/api/v1/calculation-requests`)

### Request/Response Format

**Content-Type:** `application/json`

**Request:**
- `CalculationRequest` with `tenant_id` and `calculation_instructions.mutations[]`
- See `api-spec.yaml` for full schema

**Response (HTTP 200):**
- Always return HTTP 200 for successfully processed calculations, even if the calculation outcome is `FAILURE`
- Response includes:
  - `calculation_metadata`: UUID, timestamps, duration, outcome (SUCCESS/FAILURE)
  - `calculation_result`: messages, mutations, initial_situation, end_situation

**Error Responses:**
- HTTP 400: Malformed request (cannot be parsed or violates schema)
- HTTP 500: Unexpected server error
- Use `ProblemDetails` or `ValidationProblemDetails` for error bodies

### Model Binding

- Use `[FromBody]` for request body (already implicit with `[ApiController]`, but can be explicit for clarity)
- Use `[FromRoute]` and `[FromQuery]` explicitly where parameters come from URL/query string

### JSON Patch (Bonus Feature)

If implementing the bonus JSON Patch features:
- Use `Microsoft.AspNetCore.JsonPatch` and `JsonPatchDocument<T>`
- JSON Patch format: RFC 6902
- **Note:** JSON Patch in ASP.NET Core typically requires `Newtonsoft.Json` compatibility. If adding JSON Patch support, include `Microsoft.AspNetCore.Mvc.NewtonsoftJson` and configure it explicitly in `Program.cs`:
  ```csharp
  builder.Services.AddControllers()
      .AddNewtonsoftJson();
  ```
- Keep Newtonsoft.Json usage isolated to JSON Patch; use System.Text.Json everywhere else
- Document this dependency clearly in code comments

**JSON Patch Operations:**
- `forward_patch_to_situation_after_this_mutation`: transforms previous situation → current situation
- `backward_patch_to_previous_situation`: transforms current situation → previous situation
- Validate that applying patches produces exact matches

---

## Validation Conventions

### DataAnnotations (Simple DTO Validation)

Use DataAnnotations on request DTOs for simple validations:
- `[Required]`
- `[Range(min, max)]`
- `[StringLength(max)]`
- `[RegularExpression]`

**Example:**
```csharp
public class CalculationRequest
{
    [Required]
    [StringLength(25)]
    [RegularExpression("[a-z0-9]+(?:_[a-z0-9]+)*")]
    public string TenantId { get; set; }
}
```

### FluentValidation (Complex/Business Rule Validation)

Use **FluentValidation** for:
- Cross-field validation
- Business rule validation (e.g., "dossier must exist", "salary must be >= 0")
- Mutation-specific validation logic

**Where validators live:**
- `Services/Validators/` or `Validators/` (align with existing structure)
- One validator per DTO or mutation type

**Registration:**
```csharp
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
```

**Usage:**
- Inject `IValidator<T>` into services/handlers
- Call `validator.ValidateAsync(model)` and check `ValidationResult.IsValid`
- Convert validation errors into `CalculationMessage` objects (with severity: CRITICAL/WARNING)

**Response Format:**
- Do NOT throw exceptions for business validation failures
- Validation failures → add to `calculation_result.messages[]`
- CRITICAL messages → halt processing, set `calculation_outcome = FAILURE`
- WARNING messages → continue processing

---

## Error Handling Conventions

### ProblemDetails Responses

Use standardized error responses:
- `ProblemDetails` for generic errors (HTTP 500)
- `ValidationProblemDetails` for validation errors (HTTP 400)

**Example (HTTP 400):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "tenant_id": ["The tenant_id field is required."]
  }
}
```

### Exception Handling

- Do NOT leak internal details in error messages
- Return safe, generic messages to clients
- Log exceptions internally only if logging is explicitly requested

### Calculation Messages (Business Validation)

Mutations produce `CalculationMessage` objects:
```csharp
public class CalculationMessage
{
    public string Code { get; set; }          // e.g., "DOSSIER_NOT_FOUND"
    public string Severity { get; set; }      // "CRITICAL" or "WARNING"
    public string Message { get; set; }       // Human-readable description
}
```

**Message Codes (see README.md):**
- `DOSSIER_ALREADY_EXISTS`, `DOSSIER_NOT_FOUND`, `INVALID_SALARY`, `NO_POLICIES`, `NOT_ELIGIBLE`, etc.

**Processing Rules:**
- CRITICAL → halt processing immediately, do not apply the failing mutation
- WARNING → record message, continue processing

---

## Performance Guidelines

This is a **performance-critical** hackathon project. Follow these rules:

### Avoid Allocations in Hot Paths
- Reuse collections where possible (e.g., `List<T>.Clear()` instead of `new List<T>()`)
- Use `Span<T>`, `ReadOnlySpan<T>`, `ArrayPool<T>` for temporary buffers where measurable
- Avoid LINQ in hot paths (e.g., mutation processing loops) — use `for`/`foreach` with early exits

### Date Arithmetic Performance
- Avoid repeated `DateTime` parsing
- Years of service calculation: `(date2 - date1).TotalDays / 365.25` (fast, acceptable precision)
- Do not use heavy date libraries unless necessary

### Filtering and Lookups
- For `apply_indexation`: filtering policies by `scheme_id` or `employment_start_date` happens frequently
- Naive approach: scan all policies every time → slow with many policies
- Better: use `Dictionary<string, List<Policy>>` or similar indexing if measurable gains

### Async and CancellationToken
- Use `async`/`await` for I/O-bound operations (e.g., external Scheme Registry calls)
- Accept `CancellationToken` in service methods and pass it through
- Do NOT use async for CPU-bound calculations unless parallelizing

### Keep Controllers Thin
- Controllers should only:
  - Accept request
  - Call service
  - Return response
- All computation and business logic → services

### Parallelization (Advanced)
- Per-policy calculations in `calculate_retirement_benefit` can be parallelized if measurable gains
- External Scheme Registry calls (bonus): fetch multiple schemes concurrently

---

## OpenAPI / Swagger Conventions (Swashbuckle)

### XML Comments
- Enable XML documentation in `.csproj`:
  ```xml
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
  </PropertyGroup>
  ```
- Add XML comments to controllers and DTOs:
  ```csharp
  /// <summary>
  /// Processes a pension calculation request with ordered mutations.
  /// </summary>
  [HttpPost]
  public async Task<ActionResult<CalculationResponse>> ProcessCalculationRequest(...)
  ```

### Response Type Annotations
Use `[ProducesResponseType]` to document responses:
```csharp
[HttpPost]
[ProducesResponseType(typeof(CalculationResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public async Task<ActionResult<CalculationResponse>> ProcessCalculationRequest(...)
```

### Examples (Optional)
- Add example values using Swashbuckle annotations where helpful
- Do NOT bloat the spec with excessive examples
- Refer to `api-spec.yaml` as the authoritative source

### JSON Patch Documentation
If implementing JSON Patch bonus:
- Ensure Swagger correctly describes `JsonPatchDocument<T>` structure
- Add custom schema filters if needed for RFC 6902 compliance

---

## Testing Conventions (xUnit)

### Unit Tests
**What to test:**
- Individual mutation handlers (validation + application logic)
- Service methods
- Validators (FluentValidation rules)

**Example structure:**
```csharp
public class CreateDossierHandlerTests
{
    [Fact]
    public async Task CreateDossier_ValidInput_CreatesActiveDossier()
    {
        // Arrange
        var handler = new CreateDossierHandler();
        var mutation = new CreateDossierMutation { ... };

        // Act
        var result = await handler.ApplyAsync(mutation);

        // Assert
        Assert.Equal("ACTIVE", result.Dossier.Status);
    }

    [Fact]
    public async Task CreateDossier_DossierAlreadyExists_ReturnsCriticalError()
    {
        // Arrange
        var situation = new Situation { Dossier = new Dossier { ... } };
        var handler = new CreateDossierHandler();
        var mutation = new CreateDossierMutation { ... };

        // Act
        var messages = await handler.ValidateAsync(mutation, situation);

        // Assert
        Assert.Contains(messages, m => m.Code == "DOSSIER_ALREADY_EXISTS" && m.Severity == "CRITICAL");
    }
}
```

### Integration Tests (Lightweight)
Use `WebApplicationFactory` for testing the full endpoint:
```csharp
public class CalculationRequestTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CalculationRequestTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostCalculationRequest_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new CalculationRequest { ... };

        // Act
        var response = await _client.PostAsJsonAsync("/calculation-requests", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CalculationResponse>();
        Assert.Equal("SUCCESS", result.CalculationMetadata.CalculationOutcome);
    }
}
```

**Test Naming:**
- `MethodName_Scenario_ExpectedBehavior`
- Example: `ApplyIndexation_NoMatchingPolicies_ReturnsWarning`

**Arrangement Style:**
- Use Arrange-Act-Assert pattern
- Keep tests focused (one assertion per test when possible)

---

## Mutation Processing Architecture (Bonus: Clean Mutation Architecture)

If implementing the bonus "Clean Mutation Architecture" feature:

### Common Mutation Interface
Define a common interface/contract:
```csharp
public interface IMutationHandler
{
    string MutationDefinitionName { get; }
    Task<List<CalculationMessage>> ValidateAsync(Mutation mutation, Situation situation);
    Task<Situation> ApplyAsync(Mutation mutation, Situation situation);
}
```

### Individual Mutation Handlers
Each mutation (e.g., `create_dossier`, `add_policy`) implements the interface:
```csharp
public class CreateDossierHandler : IMutationHandler
{
    public string MutationDefinitionName => "create_dossier";

    public Task<List<CalculationMessage>> ValidateAsync(Mutation mutation, Situation situation)
    {
        // Business validation logic
    }

    public Task<Situation> ApplyAsync(Mutation mutation, Situation situation)
    {
        // Mutation application logic
    }
}
```

### Mutation Registry
Use dependency injection and a registry/dispatcher:
```csharp
public class MutationHandlerRegistry
{
    private readonly Dictionary<string, IMutationHandler> _handlers;

    public MutationHandlerRegistry(IEnumerable<IMutationHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.MutationDefinitionName);
    }

    public IMutationHandler GetHandler(string mutationDefinitionName)
    {
        if (!_handlers.TryGetValue(mutationDefinitionName, out var handler))
            throw new InvalidOperationException($"No handler for mutation: {mutationDefinitionName}");
        return handler;
    }
}
```

**Registration in `Program.cs`:**
```csharp
builder.Services.AddSingleton<IMutationHandler, CreateDossierHandler>();
builder.Services.AddSingleton<IMutationHandler, AddPolicyHandler>();
// ... other handlers
builder.Services.AddSingleton<MutationHandlerRegistry>();
```

**NO if/else or switch on mutation names:**
- The engine dispatches via the registry
- Adding a new mutation = implement interface + register in DI

---

## External Scheme Registry Integration (Bonus)

If the `SCHEME_REGISTRY_URL` environment variable is set, fetch accrual rates from an external service.

**Implementation:**
- Use `HttpClient` with `IHttpClientFactory`
- Endpoint: `GET {SCHEME_REGISTRY_URL}/schemes/{scheme_id}`
- Response: `{ "scheme_id": "SCHEME-001", "accrual_rate": 0.025 }`
- Timeout: 2 seconds (fallback to default `0.02` if unavailable)

**Performance Considerations:**
- **Caching:** Cache accrual rates by `scheme_id` (use `MemoryCache` or `ConcurrentDictionary`)
- **Parallel I/O:** Fetch multiple unique schemes concurrently (`Task.WhenAll`)
- **Connection Pooling:** Configure `HttpClient` for connection reuse

**Registration:**
```csharp
builder.Services.AddHttpClient<SchemeRegistryClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["SCHEME_REGISTRY_URL"] ?? "");
    client.Timeout = TimeSpan.FromSeconds(2);
});
```

**Fallback Logic:**
```csharp
public async Task<decimal> GetAccrualRateAsync(string schemeId)
{
    if (string.IsNullOrEmpty(_baseUrl))
        return 0.02m; // Default if SCHEME_REGISTRY_URL not set

    try
    {
        var response = await _httpClient.GetFromJsonAsync<SchemeResponse>($"/schemes/{schemeId}");
        return response?.AccrualRate ?? 0.02m;
    }
    catch (Exception)
    {
        return 0.02m; // Fallback on error/timeout
    }
}
```

---

## Data Model and Domain Objects

**Key entities (see `data-model.md` and `api-spec.yaml`):**

### Situation
Root state object containing the dossier.

### Dossier
- `dossier_id` (UUID)
- `status` ("ACTIVE" or "RETIRED")
- `retirement_date` (optional)
- `persons[]` (array of Person)
- `policies[]` (array of Policy)

### Person
- `person_id` (UUID)
- `role` ("PARTICIPANT")
- `name` (string)
- `birth_date` (date)

### Policy
- `policy_id` (string, format: `{dossier_id}-{sequence_number}`)
- `scheme_id` (string)
- `employment_start_date` (date)
- `salary` (decimal)
- `part_time_factor` (decimal, 0-1)
- `attainable_pension` (nullable decimal, set by `calculate_retirement_benefit`)
- `projections` (nullable array, bonus feature)

**Policy ID Generation:**
- First policy: `{dossier_id}-1`
- Second policy: `{dossier_id}-2`
- Sequence number is per dossier, increments with each `add_policy` mutation

---

## Mutation Definitions (Business Logic)

**Core Mutations:**
1. **`create_dossier`** (type: DOSSIER_CREATION)
   - Creates a new dossier with participant info
   - Sets status to "ACTIVE"
   - Validation: dossier must not already exist, name not empty, birth_date valid

2. **`add_policy`** (type: DOSSIER)
   - Adds a pension policy to an existing dossier
   - Generates `policy_id` as `{dossier_id}-{sequence}`
   - Validation: dossier must exist, salary >= 0, part_time_factor 0-1

3. **`apply_indexation`** (type: DOSSIER)
   - Applies percentage salary adjustment to matching policies
   - Filters by `scheme_id` and/or `effective_before` (optional)
   - Formula: `new_salary = salary * (1 + percentage)`
   - Validation: dossier exists, policies exist, no matching policies → WARNING

4. **`calculate_retirement_benefit`** (type: DOSSIER)
   - Calculates retirement benefits based on employment history
   - Formula (see README.md section "calculate_retirement_benefit"):
     - Years of service per policy
     - Effective salary per policy
     - Weighted average salary
     - Annual pension = weighted_avg × total_years × accrual_rate
     - Distribute proportionally by years
   - Validation: dossier exists, policies exist, eligibility (age >= 65 OR years >= 40)

**Bonus Mutation:**
5. **`project_future_benefits`** (type: DOSSIER, bonus)
   - Projects pension benefits at multiple future dates
   - Properties: `projection_start_date`, `projection_end_date`, `projection_interval_months`
   - Does NOT change dossier status

**See README.md for complete mutation specifications.**

---

## Important: Follow Existing Patterns

**Before writing new code:**
1. Check if similar code already exists in the repository
2. Follow the naming conventions, folder structure, and coding style of existing files
3. Do NOT introduce new patterns or frameworks unless absolutely necessary

**If the repository already defines:**
- A naming convention → use it
- A folder structure → follow it
- A validation approach → match it
- A service pattern → replicate it

**If existing code conflicts with these instructions:**
- Prefer the existing code patterns (they are tailored to this project)
- Only deviate if explicitly asked to refactor

---

## Docker Deployment

**`Dockerfile` requirements:**
- Must be in the repository root
- Application must listen on port 8080 (configurable via `PORT` environment variable)
- Use multi-stage build for efficiency (build + runtime)

**Example:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["PensionCalculationEngine.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PensionCalculationEngine.dll"]
```

---

## Build and Run (Local Development)

**Build:**
```bash
dotnet build
```

**Run:**
```bash
dotnet run
```

**Test:**
```bash
dotnet test
```

**Docker:**
```bash
docker build -t pension-engine .
docker run -p 8080:8080 pension-engine
```

**Access Swagger:**
```
http://localhost:8080/swagger
```

---

## When Unsure

**If you're unsure about:**
- How a mutation should behave → refer to `README.md` mutation details
- API contract → refer to `api-spec.yaml` (authoritative source)
- Data model → refer to `data-model.md`
- Existing patterns → search the codebase first

**ASK instead of guessing:**
- If requirements are ambiguous
- If performance trade-offs are unclear
- If a feature is out of scope

**Keep changes minimal:**
- Only implement what's explicitly requested
- Do not refactor unrelated code
- Do not add features "just in case"

---

## Key Reminders

✅ **DO:**
- Follow `api-spec.yaml` exactly (it's the contract)
- Prioritize correctness, then optimize for performance
- Use FluentValidation for business rules
- Keep controllers thin
- Return HTTP 200 for all processed calculations (SUCCESS or FAILURE)
- Use ProblemDetails for HTTP 400/500 errors

❌ **DO NOT:**
- Add logging (no ILogger, no Serilog, no log statements)
- Add a database or persistence
- Add authentication or authorization
- Use LINQ in hot paths without measuring impact
- Leak internal exception details to clients
- Contradict `api-spec.yaml` or `README.md`

---

**Focus:** Correct results first, then make it fast. Good luck!
