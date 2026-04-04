using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace SteelProgress.Domain.Entities;

public class WorkoutExercise
{
    public int Id { get; set; }

    public int WorkoutSessionId { get; set; }

    public int ExerciseId { get; set; }

    public WorkoutSession? WorkoutSession { get; set; }

    public Exercise? Exercise { get; set; }

    public ICollection<WorkoutSet> Sets { get; set; } = new List<WorkoutSet>();
}