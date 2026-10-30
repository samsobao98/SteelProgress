using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;

namespace SteelProgress.Core.ViewModels;

public enum WeightAccess
{
    /// <summary>Perfil de entrenador sin cliente activo: no hay de quién apuntarlo.</summary>
    Hidden,
    Allowed,
    /// <summary>Cliente sin consentimiento para datos de salud.</summary>
    NeedsConsent
}

/// <summary>Una fila de «Media de cada semana».</summary>
public sealed record WeekAverageItem(string Week, string Kg, string Count);

/// <summary>Una fila de «Tu fuerza respecto a tu peso».</summary>
public sealed record StrengthItem(string Exercise, string Ratio, string Before)
{
    public bool HasBefore => Before.Length > 0;
}

/// <summary>
/// Peso corporal (maqueta del 2026-10-30): la tarjeta de Progreso y su pantalla, con la tendencia, las
/// medias semanales y la fuerza respecto al propio peso; y la hoja para apuntar el de hoy.
/// Del perfil o del cliente activo. El de un cliente necesita su consentimiento para datos de salud.
/// </summary>
public partial class BodyWeightViewModel : ObservableObject
{
    public const double MinKg = 20, MaxKg = 400;
    private const int WeeksShown = 6, StrengthShown = 5;
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    private readonly BodyWeightRepository _repository;
    private readonly WorkoutRepository? _workouts;
    private readonly ClientContextService? _clientContext;
    private readonly ViewOptionsService? _viewOptions;
    private readonly TimeProvider _clock;
    private List<WeightPoint> _entries = new();
    private List<WeightPoint> _trend = new();

    public BodyWeightViewModel(BodyWeightRepository repository, WorkoutRepository? workouts = null,
        ClientContextService? clientContext = null, ViewOptionsService? viewOptions = null, TimeProvider? clock = null)
    {
        _repository = repository;
        _workouts = workouts;
        _clientContext = clientContext;
        _viewOptions = viewOptions;
        _clock = clock ?? TimeProvider.System;
    }

    private DateTime Today => _clock.GetLocalNow().Date;

    // ── Quién y si se puede ──

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAllowed), nameof(NeedsConsent), nameof(IsVisible))]
    private WeightAccess _access;

    public bool IsAllowed => Access == WeightAccess.Allowed;
    public bool NeedsConsent => Access == WeightAccess.NeedsConsent;
    public bool IsVisible => Access != WeightAccess.Hidden;

    public int? ClientId => _clientContext?.ActiveClient?.Id;
    [ObservableProperty] private string _title = "Peso corporal";
    [ObservableProperty] private string _consentText = "";
    [ObservableProperty] private string _weighTip = "";

    // ── Lo que se enseña ──

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasEntries;

    public bool IsEmpty => IsAllowed && !HasEntries;

    [ObservableProperty] private string _trendText = "";
    [ObservableProperty] private string _changeText = "";
    [ObservableProperty] private string _lastText = "";

    public ObservableCollection<WeekAverageItem> Weeks { get; } = new();
    public ObservableCollection<StrengthItem> Strength { get; } = new();
    public bool HasStrength => Strength.Count > 0;

    /// <summary>0 = 1 mes, 1 = 3 meses, 2 = todo.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMonth), nameof(IsQuarter), nameof(IsAll))]
    private int _range;

    public bool IsMonth => Range == 0;
    public bool IsQuarter => Range == 1;
    public bool IsAll => Range == 2;

    [RelayCommand] private void ShowRange(string range) => Range = int.Parse(range, CultureInfo.InvariantCulture);
    partial void OnRangeChanged(int value) => BuildChart();

    [ObservableProperty] private ISeries[] _series = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _xAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes = Array.Empty<Axis>();
    [ObservableProperty] private ISeries[] _sparkline = Array.Empty<ISeries>();

    /// <summary>La línea de la tarjeta va sin ejes ni etiquetas.</summary>
    public Axis[] SparkXAxes { get; } = { new Axis { IsVisible = false } };
    public Axis[] SparkYAxes { get; } = { new Axis { IsVisible = false } };

    public async Task LoadAsync()
    {
        var client = _clientContext?.ActiveClient;
        if (client is not null)
        {
            Title = $"Peso de {client.Name}";
            WeighTip = "Que se pese siempre igual, por ejemplo al levantarse. El peso de un día sube y baja por agua y comida: lo que cuenta es la tendencia.";
            ConsentText = $"El peso de un cliente es un dato de salud. Para apuntarlo necesitas el consentimiento de {client.Name}: el mismo que para sus limitaciones, se da una sola vez.";
            Access = await _repository.HasHealthConsentAsync(client.Id) ? WeightAccess.Allowed : WeightAccess.NeedsConsent;
        }
        else
        {
            Title = "Peso corporal";
            WeighTip = "Pésate siempre igual, por ejemplo al levantarte. El peso de un día sube y baja por agua y comida: lo que cuenta es la tendencia.";
            Access = _viewOptions?.Current.IsCoach == true ? WeightAccess.Hidden : WeightAccess.Allowed;
        }

        if (!IsAllowed)
        {
            _entries = new();
            _trend = new();
            HasEntries = false;
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }

        _entries = (await _repository.GetAllAsync()).Select(w => new WeightPoint(w.Date, w.Kg)).ToList();
        _trend = BodyWeightStats.Trend(_entries);
        HasEntries = _entries.Count > 0;
        OnPropertyChanged(nameof(IsEmpty));

        var today = Today;
        if (HasEntries)
        {
            TrendText = BodyWeightStats.Kg(_trend[^1].Kg);
            ChangeText = BodyWeightStats.ChangeText(_trend, today);
            var last = _entries[^1];
            LastText = $"Último: {BodyWeightStats.Kg(last.Kg)} kg · {RoutineSchedule.AgoText(last.Date, today)}";
        }

        Weeks.Clear();
        foreach (var w in BodyWeightStats.WeeklyAverages(_entries, WeeksShown))
            Weeks.Add(new WeekAverageItem($"Semana del {w.WeekStart:d/MM}", $"{BodyWeightStats.Kg(w.Kg)} kg",
                w.Count == 1 ? "1 pesaje" : $"{w.Count} pesajes"));

        Strength.Clear();
        if (_workouts is not null && HasEntries)
        {
            var log = await _workouts.GetTrainingLogAsync();
            foreach (var s in BodyWeightStats.Strength(TrainingStats.Exercises(log.Sets), _trend, today, StrengthShown))
                Strength.Add(new StrengthItem(s.Exercise, BodyWeightStats.Ratio(s.Ratio),
                    s.PreviousRatio is double before ? $"el {s.PreviousDate:d/MM} {BodyWeightStats.Ratio(before)}" : ""));
        }
        OnPropertyChanged(nameof(HasStrength));

        BuildChart();
    }

    // ── Gráficas ──

    private void BuildChart()
    {
        var steel = SKColor.Parse("#8A91A8");
        var lilac = SKColor.Parse("#C9B9F2");

        // Tarjeta de Progreso: solo la línea de tendencia de las últimas 4 semanas
        var recent = _trend.Where(p => p.Date >= Today.AddDays(-BodyWeightStats.ChangeDays)).ToList();
        Sparkline = recent.Count < 2 ? Array.Empty<ISeries>() : new ISeries[]
        {
            new LineSeries<ObservablePoint>
            {
                Values = recent.Select(p => new ObservablePoint(p.Date.Ticks, Math.Round(p.Kg, 2))).ToList(),
                Stroke = new SolidColorPaint(lilac) { StrokeThickness = 3 },
                GeometrySize = 0, GeometryStroke = null, GeometryFill = null, Fill = null, LineSmoothness = 0.6
            }
        };

        if (_entries.Count == 0) { Series = Array.Empty<ISeries>(); return; }

        var from = Range switch
        {
            0 => Today.AddDays(-30),
            1 => Today.AddDays(-91),
            _ => _entries[0].Date.Date
        };
        // Eje fijo al periodo (al menos una semana), para que con pocos pesajes las fechas tengan sentido
        if (from > Today.AddDays(-7)) from = Today.AddDays(-7);
        var dots = _entries.Where(e => e.Date >= from).ToList();
        var line = _trend.Where(p => p.Date >= from).ToList();
        Series = new ISeries[]
        {
            new ScatterSeries<ObservablePoint>
            {
                Name = "Cada pesaje",
                Values = dots.Select(p => new ObservablePoint(p.Date.Ticks, p.Kg)).ToList(),
                Fill = new SolidColorPaint(steel.WithAlpha(180)), Stroke = null, GeometrySize = 6
            },
            new LineSeries<ObservablePoint>
            {
                Name = "Tendencia",
                Values = line.Select(p => new ObservablePoint(p.Date.Ticks, Math.Round(p.Kg, 2))).ToList(),
                Stroke = new SolidColorPaint(lilac) { StrokeThickness = 3 },
                GeometrySize = 0, GeometryStroke = null, GeometryFill = null, Fill = null, LineSmoothness = 0
            }
        };
        var labels = new SolidColorPaint(steel) { SKTypeface = SKTypeface.FromFamilyName("sans-serif") };
        XAxes = new[] { new Axis { Labeler = v => new DateTime((long)Math.Max(0, v)).ToString("d/MM"), LabelsPaint = labels, TextSize = 10, SeparatorsPaint = null,
            MinLimit = from.Ticks, MaxLimit = Today.AddHours(12).Ticks } };
        YAxes = new[] { new Axis { Labeler = v => v.ToString("0.#", Es), LabelsPaint = labels, TextSize = 10,
            SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#2A2E3C")) { StrokeThickness = 1 } } };
    }

    // ── Apuntar ──

    [ObservableProperty] private bool _isEntering;
    [ObservableProperty] private string _entryText = "";
    [ObservableProperty] private string _entryHint = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntryError))]
    private string _entryError = "";

    public bool HasEntryError => EntryError.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntryOnDate))]
    private DateTime _entryDate = DateTime.Today;

    public bool HasEntryOnDate => _entries.Any(e => e.Date.Date == EntryDate.Date);

    [RelayCommand]
    private void StartEntry()
    {
        if (!IsAllowed) return;
        EntryDate = Today;
        var last = _entries.LastOrDefault();
        EntryText = last is null ? "" : BodyWeightStats.Kg(last.Kg);
        EntryHint = last is null
            ? "Escribe el peso en kilos."
            : $"El último fue {BodyWeightStats.Kg(last.Kg)} kg, {RoutineSchedule.AgoText(last.Date, Today)}. Ajústalo con − y + o escríbelo.";
        EntryError = "";
        OnPropertyChanged(nameof(HasEntryOnDate));
        IsEntering = true;
    }

    [RelayCommand] private void CancelEntry() => IsEntering = false;
    [RelayCommand] private void EntryMinus() => Nudge(-0.1);
    [RelayCommand] private void EntryPlus() => Nudge(0.1);

    private void Nudge(double kg)
    {
        if (ParseKg(EntryText) is not double current) return;
        EntryText = BodyWeightStats.Kg(Math.Clamp(Math.Round(current + kg, 1), MinKg, MaxKg));
    }

    /// <summary>Acepta «78,9» y «78.9».</summary>
    public static double? ParseKg(string? text) =>
        double.TryParse(text?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var kg) ? kg : null;

    [RelayCommand]
    private async Task SaveEntryAsync()
    {
        if (ParseKg(EntryText) is not double kg || kg < MinKg || kg > MaxKg)
        {
            EntryError = $"Escribe un peso entre {MinKg:0} y {MaxKg:0} kg.";
            return;
        }
        if (EntryDate.Date > Today)
        {
            EntryError = "No se puede apuntar un día que aún no ha llegado.";
            return;
        }
        await _repository.SaveAsync(EntryDate, Math.Round(kg, 1));
        IsEntering = false;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteEntryAsync()
    {
        var confirmed = await ConfirmDialogService.ConfirmAsync("Borrar pesaje",
            $"Se borrará el peso del {EntryDate:dd/MM/yyyy}.", accept: "Borrar", destructive: true);
        if (!confirmed) return;
        await _repository.DeleteAsync(EntryDate);
        IsEntering = false;
        await LoadAsync();
    }
}
