using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteelProgress.Domain.Entities
{
    public class Exercise
    {
        public int Id { get; set; }
        // Identidad estable entre dispositivos (exportar/importar); el Id solo vale dentro de esta base de datos
        public Guid Uid { get; set; } = Guid.NewGuid();
        public int? UserId { get; set; }
        public UserProfile? UserProfile { get; set; }

        public string Name { get; set; } = string.Empty;
        public string MuscleGroup { get; set; } = string.Empty;
        public string? Notes { get; set; }

        // Campos de biblioteca (seed con UserId == null; también editables en propios)
        public string? SecondaryMuscles { get; set; }
        // Material (barra, mancuernas, polea...) e instrucciones de ejecución
        public string? Equipment { get; set; }
        public string? Instructions { get; set; }

        // Persistido (no computado): distingue la biblioteca precargada de datos
        // legacy con UserId null, que el seed de perfiles migra a un perfil.
        public bool IsSystem { get; set; }

        // Quién lo envió, si llegó en un paquete .steel (null: propio o de la biblioteca)
        public string? AuthorName { get; set; }
        // Etiquetas libres separadas por comas (vista entrenador), p. ej. «unilateral, casa»
        public string? Tags { get; set; }

        public ICollection<ExerciseMedia> Media { get; set; } = new List<ExerciseMedia>();
    }
}
