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
        modelBuilder.Entity<Routine>()
            .HasQueryFilter(r => _currentUserService == null || r.UserId == _currentUserService.CurrentUserId);

        modelBuilder.Entity<WorkoutSession>()
            .HasOne(ws => ws.UserProfile)
            .WithMany(up => up.WorkoutSessions)
            .HasForeignKey(ws => ws.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        modelBuilder.Entity<WorkoutSession>()
            .HasQueryFilter(s => _currentUserService == null || s.UserId == _currentUserService.CurrentUserId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUserService?.CurrentUserId is int userId)
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added) continue;

                if (entry.Entity is Exercise ex && ex.UserId == null)
                    ex.UserId = userId;
                else if (entry.Entity is Routine r && r.UserId == 0)
                    r.UserId = userId;
                else if (entry.Entity is WorkoutSession ws && ws.UserId == 0)
                    ws.UserId = userId;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
