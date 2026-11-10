using System.Windows;

namespace Tolerance.App;

// View-only state: selection is independent from keyboard focus and rank colour.
internal static class SelectionVisual
{
    public static readonly DependencyProperty IsSelectedProperty=DependencyProperty.RegisterAttached("IsSelected",typeof(bool),typeof(SelectionVisual),new FrameworkPropertyMetadata(false));
    public static bool GetIsSelected(DependencyObject target)=>(bool)target.GetValue(IsSelectedProperty);
    public static void SetIsSelected(DependencyObject target,bool value)=>target.SetValue(IsSelectedProperty,value);
}
