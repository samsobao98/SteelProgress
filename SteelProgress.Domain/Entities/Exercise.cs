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

        // Material (barra, mancuernas, polea...) e instrucciones de ejecución
        public string? Equipment { get; set; }
        public string? Instructions { get; set; }
    }
}
