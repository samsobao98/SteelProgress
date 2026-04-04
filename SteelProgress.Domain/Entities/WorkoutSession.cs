using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteelProgress.Domain.Entities;

public class WorkoutSession
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    public int RoutineDayId { get; set; }

    public RoutineDay? RoutineDay { get; set; }

    public ICollection<WorkoutExercise> Exercises { get; set; } = new List<WorkoutExercise>();
}