using System.Windows;
using System.Windows.Controls;
using Tolerance.Core;

namespace Tolerance.App;

public partial class MainWindow : Window
{
    public QueryViewModel ViewModel { get; } = new();
    private readonly ClipboardService clipboard;
    private readonly bool ownsClipboard;
    public MainWindow() : this(new ClipboardService(), true) { }
    internal MainWindow(ClipboardService clipboard, bool ownsClipboard = false)
    {
        this.clipboard = clipboard;
        this.ownsClipboard = ownsClipboard;
        InitializeComponent();
        // Fit this application's window to the available desktop; the fixed
        // content scales down instead of introducing scroll bars.
        var area = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, area.Width);
        MinHeight = Math.Min(MinHeight, area.Height);
        Width = Math.Min(Width, area.Width);
        Height = Math.Min(Height, area.Height);
        DataContext = ViewModel;
        ViewModel.QueryChanged += (_, _) => { clipboard.Cancel(); CopyStatus.Text = "数值不含单位，可直接粘贴到绘图软件"; };
        Closed += (_, _) => { clipboard.Cancel(); if (ownsClipboard) clipboard.Dispose(); };
    }
    private void TypeChoice_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: ToleranceType type }) ViewModel.SelectedType = type;
    }
    private void GradeChoice_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string code, IsEnabled: true }) ViewModel.SelectedGradeCode = code;
    }
    private void GradeChoice_Click(object sender, RoutedEventArgs e) => ViewModel.ShowGradeAdvice();
    private void CopyMm_Click(object sender, RoutedEventArgs e) => Copy("mm");
    private void CopyUm_Click(object sender, RoutedEventArgs e) => Copy("μm");
    private void CopyFull_Click(object sender, RoutedEventArgs e) => Copy("full");
    internal void Copy(string mode)
    {
        if (!ViewModel.HasResult) return;
        var text = mode switch { "mm" => ViewModel.Millimeters, "μm" => ViewModel.MicrometerValue, _ => ViewModel.FullResult };
        clipboard.Copy(text, message => CopyStatus.Text = message);
    }
}
