using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteelProgress.Domain.Entities;

public class WorkoutSession
{
    public int Id { get; set; }
    public Guid Uid { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public UserProfile? UserProfile { get; set; }

    // Sesión de un cliente (null = entreno propio)
    public int? ClientId { get; set; }
    public Client? Client { get; set; }

    // Registrada por el entrenador desde su móvil, con el cliente delante
    public bool IsInPerson { get; set; }

    public DateTime Date { get; set; }

    public int? DurationMinutes { get; set; }

    // "¿Cómo te has sentido?" de la pantalla de fin de entreno (1–5)
    public int? Feeling { get; set; }

    public string? Notes { get; set; }

    // Falso si se guardó con series de la rutina sin hacer
    public bool IsComplete { get; set; } = true;

    public int RoutineDayId { get; set; }
    public RoutineDay? RoutineDay { get; set; }

    public ICollection<WorkoutExercise> Exercises { get; set; } = new List<WorkoutExercise>();
}