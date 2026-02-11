using System.Text.Json.Serialization;

namespace PensionCalculationEngine.IntegrationTests.Models;

public class TestCase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("request")]
    public TestRequest Request { get; set; } = new();

    [JsonPropertyName("expected")]
    public TestExpected Expected { get; set; } = new();
}

public class TestRequest
{
    [JsonPropertyName("tenant_id")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("calculation_instructions")]
    public TestCalculationInstructions CalculationInstructions { get; set; } = new();
}

public class TestCalculationInstructions
{
    [JsonPropertyName("mutations")]
    public List<TestMutation> Mutations { get; set; } = new();
}

public class TestMutation
{
    [JsonPropertyName("mutation_id")]
    public string MutationId { get; set; } = string.Empty;

    [JsonPropertyName("mutation_definition_name")]
    public string MutationDefinitionName { get; set; } = string.Empty;

    [JsonPropertyName("mutation_type")]
    public string MutationType { get; set; } = string.Empty;

    [JsonPropertyName("actual_at")]
    public DateOnly ActualAt { get; set; }

    [JsonPropertyName("dossier_id")]
    public string? DossierId { get; set; }

    [JsonPropertyName("mutation_properties")]
    public Dictionary<string, object> MutationProperties { get; set; } = new();
}

public class TestExpected
{
    [JsonPropertyName("http_status")]
    public int HttpStatus { get; set; }

    [JsonPropertyName("calculation_outcome")]
    public string CalculationOutcome { get; set; } = string.Empty;

    [JsonPropertyName("message_count")]
    public int MessageCount { get; set; }

    [JsonPropertyName("messages")]
    public List<TestExpectedMessage> Messages { get; set; } = new();

    [JsonPropertyName("end_situation")]
    public object? EndSituation { get; set; }

    [JsonPropertyName("end_situation_mutation_id")]
    public string EndSituationMutationId { get; set; } = string.Empty;

    [JsonPropertyName("end_situation_mutation_index")]
    public int EndSituationMutationIndex { get; set; }

    [JsonPropertyName("end_situation_actual_at")]
    public DateOnly EndSituationActualAt { get; set; }

    [JsonPropertyName("mutations_processed_count")]
    public int MutationsProcessedCount { get; set; }
}

public class TestExpectedMessage
{
    [JsonPropertyName("level")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
}
