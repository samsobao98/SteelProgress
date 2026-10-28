using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Controls;

// La rutina activa se pinta como placa con su semana; el resto, como tarjeta
public class RoutineTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ActiveTemplate { get; set; }
    public DataTemplate? CardTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        (item is RoutineListItem { IsActive: true } ? ActiveTemplate : CardTemplate)!;
}
