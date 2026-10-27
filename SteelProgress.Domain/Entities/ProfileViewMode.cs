namespace SteelProgress.Domain.Entities;

public enum ProfileViewMode
{
    Simple = 0,
    Trainer = 1
}

// Bloques que se pueden activar en la vista simple desde Ajustes.
// La vista simple nunca oculta datos: solo las opciones de edición de estos bloques.
[Flags]
public enum SimpleViewBlocks
{
    None = 0,
    Intensity = 1,        // RPE / RIR y % de 1RM
    TempoAndRest = 2,     // tempo y descanso entre series
    AdvancedStructure = 4, // superseries y series de calentamiento
    RepRanges = 8,        // repeticiones en rango
    DetailedNotes = 16,   // notas por serie
    All = Intensity | TempoAndRest | AdvancedStructure | RepRanges | DetailedNotes
}
