using Microsoft.EntityFrameworkCore;
using SteelProgress.Domain.Services;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Data.Context;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<Exercise> Exercises { get; set; }
    public DbSet<Routine> Routines { get; set; }
    public DbSet<RoutineDay> RoutineDays { get; set; }
    public DbSet<RoutineDayExercise> RoutineDayExercises { get; set; }
    public DbSet<WorkoutSession> WorkoutSessions { get; set; }
    public DbSet<WorkoutExercise> WorkoutExercises { get; set; }
    public DbSet<WorkoutSet> WorkoutSets { get; set; }
    public DbSet<ExerciseMedia> ExerciseMedia { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<ClientLimitation> ClientLimitations { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Exercise>()
            .HasOne(e => e.UserProfile)
            .WithMany(up => up.Exercises)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Exercise>()
            .HasQueryFilter(e => _currentUserService == null || e.UserId == null || e.UserId == _currentUserService.CurrentUserId);

        modelBuilder.Entity<Routine>()
            .HasOne(r => r.UserProfile)
            .WithMany(up => up.Routines)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        // Con un cliente activo se ven sus rutinas y sus sesiones; sin cliente, las propias
        modelBuilder.Entity<Routine>()
            .HasQueryFilter(r => _currentUserService == null
                || (r.UserId == _currentUserService.CurrentUserId && r.ClientId == _currentUserService.CurrentClientId));

        modelBuilder.Entity<WorkoutSession>()
            .HasOne(ws => ws.UserProfile)
            .WithMany(up => up.WorkoutSessions)
            .HasForeignKey(ws => ws.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        modelBuilder.Entity<WorkoutSession>()
            .HasQueryFilter(s => _currentUserService == null
                || (s.UserId == _currentUserService.CurrentUserId && s.ClientId == _currentUserService.CurrentClientId));

        // Clientes del entrenador: al borrar un cliente se borran sus rutinas y sus sesiones
        modelBuilder.Entity<Client>()
            .HasOne(c => c.UserProfile)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Client>()
            .HasQueryFilter(c => _currentUserService == null || c.UserId == _currentUserService.CurrentUserId);
        modelBuilder.Entity<Client>().HasIndex(c => c.Uid).IsUnique();
        modelBuilder.Entity<Routine>()
            .HasOne(r => r.Client)
            .WithMany(c => c.Routines)
            .HasForeignKey(r => r.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<WorkoutSession>()
            .HasOne(s => s.Client)
            .WithMany(c => c.WorkoutSessions)
            .HasForeignKey(s => s.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
        // Limitaciones: se borran con su cliente y se filtran como él
        modelBuilder.Entity<ClientLimitation>()
            .HasOne(l => l.Client)
            .WithMany(c => c.Limitations)
            .HasForeignKey(l => l.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ClientLimitation>()
            .HasQueryFilter(l => _currentUserService == null || l.Client!.UserId == _currentUserService.CurrentUserId);

        // Igual que en la migración AddUserProfiles, para que el modelo no pida reconstruir la tabla
        modelBuilder.Entity<UserProfile>()
            .Property(p => p.AvatarColor)
            .HasDefaultValue("#89B4FA");

        // Uid: identidad estable para exportar/importar sin duplicar
        // Ejercicios y rutinas: únicos por perfil, porque dos perfiles del mismo móvil pueden importar
        // el mismo ejercicio o la misma rutina y cada uno debe poder reconocerlo como suyo
        modelBuilder.Entity<Exercise>().HasIndex(e => new { e.UserId, e.Uid }).IsUnique();
        modelBuilder.Entity<Routine>().HasIndex(r => new { r.UserId, r.Uid }).IsUnique();
        modelBuilder.Entity<RoutineDay>().HasIndex(d => d.Uid).IsUnique();
        modelBuilder.Entity<RoutineDayExercise>().HasIndex(rde => rde.Uid).IsUnique();
        modelBuilder.Entity<WorkoutSession>().HasIndex(ws => ws.Uid).IsUnique();

        // Al borrar un ejercicio se borran sus vídeos e imágenes (los archivos los borra ExerciseMediaService)
        modelBuilder.Entity<ExerciseMedia>()
            .HasOne(m => m.Exercise)
            .WithMany(e => e.Media)
            .HasForeignKey(m => m.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ExerciseMedia>().HasIndex(m => new { m.ExerciseId, m.Uid }).IsUnique();
        // Mismo filtro que su ejercicio, para no ver la multimedia de otros perfiles
        modelBuilder.Entity<ExerciseMedia>()
            .HasQueryFilter(m => _currentUserService == null || m.Exercise!.UserId == null || m.Exercise.UserId == _currentUserService.CurrentUserId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUserService?.CurrentUserId is int userId)
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added) continue;

                var clientId = _currentUserService.CurrentClientId;

                if (entry.Entity is Exercise ex && ex.UserId == null && !ex.IsSystem)
                    ex.UserId = userId;
                else if (entry.Entity is Routine r && r.UserId == 0)
                {
                    r.UserId = userId;
                    r.ClientId ??= clientId;
                }
                else if (entry.Entity is WorkoutSession ws && ws.UserId == 0)
                {
                    ws.UserId = userId;
                    if (ws.ClientId is null && clientId is not null)
                    {
                        // Entreno con un cliente desde el móvil del entrenador
                        ws.ClientId = clientId;
                        ws.IsInPerson = true;
                    }
                }
                else if (entry.Entity is Client c && c.UserId == 0)
                    c.UserId = userId;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
