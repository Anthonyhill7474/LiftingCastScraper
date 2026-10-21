namespace LiftingCastTracker.Services.Models;

public class Competition
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Date { get; set; }
    public string? DateFormat { get; set; }
    public DateTime? CreateDate { get; set; }
    public bool ShowOnHomePage { get; set; }
}
