using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Repositories;
using SteelProgress.Core.Services;
using SteelProgress.Maui.Pages;

namespace SteelProgress.Maui.Services;

/// <summary>
/// Abre la ficha de un ejercicio (vídeo, datos y su progreso) desde fuera de Ejercicios.
/// En vista simple el apartado Ejercicios no está: la ficha se abre desde la rutina.
/// </summary>
public static class ExerciseDetailLauncher
{
    public static async Task OpenAsync(INavigation navigation, IServiceProvider services, int exerciseId)
    {
        var exercise = await services.GetRequiredService<ExerciseRepository>().GetWithMediaAsync(exerciseId);
        if (exercise is null) return;

        var log = await services.GetRequiredService<WorkoutRepository>().GetTrainingLogAsync();
        var progress = TrainingStats.Exercises(log.Sets).FirstOrDefault(p => p.ExerciseId == exerciseId);

        // Sin «Editar»: editar el ejercicio es cosa del apartado Ejercicios (vista detallada)
        await ExerciseDetailPage.Show(navigation, new ExerciseDetailViewModel(exercise, progress) { ReadOnly = true }, onEdit: null);
    }
}
