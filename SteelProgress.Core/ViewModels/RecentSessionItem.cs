namespace SteelProgress.Core.ViewModels;

public class RecentSessionItem
{
    public string DayName { get; set; } = "";
    public DateTime Date { get; set; }
    public int ExerciseCount { get; set; }

    public string RelativeDate => (DateTime.Today - Date.Date).Days switch
    {
        0 => "Hoy",
        1 => "Ayer",
        var d when d < 7 => $"Hace {d} días",
        var d when d < 30 => $"Hace {d / 7} sem.",
        var d => $"Hace {d / 30} mes."
    };

    // Color-coded chips so every card doesn't look identical
    public string ChipColor => RelativeDate switch
    {
        "Hoy" => "#A6E3A1",
        "Ayer" => "#89B4FA",
        _ => "#313244"
    };

    public string ChipTextColor => RelativeDate switch
    {
        "Hoy" => "#1E1E2E",
        "Ayer" => "#1E1E2E",
        _ => "#BAC2DE"
    };
}
