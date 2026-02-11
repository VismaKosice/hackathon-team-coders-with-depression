# Pension Calculation Engine - Implementation

This is the implementation of the Pension Calculation Engine for the Visma Performance Hackathon.

## Project Structure

```
/
??? Controllers/                         # API controllers
?   ??? CalculationRequestsController.cs # Main calculation endpoint
??? Models/                              # Data models
?   ??? Domain/                          # Domain models
?   ?   ??? Dossier.cs
?   ?   ??? Person.cs
?   ?   ??? Policy.cs
?   ?   ??? Situation.cs
?   ??? Request/                         # Request DTOs
?   ?   ??? CalculationRequest.cs
?   ??? Response/                        # Response DTOs
?       ??? CalculationResponse.cs
??? Services/                            # Business logic
?   ??? CalculationService.cs           # Core calculation service
??? Dockerfile                           # Docker configuration
??? Program.cs                           # Application entry point
??? PensionCalculationEngine.csproj     # Project file
```

## API Endpoint

### POST /calculation-requests

Processes a pension calculation request with ordered mutations.

**Request Body:**
```json
{
  "tenant_id": "tenant-001",
  "calculation_instructions": {
    "mutations": [
      {
        "mutation_id": "uuid",
        "mutation_definition_name": "create_dossier",
        "mutation_type": "DOSSIER_CREATION",
        "actual_at": "2020-01-01",
        "mutation_properties": { ... }
      }
    ]
  }
}
```

**Response (HTTP 200):**
```json
{
  "calculation_metadata": {
    "calculation_id": "uuid",
    "tenant_id": "tenant-001",
    "calculation_started_at": "2024-01-01T00:00:00Z",
    "calculation_completed_at": "2024-01-01T00:00:01Z",
    "calculation_duration_ms": 100,
    "calculation_outcome": "SUCCESS"
  },
  "calculation_result": {
    "messages": [],
    "mutations": [],
    "initial_situation": { ... },
    "end_situation": { ... }
  }
}
```

## Building and Running

### Local Development

```bash
# Build the project
dotnet build

# Run the application
dotnet run

# Run tests (when implemented)
dotnet test
```

The application will start on port 8080 by default. Access Swagger UI at:
```
http://localhost:8080/swagger
```

### Docker

```bash
# Build Docker image
docker build -t pension-engine .

# Run container
docker run -p 8080:8080 pension-engine

# With environment variables
docker run -p 8080:8080 -e PORT=8080 pension-engine
```

## Current Status

? **Completed:**
- Project structure and setup
- Basic API endpoint (`POST /calculation-requests`)
- Request/Response models matching API specification
- Domain models (Dossier, Policy, Person, Situation)
- Swagger/OpenAPI documentation
- Docker configuration
- Basic calculation service scaffolding

?? **To Implement:**
- Mutation handlers:
  - `create_dossier`
  - `add_policy`
  - `apply_indexation`
  - `calculate_retirement_benefit`
- Business validation (FluentValidation)
- Error handling and calculation messages
- Unit tests
- Performance optimizations

?? **Bonus Features (Optional):**
- JSON Patch support (forward/backward)
- Clean Mutation Architecture
- External Scheme Registry integration
- `project_future_benefits` mutation
- Cold start optimization

## Development Guidelines

See `.github/copilot-instructions.md` for detailed development guidelines and conventions.

### Key Points:
- **Performance-critical:** Correctness first, then optimize
- **No logging:** Do not add ILogger calls unless explicitly requested
- **No database:** Stateless compute only
- **Thin controllers:** All logic in services
- **Use FluentValidation:** For business rules
- **Return HTTP 200:** For all processed calculations (SUCCESS or FAILURE)

## Technology Stack

- .NET 10.0
- ASP.NET Core (Controllers, not Minimal APIs)
- Swashbuckle (OpenAPI/Swagger)
- FluentValidation
- xUnit (for testing, when implemented)
