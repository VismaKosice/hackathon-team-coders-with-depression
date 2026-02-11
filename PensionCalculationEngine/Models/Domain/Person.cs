namespace PensionCalculationEngine.Models.Domain;

public class Person
{
    public string PersonId { get; set; } = string.Empty;
    public string Role { get; set; } = "PARTICIPANT";
    public string Name { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
}
