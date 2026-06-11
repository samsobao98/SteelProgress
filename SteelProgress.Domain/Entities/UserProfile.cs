namespace SteelProgress.Domain.Entities;

public class UserProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#89B4FA";
    public string? Description { get; set; }

    public ICollection<Exercise> Exercises { get; set; } = new List<Exercise>();
    public ICollection<Routine> Routines { get; set; } = new List<Routine>();
    public ICollection<WorkoutSession> WorkoutSessions { get; set; } = new List<WorkoutSession>();
}
