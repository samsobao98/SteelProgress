using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Data.Context
{
    // Usamos DBContext para la conexion entre EF core y la base de datos
    public class AppDbContext : DbContext 
    {
        public DbSet<Exercise> Exercises { get; set; }
        public DbSet<Routine> Routines { get; set; }
        public DbSet<RoutineDay> RoutineDays { get; set; }
        public DbSet<RoutineDayExercise> RoutineDayExercises { get; set; }
        public DbSet<WorkoutSession> WorkoutSessions { get; set; }
        public DbSet<WorkoutExercise> WorkoutExercises { get; set; }
        public DbSet<WorkoutSet> WorkoutSets { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
    }
}
