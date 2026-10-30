namespace SteelProgress.Domain.Entities;

// Una lesión, molestia o pauta de un profesional sanitario de un cliente. La app solo la apunta y la
// recuerda al entrenar con él: nunca propone ejercicios ni cambios (docs/INVESTIGACION_DATOS_PERFIL.md).
// Son datos de salud: solo se guardan con el consentimiento del cliente (Client.HealthConsentAt).
public class ClientLimitation
{
    public int Id { get; set; }

    public int ClientId { get; set; }
    public Client? Client { get; set; }

    /// <summary>Zona del cuerpo, de la lista fija de LimitationZones (Core).</summary>
    public string Zone { get; set; } = string.Empty;
    public BodySide Side { get; set; }
    /// <summary>Qué tener en cuenta, con las palabras del entrenador.</summary>
    public string Text { get; set; } = string.Empty;
    public GuidanceSource Source { get; set; }
    public DateTime Since { get; set; } = DateTime.Today;
    /// <summary>Cuándo dejó de aplicar; null mientras sigue.</summary>
    public DateTime? ResolvedAt { get; set; }
}

public enum BodySide
{
    None = 0,
    Left = 1,
    Right = 2,
    Both = 3
}

// De quién son las pautas
public enum GuidanceSource
{
    Own = 0,
    Physio = 1,
    Doctor = 2,
    Other = 3
}
