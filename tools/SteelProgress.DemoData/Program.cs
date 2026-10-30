using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

// Rellena una base de datos de SteelProgress con dos perfiles de prueba, sustituyendo los que tenga:
//  · Lucía, personal (vista detallada): tres rutinas, unos cuatro meses de entrenos y su peso.
//  · Álex, entrenador (vista detallada): ocho clientes en situaciones distintas (toca hoy, sin
//    noticias, sin rutina, PAR-Q caducado, limitaciones, peso, con y sin consentimiento, entrenos
//    que le han enviado) y uno archivado. Más de los que caben en Inicio, para ver el límite.
//  Lucía es también clienta de Álex: su «Torso / Pierna» se la pasó él, así que al terminar un
//  entreno le sale «Enviar a Álex» (y Álex reconoce lo que le llega).
// Las fechas van en relación con hoy, así que sirve cualquier día. Uso: tools/demo-data.sh
// (o: dotnet run --project tools/SteelProgress.DemoData -- ruta/a/steelprogress.db)

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Uso: SteelProgress.DemoData <ruta a steelprogress.db>");
    return 1;
}

var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={args[0]}").Options;
// Sin perfil activo los filtros de AppDbContext no se pueden evaluar: todas las consultas los ignoran
await using var db = new AppDbContext(options);
await db.Database.MigrateAsync();

var library = await db.Exercises.IgnoreQueryFilters().Where(e => e.IsSystem).ToDictionaryAsync(e => e.Name);
if (library.Count == 0)
{
    Console.Error.WriteLine("La base de datos no tiene la biblioteca de ejercicios: abre la app una vez en el emulador y vuelve a probar.");
    return 1;
}

// Fuera todos los perfiles (en cascada: rutinas, sesiones, clientes, pesajes…) y sus ejercicios propios
db.UserProfiles.RemoveRange(await db.UserProfiles.ToListAsync());
db.Exercises.RemoveRange(await db.Exercises.IgnoreQueryFilters().Where(e => !e.IsSystem).ToListAsync());
await db.SaveChangesAsync();

var today = DateTime.Today;
var rng = new Random(42);
var seeder = new Seeder(db, library, rng, today);

await seeder.SeedLuciaAsync();
await seeder.SeedAlexAsync();

Console.WriteLine($"Listo: {await db.UserProfiles.CountAsync()} perfiles, {await db.Clients.IgnoreQueryFilters().CountAsync()} clientes, " +
                  $"{await db.Routines.IgnoreQueryFilters().CountAsync()} rutinas, {await db.WorkoutSessions.IgnoreQueryFilters().CountAsync()} sesiones, {await db.BodyWeights.IgnoreQueryFilters().CountAsync()} pesajes.");
return 0;

/// <summary>Un ejercicio de un día de rutina: series, rango de repeticiones, descanso y peso de partida.</summary>
record Plan(string Exercise, int Sets, int RepsMin, int? RepsMax, int RestSeconds, double StartKg, double? Rpe = null, int Warmups = 0);

sealed class Seeder(AppDbContext db, Dictionary<string, Exercise> library, Random rng, DateTime today)
{
    private static readonly string[] SessionNotes =
    [
        "Buena sesión, todo se movió rápido.", "Algo cansado del trabajo.", "Subí peso en el principal.",
        "Dormí poco; bajé un poco las cargas.", "Muy buenas sensaciones."
    ];

    // ── Lucía: perfil personal ─────────────────────────────────────────

    public async Task SeedLuciaAsync()
    {
        var lucia = new UserProfile
        {
            Name = "Lucía", AvatarColor = "#CBA6F7", Description = "Constancia antes que intensidad",
            Kind = ProfileKind.Personal, ViewMode = ProfileViewMode.Detailed
        };
        db.UserProfiles.Add(lucia);
        await db.SaveChangesAsync();

        var torsoPierna = _luciaRoutine = AddRoutine(lucia.Id, null, "Torso / Pierna", active: true,
            ("Torso A", [
                new("Press banca", 4, 5, 8, 180, 45, 8, Warmups: 2),
                new("Remo con barra", 4, 6, 10, 150, 42.5),
                new("Press de hombros con mancuernas", 3, 8, 12, 120, 12),
                new("Jalón al pecho", 3, 8, 12, 90, 40),
                new("Elevaciones laterales", 3, 12, 15, 60, 6),
                new("Curl con mancuernas", 3, 10, 12, 60, 9)]),
            ("Pierna A", [
                new("Sentadilla", 4, 5, 6, 180, 70, 8, Warmups: 2),
                new("Peso muerto rumano", 3, 8, 10, 150, 60),
                new("Prensa de piernas", 3, 10, 12, 120, 120),
                new("Curl femoral tumbado", 3, 10, 12, 90, 30),
                new("Elevación de talones de pie", 4, 12, 15, 60, 50)]),
            ("Torso B", [
                new("Press inclinado con mancuernas", 4, 8, 10, 150, 16),
                new("Dominadas", 4, 6, 8, 150, 0),
                new("Press militar", 3, 6, 8, 150, 32.5, 8),
                new("Remo en polea baja", 3, 10, 12, 90, 40),
                new("Face pull", 3, 15, null, 60, 15),
                new("Extensiones con cuerda en polea", 3, 12, null, 60, 15)]),
            ("Pierna B", [
                new("Peso muerto", 3, 3, 5, 240, 90, 8, Warmups: 3),
                new("Sentadilla frontal", 3, 6, 8, 180, 50),
                new("Hip thrust", 3, 8, 10, 120, 80),
                new("Zancadas búlgaras", 3, 10, null, 90, 12),
                new("Elevaciones de piernas colgado", 3, 12, null, 60, 0)]));

        var fullBody = AddRoutine(lucia.Id, null, "Full body 3 días", active: false,
            ("Día A", [new("Sentadilla", 3, 5, null, 180, 60), new("Press banca", 3, 5, null, 150, 40), new("Remo con barra", 3, 8, null, 120, 37.5)]),
            ("Día B", [new("Peso muerto", 1, 5, null, 240, 80), new("Press militar", 3, 5, null, 150, 27.5), new("Dominadas", 3, 6, 8, 120, 0)]),
            ("Día C", [new("Sentadilla frontal", 3, 6, null, 150, 42.5), new("Press inclinado con mancuernas", 3, 8, null, 120, 14), new("Remo con mancuerna a una mano", 3, 10, null, 90, 16)]));

        AddRoutine(lucia.Id, null, "Fuerza 5×5", active: false,
            ("A", [new("Sentadilla", 5, 5, null, 180, 60), new("Press banca", 5, 5, null, 180, 40), new("Remo con barra", 5, 5, null, 150, 40)]),
            ("B", [new("Sentadilla", 5, 5, null, 180, 60), new("Press militar", 5, 5, null, 180, 27.5), new("Peso muerto", 1, 5, null, 240, 80)]));
        await db.SaveChangesAsync();

        // Full body de hace 16 a 9 semanas (lunes, miércoles, viernes)…
        var start = Monday(today).AddDays(-7 * 16);
        AddSessions(lucia.Id, null, fullBody, Dates(start, Monday(today).AddDays(-7 * 9), [0, 2, 4]), hour: 19, progress: 0.006);
        // …y Torso / Pierna las últimas 8 semanas (lunes, martes, jueves, viernes), con una semana floja
        var dates = Dates(Monday(today).AddDays(-7 * 8), today.AddDays(-1), [0, 1, 3, 4])
            .Where(d => d < Monday(today).AddDays(-7 * 4) || d >= Monday(today).AddDays(-7 * 3) || d.DayOfWeek == DayOfWeek.Monday)
            .ToList();
        AddSessions(lucia.Id, null, torsoPierna, dates, hour: 19, progress: 0.005);

        AddWeights(lucia.Id, null, today.AddDays(-7 * 12), 63.4, 62.3, everyDays: 1, skipEvery: 4);
        await db.SaveChangesAsync();
    }

    // ── Álex: entrenador con cuatro clientes ───────────────────────────

    public async Task SeedAlexAsync()
    {
        var alex = new UserProfile
        {
            Name = "Álex", AvatarColor = "#89B4FA", Description = "Entrenador personal",
            Kind = ProfileKind.Coach, ViewMode = ProfileViewMode.Detailed
        };
        db.UserProfiles.Add(alex);
        await db.SaveChangesAsync();

        const int mon = 1, tue = 2, wed = 4, thu = 8, fri = 16;

        // Felipe: lunes, miércoles y viernes; limitaciones; PAR-Q caducado; peso. Hoy le toca si es su día
        var felipe = AddClient(alex.Id, "Felipe", "#94E2D5", "Recomposición", "Prefiere entrenar a última hora de la tarde.",
            mon | wed | fri, consentDaysAgo: 60, parq: today.AddMonths(-13));
        felipe.Limitations.Add(new ClientLimitation
        {
            Zone = "Rodilla", Side = BodySide.Right, Source = GuidanceSource.Physio, Since = today.AddDays(-50),
            Text = "Operado de LCA en 2025. Sin saltos ni sentadilla por debajo de 90°."
        });
        felipe.Limitations.Add(new ClientLimitation
        {
            Zone = "Lumbar", Side = BodySide.None, Source = GuidanceSource.Own, Since = today.AddDays(-20),
            Text = "Molestias al levantar peso desde el suelo. Vigilar la técnica."
        });
        felipe.Limitations.Add(new ClientLimitation
        {
            Zone = "Hombro", Side = BodySide.Left, Source = GuidanceSource.Own, Since = today.AddDays(-70),
            ResolvedAt = today.AddDays(-30), Text = "Molestia en press por encima de la cabeza."
        });

        // Marta: martes y jueves, pero lleva más de una semana sin venir (sin noticias)
        var marta = AddClient(alex.Id, "Marta", "#F9E2AF", "Fuerza", null, tue | thu, consentDaysAgo: 40, parq: today.AddMonths(-4));
        // Jorge: sin días fijos, sin consentimiento ni PAR-Q
        var jorge = AddClient(alex.Id, "Jorge", "#FAB387", "Hipertrofia", "Viene a primera hora.", 0, consentDaysAgo: null, parq: null);
        // Ana: recién dada de alta, sin rutina
        var ana = AddClient(alex.Id, "Ana", "#F38BA8", "Empezar a entrenar", "Nunca ha entrenado con pesas.", 0, consentDaysAgo: null, parq: today.AddDays(-1));
        ana.CreatedAt = today.AddDays(-1);
        // Lucía: la rutina que tiene en su perfil se la pasó Álex (sin sesiones aquí: ella se las envía)
        var luciaClient = AddClient(alex.Id, "Lucía", "#CBA6F7", "Fuerza e hipertrofia", null, mon | tue | thu | fri, consentDaysAgo: null, parq: today.AddMonths(-2));
        await db.SaveChangesAsync();

        var felipeA = AddRoutine(alex.Id, felipe.Id, "Rutina A", active: true,
            ("Empuje", [new("Press banca", 4, 6, 8, 150, 70, 8, Warmups: 2), new("Press inclinado con mancuernas", 3, 8, 10, 120, 24),
                new("Elevaciones laterales", 3, 12, 15, 60, 10), new("Extensiones con cuerda en polea", 3, 10, 12, 60, 25)]),
            ("Tirón", [new("Dominadas", 4, 6, 8, 150, 0), new("Remo con barra", 4, 8, 10, 120, 65),
                new("Face pull", 3, 15, null, 60, 20), new("Curl martillo", 3, 10, 12, 60, 14)]),
            ("Pierna", [new("Prensa de piernas", 4, 8, 10, 150, 160), new("Hip thrust", 3, 8, 10, 120, 100),
                new("Curl femoral sentado", 3, 10, 12, 90, 45), new("Elevación de talones sentado", 3, 12, 15, 60, 40)]));
        AddRoutine(alex.Id, felipe.Id, "Rutina B (2 días)", active: false,
            ("Torso", [new("Press banca", 3, 8, 10, 120, 65), new("Remo en polea baja", 3, 10, 12, 90, 55)]),
            ("Pierna", [new("Prensa de piernas", 3, 10, 12, 120, 150), new("Curl femoral tumbado", 3, 10, 12, 90, 35)]));

        var martaRoutine = AddRoutine(alex.Id, marta.Id, "Fuerza 2 días", active: true,
            ("Día 1", [new("Sentadilla", 5, 3, 5, 210, 75, 8, Warmups: 3), new("Press banca", 5, 3, 5, 180, 50, 8, Warmups: 2), new("Remo con barra", 4, 6, null, 120, 45)]),
            ("Día 2", [new("Peso muerto", 4, 3, null, 240, 100, 8, Warmups: 3), new("Press militar", 4, 5, null, 150, 32.5), new("Dominadas", 4, 5, null, 120, 0)]));

        var jorgeRoutine = AddRoutine(alex.Id, jorge.Id, "Hipertrofia PPL", active: true,
            ("Empuje", [new("Press banca", 4, 8, 10, 120, 80), new("Press de hombros en máquina", 3, 10, 12, 90, 50),
                new("Fondos en paralelas (pecho)", 3, 8, 12, 90, 0), new("Elevaciones laterales en polea", 3, 15, 20, 45, 7.5)]),
            ("Tirón", [new("Jalón al pecho", 4, 10, 12, 90, 70), new("Remo en máquina", 4, 10, 12, 90, 65),
                new("Curl con barra", 3, 10, 12, 60, 35), new("Pájaros (elevaciones posteriores)", 3, 15, null, 45, 8)]),
            ("Pierna", [new("Sentadilla hack", 4, 8, 10, 150, 120), new("Peso muerto rumano", 3, 10, null, 120, 90),
                new("Extensiones de cuádriceps", 3, 12, 15, 60, 55), new("Curl femoral sentado", 3, 12, 15, 60, 50)]));
        await db.SaveChangesAsync();

        // La copia de Álex de la rutina de Lucía, y la de Lucía apuntando a ella (como al importarla)
        var luciaPlans = _plans[_luciaRoutine!];
        var coachCopy = AddRoutine(alex.Id, luciaClient.Id, "Torso / Pierna", active: true,
            _luciaRoutine!.Days.OrderBy(d => d.Order).Select((d, i) => (d.Name, luciaPlans[i])).ToArray());
        await db.SaveChangesAsync();
        _luciaRoutine.SourceUid = coachCopy.Uid;
        _luciaRoutine.AuthorName = "Álex";

        // Felipe: L/X/V las últimas 7 semanas, hasta hace dos días como mucho
        AddSessions(alex.Id, felipe.Id, felipeA, Dates(Monday(today).AddDays(-49), today.AddDays(-2), [0, 2, 4]), hour: 20, progress: 0.004, inPerson: true);
        // Marta: M/J de hace 8 a hace ~1,5 semanas
        AddSessions(alex.Id, marta.Id, martaRoutine, Dates(Monday(today).AddDays(-56), today.AddDays(-10), [1, 3]), hour: 18, progress: 0.006, inPerson: true);
        // Jorge: tres días por semana, el último hace 3 días
        AddSessions(alex.Id, jorge.Id, jorgeRoutine, Dates(Monday(today).AddDays(-35), today.AddDays(-3), [0, 2, 4]), hour: 8, progress: 0.005, inPerson: true);

        // Tres clientes más (Inicio enseña solo cinco) y uno archivado con su historial
        (string Name, string Color, string Goal, int DaysAgo)[] more =
            [("Raúl", "#89DCEB", "Perder grasa", 1), ("Sara", "#A6E3A1", "Correr mejor", 5), ("Tomás", "#F38BA8", "Volver a entrenar", 12)];
        foreach (var m in more)
        {
            var c = AddClient(alex.Id, m.Name, m.Color, m.Goal, null, 0, consentDaysAgo: null, parq: null);
            await db.SaveChangesAsync();
            var r = AddRoutine(alex.Id, c.Id, "Full body", active: true,
                ("A", [new("Sentadilla goblet", 3, 10, 12, 90, 16), new("Press con mancuernas", 3, 10, 12, 90, 14), new("Jalón al pecho", 3, 10, 12, 90, 35)]),
                ("B", [new("Peso muerto rumano con mancuernas", 3, 10, 12, 90, 14), new("Press de hombros en máquina", 3, 10, 12, 90, 25), new("Remo en polea baja", 3, 10, 12, 90, 35)]));
            await db.SaveChangesAsync();
            AddSessions(alex.Id, c.Id, r, Dates(Monday(today).AddDays(-28), today.AddDays(-m.DaysAgo), [1, 3]), hour: 17, progress: 0.004, inPerson: true);
        }
        var pablo = AddClient(alex.Id, "Pablo", "#B3B7EC", "Fuerza", "Se mudó de ciudad.", 0, consentDaysAgo: null, parq: null);
        pablo.ArchivedAt = today.AddDays(-20);
        await db.SaveChangesAsync();
        var pabloRoutine = AddRoutine(alex.Id, pablo.Id, "Fuerza 3 días", active: true,
            ("A", [new("Sentadilla", 3, 5, null, 180, 90), new("Press banca", 3, 5, null, 180, 70)]));
        await db.SaveChangesAsync();
        AddSessions(alex.Id, pablo.Id, pabloRoutine, Dates(Monday(today).AddDays(-120), today.AddDays(-25), [0, 3]), hour: 18, progress: 0.003, inPerson: true);

        // Dos entrenos de Felipe de esta semana, como si te los hubiera enviado él
        await db.SaveChangesAsync();
        foreach (var s in db.WorkoutSessions.Local.Where(s => s.ClientId == felipe.Id && s.Date >= Monday(today)).Take(2))
        {
            s.IsInPerson = false;
            s.SourceUid = Guid.NewGuid();
            s.ReceivedAt = s.Date.AddHours(1);
            s.Notes ??= "Todo bien, la rodilla sin molestias.";
        }

        AddWeights(alex.Id, felipe.Id, today.AddDays(-56), 84.2, 82.6, everyDays: 2, skipEvery: 0);
        AddWeights(alex.Id, marta.Id, today.AddDays(-50), 61.0, 61.4, everyDays: 3, skipEvery: 0);
        await db.SaveChangesAsync();
    }

    // ── Piezas ──────────────────────────────────────────────────────────

    private Client AddClient(int coachId, string name, string color, string? goal, string? notes, int trainingDays, int? consentDaysAgo, DateTime? parq)
    {
        var client = new Client
        {
            UserId = coachId, Name = name, AvatarColor = color, Goal = goal, Notes = notes, TrainingDays = trainingDays,
            HealthConsentAt = consentDaysAgo is int d ? today.AddDays(-d).AddHours(18) : null,
            ParqSignedAt = parq?.Date, CreatedAt = today.AddDays(-90)
        };
        db.Clients.Add(client);
        return client;
    }

    private Routine AddRoutine(int userId, int? clientId, string name, bool active, params (string Day, Plan[] Plans)[] days)
    {
        var routine = new Routine { UserId = userId, ClientId = clientId, Name = name, IsMarkedActive = active };
        var order = 1;
        foreach (var (dayName, plans) in days)
        {
            var day = new RoutineDay { Name = dayName, Order = order++ };
            var i = 1;
            foreach (var p in plans)
                day.Exercises.Add(new RoutineDayExercise
                {
                    ExerciseId = Ex(p.Exercise).Id, Order = i++, TargetSets = p.Sets, TargetReps = p.RepsMin, TargetRepsMax = p.RepsMax,
                    RestSeconds = p.RestSeconds, TargetRpe = p.Rpe, WarmupSets = p.Warmups > 0 ? p.Warmups : null
                });
            routine.Days.Add(day);
        }
        db.Routines.Add(routine);
        _plans[routine] = days.Select(d => d.Plans).ToList();
        return routine;
    }

    private readonly Dictionary<Routine, List<Plan[]>> _plans = new();
    private Routine? _luciaRoutine;

    /// <summary>Sesiones en esas fechas, pasando por los días de la rutina en orden y subiendo poco a poco las cargas.</summary>
    private void AddSessions(int userId, int? clientId, Routine routine, IReadOnlyList<DateTime> dates, int hour, double progress, bool inPerson = false)
    {
        var days = routine.Days.OrderBy(d => d.Order).ToList();
        var plans = _plans[routine];
        for (var n = 0; n < dates.Count; n++)
        {
            var dayIndex = n % days.Count;
            var session = new WorkoutSession
            {
                UserId = userId, ClientId = clientId, IsInPerson = inPerson, RoutineDayId = days[dayIndex].Id,
                Date = dates[n].AddHours(hour).AddMinutes(rng.Next(0, 40)), DurationMinutes = rng.Next(50, 80),
                Feeling = rng.Next(3, 6), IsComplete = true, Notes = rng.Next(5) == 0 ? SessionNotes[rng.Next(SessionNotes.Length)] : null
            };
            var factor = 1 + progress * n;
            foreach (var p in plans[dayIndex])
            {
                var we = new WorkoutExercise { ExerciseId = Ex(p.Exercise).Id };
                var kg = p.StartKg <= 0 ? 0 : Round(p.StartKg * factor, p.StartKg >= 20 ? 2.5 : 1);
                for (var w = 0; w < p.Warmups; w++)
                    we.Sets.Add(new WorkoutSet { Weight = Round(kg * (0.5 + 0.15 * w), 2.5), Reps = 5, IsWarmup = true });
                for (var s = 0; s < p.Sets; s++)
                {
                    var max = p.RepsMax ?? p.RepsMin;
                    var reps = Math.Max(1, max - s - rng.Next(0, 2));
                    reps = Math.Clamp(reps, Math.Max(1, p.RepsMin - 1), max);
                    we.Sets.Add(new WorkoutSet
                    {
                        Weight = kg, Reps = reps,
                        Rpe = p.Rpe is double rpe && s == p.Sets - 1 ? Math.Min(10, rpe + rng.Next(0, 2) * 0.5) : null
                    });
                }
                session.Exercises.Add(we);
            }
            db.WorkoutSessions.Add(session);
        }
    }

    /// <summary>Pesajes de «from» a hoy, de «startKg» a «endKg» con el vaivén de cada día.</summary>
    private void AddWeights(int userId, int? clientId, DateTime from, double startKg, double endKg, int everyDays, int skipEvery)
    {
        var total = (today - from).Days;
        for (var d = 0; d <= total; d += everyDays)
        {
            if (skipEvery > 0 && d % skipEvery == skipEvery - 1) continue;
            var kg = startKg + (endKg - startKg) * d / total + Math.Sin(d * 1.7) * 0.35 + (rng.NextDouble() - 0.5) * 0.3;
            db.BodyWeights.Add(new BodyWeightEntry { UserId = userId, ClientId = clientId, Date = from.AddDays(d).Date, Kg = Math.Round(kg, 1) });
        }
    }

    private Exercise Ex(string name) =>
        library.TryGetValue(name, out var e) ? e : throw new InvalidOperationException($"No está en la biblioteca: {name}");

    private static double Round(double kg, double step) => Math.Round(kg / step) * step;

    private static DateTime Monday(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    /// <summary>Los días de la semana indicados (0 = lunes) entre dos fechas, ambas incluidas.</summary>
    private static List<DateTime> Dates(DateTime from, DateTime to, int[] weekdays)
    {
        var list = new List<DateTime>();
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            if (weekdays.Contains(((int)d.DayOfWeek + 6) % 7)) list.Add(d);
        return list;
    }
}
