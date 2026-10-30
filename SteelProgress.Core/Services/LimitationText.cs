using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.Services;

/// <summary>Zonas y textos de las limitaciones de un cliente («Rodilla derecha», «Pautas del fisio»…).</summary>
public static class LimitationText
{
    /// <summary>Las zonas que se pueden elegir, en el orden en que se muestran.</summary>
    public static readonly IReadOnlyList<string> Zones =
        ["Cuello", "Hombro", "Codo", "Muñeca", "Espalda alta", "Lumbar", "Cadera", "Rodilla", "Tobillo", "Otra"];

    // Para concordar el lado: «hombro derecho», «rodilla derecha»
    private static readonly HashSet<string> Masculine = ["Cuello", "Hombro", "Codo", "Tobillo"];

    public static readonly IReadOnlyList<(BodySide Side, string Label)> Sides =
        [(BodySide.Left, "Izquierda"), (BodySide.Right, "Derecha"), (BodySide.Both, "Ambos")];

    public static readonly IReadOnlyList<(GuidanceSource Source, string Label)> Sources =
        [(GuidanceSource.Own, "Mías"), (GuidanceSource.Physio, "Fisio"), (GuidanceSource.Doctor, "Médico"), (GuidanceSource.Other, "Otro")];

    /// <summary>«Rodilla derecha», «Hombro izquierdo», «Cadera, ambos lados», «Lumbar».</summary>
    public static string Title(ClientLimitation l)
    {
        var m = Masculine.Contains(l.Zone);
        return l.Side switch
        {
            BodySide.Left => $"{l.Zone} {(m ? "izquierdo" : "izquierda")}",
            BodySide.Right => $"{l.Zone} {(m ? "derecho" : "derecha")}",
            BodySide.Both => $"{l.Zone}, ambos lados",
            _ => l.Zone
        };
    }

    /// <summary>De quién son las pautas, en la ficha: «Pautas del fisio», «Apunte tuyo».</summary>
    public static string SourceText(GuidanceSource source) => source switch
    {
        GuidanceSource.Physio => "Pautas del fisio",
        GuidanceSource.Doctor => "Pautas del médico",
        GuidanceSource.Other => "Pautas de otro profesional",
        _ => "Apunte tuyo"
    };

    /// <summary>De quién son, en el aviso al entrenar: «fisio», «médico»; nada si son tuyas.</summary>
    public static string? ShortSource(GuidanceSource source) => source switch
    {
        GuidanceSource.Physio => "fisio",
        GuidanceSource.Doctor => "médico",
        GuidanceSource.Other => "otro profesional",
        _ => null
    };
}
