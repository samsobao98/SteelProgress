namespace SteelProgress.Domain.Entities;

// Un pesaje: uno por día como mucho (el último del día sustituye al anterior). Del perfil o, en un
// perfil de entrenador, de un cliente; el de un cliente es un dato de salud y solo se guarda con su
// consentimiento (Client.HealthConsentAt).
public class BodyWeightEntry
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public UserProfile? UserProfile { get; set; }

    // Pesaje de un cliente (null = el del propio perfil)
    public int? ClientId { get; set; }
    public Client? Client { get; set; }

    public DateTime Date { get; set; }
    public double Kg { get; set; }
}
