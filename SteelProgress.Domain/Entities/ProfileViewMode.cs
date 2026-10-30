namespace SteelProgress.Domain.Entities;

// Cuánto detalle enseña la app, para los dos tipos de perfil (2026-10-30). Simple: entrar a entrenar
// y consultar. Detallada: crear y ajustar rutinas y ejercicios con todas las opciones.
// Detailed se llamaba Trainer: mismo valor guardado (1).
public enum ProfileViewMode
{
    Simple = 0,
    Detailed = 1
}

// Para qué es el perfil; se elige al crearlo. Personal: tus entrenos. Coach: tus clientes
// (Clientes, Inicio de clientes y sesiones con ellos); el entreno propio va en otro perfil.
public enum ProfileKind
{
    Personal = 0,
    Coach = 1
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
