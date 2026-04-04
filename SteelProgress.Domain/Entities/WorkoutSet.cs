using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace SteelProgress.Domain.Entities;

public class WorkoutSet
{
    public int Id { get; set; }

    public int WorkoutExerciseId { get; set; }

    public int Reps { get; set; }

    public double Weight { get; set; }

    public WorkoutExercise? WorkoutExercise { get; set; }
}