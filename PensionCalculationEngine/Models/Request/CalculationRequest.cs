using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PensionCalculationEngine.Models.Request;

public class CalculationRequest
{
    [Required]
    [StringLength(25)]
    [RegularExpression("[a-z0-9]+(?:_[a-z0-9]+)*")]
    [JsonPropertyName("tenant_id")]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("calculation_instructions")]
    public CalculationInstructions CalculationInstructions { get; set; } = new();
}

public class CalculationInstructions
{
    [Required]
    [MinLength(1)]
    [JsonPropertyName("mutations")]
    public List<CalculationMutation> Mutations { get; set; } = new();
}

public class CalculationMutation
{
    [Required]
    [JsonPropertyName("mutation_id")]
    public string MutationId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("mutation_definition_name")]
    public string MutationDefinitionName { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("mutation_type")]
    public string MutationType { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("actual_at")]
    public DateOnly ActualAt { get; set; }

    [JsonPropertyName("dossier_id")]
    public string? DossierId { get; set; }

    [Required]
    [JsonPropertyName("mutation_properties")]
    public Dictionary<string, object> MutationProperties { get; set; } = new();
}
