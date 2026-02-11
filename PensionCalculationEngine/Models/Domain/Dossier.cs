namespace PensionCalculationEngine.Models.Domain;

public class Dossier
{
    public string DossierId { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public DateOnly? RetirementDate { get; set; }
    public List<Person> Persons { get; set; } = new();
    public List<Policy> Policies { get; set; } = new();
}
