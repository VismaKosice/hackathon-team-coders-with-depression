namespace PensionCalculationEngine.Models.Domain;

public class Policy
{
    public string PolicyId { get; set; } = string.Empty;
    public string SchemeId { get; set; } = string.Empty;
    public DateOnly EmploymentStartDate { get; set; }
    public decimal Salary { get; set; }
    public decimal PartTimeFactor { get; set; }
    public decimal? AttainablePension { get; set; }
    public object? Projections { get; set; }
}
