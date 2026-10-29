namespace SteelProgress.Domain.Entities
{
    // Con qué se hace un ejercicio (obligatorio). Decide su icono y, más adelante, cómo se apunta
    // el peso: discos por lado (barra, máquina de discos), kilos de cada mancuerna o placa (polea, máquina de placas).
    // Se guarda como número: no cambiar los valores.
    public enum EquipmentType
    {
        Barbell = 1,
        Dumbbells = 2,
        StackMachine = 3,
        PlateMachine = 4,
        Cable = 5,
        Bodyweight = 6,
        Kettlebell = 7,
        Other = 8
    }
}
