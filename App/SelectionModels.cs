using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Tolerance.Core;

namespace Tolerance.App;

public sealed record TypeChoice(ToleranceType? Type, string Name, string Geometry, string? ArrowHeads = null)
{
    public bool IsAvailable => Type is not null;
    public string Help => Type?.Hint ?? "位置度按数系选取，本版未提供等级查值";
}
public sealed record TypeGroup(string Name, TypeChoice[] Choices)
{
    public int Columns => Choices.Length;
}
public sealed record GradeChoice(string Code, bool IsAvailable);

public sealed class SelectionEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is not null && values[0] != DependencyProperty.UnsetValue && Equals(values[0], values[1]);
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
