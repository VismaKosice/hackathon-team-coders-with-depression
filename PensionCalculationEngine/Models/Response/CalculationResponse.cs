using System.Text.Json.Serialization;

namespace PensionCalculationEngine.Models.Response;

public class CalculationResponse
{
    [JsonPropertyName("calculation_metadata")]
    public CalculationMetadata CalculationMetadata { get; set; } = new();

    [JsonPropertyName("calculation_result")]
    public CalculationResult CalculationResult { get; set; } = new();
}

public class CalculationMetadata
{
    [JsonPropertyName("calculation_id")]
    public string CalculationId { get; set; } = string.Empty;

    [JsonPropertyName("tenant_id")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("calculation_started_at")]
    public DateTime CalculationStartedAt { get; set; }

    [JsonPropertyName("calculation_completed_at")]
    public DateTime CalculationCompletedAt { get; set; }

    [JsonPropertyName("calculation_duration_ms")]
    public long CalculationDurationMs { get; set; }

    [JsonPropertyName("calculation_outcome")]
    public string CalculationOutcome { get; set; } = "SUCCESS";
}

public class CalculationResult
{
    [JsonPropertyName("messages")]
    public List<CalculationMessage> Messages { get; set; } = new();

    [JsonPropertyName("mutations")]
    public List<ProcessedMutation> Mutations { get; set; } = new();

    [JsonPropertyName("end_situation")]
    public SituationSnapshot EndSituation { get; set; } = new();

    [JsonPropertyName("initial_situation")]
    public InitialSituation InitialSituation { get; set; } = new();
}

public class CalculationMessage
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class ProcessedMutation
{
    [JsonPropertyName("mutation")]
    public object Mutation { get; set; } = new();

    [JsonPropertyName("calculation_message_indexes")]
    public List<int>? CalculationMessageIndexes { get; set; }
}

public class SituationSnapshot
{
    [JsonPropertyName("mutation_id")]
    public string MutationId { get; set; } = string.Empty;

    [JsonPropertyName("mutation_index")]
    public int MutationIndex { get; set; }

    [JsonPropertyName("actual_at")]
    public DateOnly ActualAt { get; set; }

    [JsonPropertyName("situation")]
    public object Situation { get; set; } = new();
}

public class InitialSituation
{
    [JsonPropertyName("actual_at")]
    public DateOnly ActualAt { get; set; }

    [JsonPropertyName("situation")]
    public object Situation { get; set; } = new();
}
