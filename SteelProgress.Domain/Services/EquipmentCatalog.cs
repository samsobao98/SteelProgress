using System.Globalization;
using System.Text;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Domain.Services
{
    /// <summary>
    /// Los tipos de material, su nombre y cómo deducirlo del texto libre que había antes
    /// («Máquina», «Mancuernas»…) o del nombre del ejercicio. Lo usan la migración de datos al arrancar,
    /// la biblioteca precargada y la importación de paquetes antiguos.
    /// </summary>
    public static class EquipmentCatalog
    {
        /// <summary>En el orden del formulario.</summary>
        public static readonly IReadOnlyList<EquipmentType> All = new[]
        {
            EquipmentType.Barbell, EquipmentType.Dumbbells, EquipmentType.StackMachine, EquipmentType.PlateMachine,
            EquipmentType.Cable, EquipmentType.Bodyweight, EquipmentType.Kettlebell, EquipmentType.Other
        };

        public static string Label(EquipmentType type) => type switch
        {
            EquipmentType.Barbell => "Barra",
            EquipmentType.Dumbbells => "Mancuernas",
            EquipmentType.StackMachine => "Máquina de placas",
            EquipmentType.PlateMachine => "Máquina de discos",
            EquipmentType.Cable => "Polea",
            EquipmentType.Bodyweight => "Peso corporal",
            EquipmentType.Kettlebell => "Kettlebell",
            _ => "Otro"
        };

        /// <summary>
        /// El material tal como se enseña: el nombre del tipo o, en «Otro», lo que se escribió («Gomas»).
        /// Vacío si no hay nada.
        /// </summary>
        public static string DisplayText(Exercise exercise)
        {
            var text = exercise.Equipment?.Trim() ?? "";
            return exercise.EquipmentType switch
            {
                null => text,
                EquipmentType.Other => text.Length > 0 ? text : Label(EquipmentType.Other),
                { } type => Label(type)
            };
        }

        /// <summary>Lo que se guarda en el texto libre: el nombre del tipo, o lo escrito en «Otro».</summary>
        public static string? StoredText(EquipmentType type, string? otherText) =>
            type == EquipmentType.Other
                ? (string.IsNullOrWhiteSpace(otherText) ? null : otherText.Trim())
                : Label(type);

        /// <summary>
        /// Deduce el tipo del texto libre del material y, si no basta, del nombre del ejercicio.
        /// Lo que no se reconoce queda en «Otro» (se puede cambiar en el formulario).
        /// </summary>
        public static EquipmentType Classify(string? equipment, string? name)
        {
            var n = Normalize(name);
            // Máquinas de cardio: no se cargan con peso
            if (Cardio.Any(n.Contains)) return EquipmentType.Other;

            var e = Normalize(equipment);
            if (e.Length > 0)
            {
                if (e.Contains("maquina") || e.Contains("multipower") || e.Contains("smith"))
                    return MachineKind(n, e);
                if (FromWords(e) is { } fromEquipment) return fromEquipment;
                if (e.Contains("banco") || e.Contains("cajon")) return EquipmentType.Bodyweight;
            }

            if (n.Contains("maquina") || n.Contains("prensa") || n.Contains("hack")) return MachineKind(n, e);
            return FromWords(n) ?? EquipmentType.Other;
        }

        private static readonly string[] Cardio =
            { "cinta de correr", "bicicleta", "eliptica", "escaladora", "assault", "remo (maquina)" };

        // Máquinas que se cargan con discos; el resto de máquinas, de placas
        private static readonly string[] PlateLoaded = { "prensa", "hack", "talones sentado", "discos" };

        private static EquipmentType MachineKind(string name, string equipment)
        {
            if (equipment.Contains("multipower") || equipment.Contains("smith") || name.Contains("multipower") || name.Contains("smith"))
                return EquipmentType.Barbell;
            if (equipment.Contains("placa")) return EquipmentType.StackMachine;
            return PlateLoaded.Any(k => name.Contains(k) || equipment.Contains(k))
                ? EquipmentType.PlateMachine
                : EquipmentType.StackMachine;
        }

        private static EquipmentType? FromWords(string t)
        {
            if (t.Length == 0) return null;
            if (t.Contains("mancuerna")) return EquipmentType.Dumbbells;
            if (t.Contains("kettlebell") || t.Contains("pesa rusa")) return EquipmentType.Kettlebell;
            if (t.Contains("polea") || t.Contains("cable") || t.Contains("cruce")) return EquipmentType.Cable;
            if (t.Contains("barra") && !t.Contains("dominada")) return EquipmentType.Barbell;
            if (t.Contains("peso corporal") || t.Contains("corporal") || t.Contains("dominada") || t.Contains("flexion"))
                return EquipmentType.Bodyweight;
            if (t.Contains("disco")) return EquipmentType.PlateMachine;
            return null;
        }

        /// <summary>Minúsculas y sin tildes, para comparar «Máquina» con «maquina».</summary>
        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
