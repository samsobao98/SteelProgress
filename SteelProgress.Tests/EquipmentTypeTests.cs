using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Tests;

// Tipo de material obligatorio: deducirlo de lo que había, enseñarlo y guardarlo
public class EquipmentTypeTests
{
    private sealed record LibraryItem(string Name, string? Equipment);

    // La biblioteca precargada, leída del archivo de la app (la salida de compilación está en otra carpeta)
    private static List<LibraryItem> Library([CallerFilePath] string here = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(here)!, "..", "SteelProgress.Maui", "Resources", "Raw", "exercise_library.json");
        return JsonSerializer.Deserialize<List<LibraryItem>>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    [Theory]
    [InlineData("Barra", "Press banca", EquipmentType.Barbell)]
    [InlineData("Mancuernas", "Curl martillo", EquipmentType.Dumbbells)]
    [InlineData("Polea", "Jalón al pecho", EquipmentType.Cable)]
    [InlineData("Peso corporal", "Dominadas", EquipmentType.Bodyweight)]
    [InlineData("Kettlebell", "Swing con kettlebell", EquipmentType.Kettlebell)]
    [InlineData("Banco", "Fondos entre bancos", EquipmentType.Bodyweight)]
    [InlineData("Otro", "Comba", EquipmentType.Other)]
    [InlineData("Máquina", "Prensa de piernas", EquipmentType.PlateMachine)]
    [InlineData("Máquina", "Sentadilla hack", EquipmentType.PlateMachine)]
    [InlineData("Máquina", "Elevación de talones sentado", EquipmentType.PlateMachine)]
    [InlineData("Máquina", "Pec deck (contractora)", EquipmentType.StackMachine)]
    [InlineData("Máquina", "Extensiones de cuádriceps", EquipmentType.StackMachine)]
    [InlineData("Máquina", "Cinta de correr", EquipmentType.Other)]
    [InlineData("Máquina", "Remo (máquina)", EquipmentType.Other)]
    [InlineData("Máquina", "Remo en máquina", EquipmentType.StackMachine)]
    [InlineData("MAQUINA", "prensa inclinada", EquipmentType.PlateMachine)]
    [InlineData("Multipower", "Sentadilla en multipower", EquipmentType.Barbell)]
    [InlineData("gomas", "Face pull con goma", EquipmentType.Other)]
    // Sin material escrito: se mira el nombre
    [InlineData(null, "Remo con barra", EquipmentType.Barbell)]
    [InlineData("", "Press inclinado con mancuernas", EquipmentType.Dumbbells)]
    [InlineData(null, "Cruce de poleas", EquipmentType.Cable)]
    [InlineData(null, "Press en máquina", EquipmentType.StackMachine)]
    [InlineData(null, "Dominadas lastradas", EquipmentType.Bodyweight)]
    [InlineData(null, "Algo raro", EquipmentType.Other)]
    public void ClassifiesFromEquipmentOrName(string? equipment, string name, EquipmentType expected) =>
        Assert.Equal(expected, EquipmentCatalog.Classify(equipment, name));

    [Fact]
    public void WholeLibraryGetsATypeConsistentWithItsEquipment()
    {
        var library = Library();
        Assert.Equal(111, library.Count);
        foreach (var item in library)
        {
            var type = EquipmentCatalog.Classify(item.Equipment, item.Name);
            var expected = item.Equipment switch
            {
                "Barra" => new[] { EquipmentType.Barbell },
                "Mancuernas" => new[] { EquipmentType.Dumbbells },
                "Polea" => new[] { EquipmentType.Cable },
                "Peso corporal" or "Banco" => new[] { EquipmentType.Bodyweight },
                "Kettlebell" => new[] { EquipmentType.Kettlebell },
                "Máquina" => new[] { EquipmentType.StackMachine, EquipmentType.PlateMachine, EquipmentType.Other },
                _ => new[] { EquipmentType.Other }
            };
            Assert.True(expected.Contains(type), $"{item.Name} ({item.Equipment}) → {type}");
        }

        var machines = library.Where(i => i.Equipment == "Máquina")
            .GroupBy(i => EquipmentCatalog.Classify(i.Equipment, i.Name))
            .ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(11, machines[EquipmentType.StackMachine]);
        Assert.Equal(4, machines[EquipmentType.PlateMachine]);  // prensa, hack, talones en prensa y talones sentado
        Assert.Equal(6, machines[EquipmentType.Other]);                     // cardio
    }

    [Fact]
    public void DisplayUsesTheTypeAndKeepsWhatWasWrittenInOther()
    {
        Assert.Equal("Máquina de discos", EquipmentCatalog.DisplayText(new Exercise { Equipment = "Máquina", EquipmentType = EquipmentType.PlateMachine }));
        Assert.Equal("Gomas", EquipmentCatalog.DisplayText(new Exercise { Equipment = "Gomas", EquipmentType = EquipmentType.Other }));
        Assert.Equal("Otro", EquipmentCatalog.DisplayText(new Exercise { EquipmentType = EquipmentType.Other }));
        Assert.Equal("Barra", EquipmentCatalog.DisplayText(new Exercise { Equipment = "Barra" }));
        Assert.Equal("", EquipmentCatalog.DisplayText(new Exercise()));

        Assert.Equal("Polea", EquipmentCatalog.StoredText(EquipmentType.Cable, "lo que sea"));
        Assert.Equal("TRX", EquipmentCatalog.StoredText(EquipmentType.Other, " TRX "));
        Assert.Null(EquipmentCatalog.StoredText(EquipmentType.Other, " "));

        var row = new ExerciseListItem(new Exercise { Name = "Prensa", Equipment = "Máquina", EquipmentType = EquipmentType.PlateMachine, IsSystem = true }, null);
        Assert.Equal("Máquina de discos", row.DetailText);
    }

    [Fact]
    public async Task StartupFillsTheTypeOfEveryExerciseOfEveryProfile()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var user = new CurrentUserService();
        user.SetCurrentUser(1);
        AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options, user);

        using (var db = Db())
        {
            db.Database.Migrate();
            db.UserProfiles.AddRange(new UserProfile { Id = 1, Name = "Samuel" }, new UserProfile { Id = 2, Name = "Laura" });
            db.Exercises.AddRange(
                new Exercise { Name = "Prensa de piernas", MuscleGroup = "Piernas", Equipment = "Máquina", IsSystem = true },
                new Exercise { Name = "Remo con barra", MuscleGroup = "Espalda", UserId = 1 },
                new Exercise { Name = "Aperturas", MuscleGroup = "Pecho", Equipment = "Polea", UserId = 2 },
                new Exercise { Name = "Ya elegido", MuscleGroup = "Pecho", Equipment = "Barra", EquipmentType = EquipmentType.Cable, UserId = 1 });
            await db.SaveChangesAsync();
        }

        using (var db = Db())
            Assert.Equal(3, await new ExerciseRepository(db).FillMissingEquipmentTypesAsync());

        using (var db = Db())
        {
            var types = db.Exercises.IgnoreQueryFilters().ToDictionary(e => e.Name, e => e.EquipmentType);
            Assert.Equal(EquipmentType.PlateMachine, types["Prensa de piernas"]);
            Assert.Equal(EquipmentType.Barbell, types["Remo con barra"]);
            Assert.Equal(EquipmentType.Cable, types["Aperturas"]);
            Assert.Equal(EquipmentType.Cable, types["Ya elegido"]);   // lo elegido a mano no se toca
            Assert.Equal(0, await new ExerciseRepository(db).FillMissingEquipmentTypesAsync());
        }
    }
}
