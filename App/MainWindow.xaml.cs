using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tolerance.Core;

namespace Tolerance.App;

public partial class MainWindow : Window
{
    public QueryViewModel ViewModel { get; } = new();
    private readonly DispatcherTimer copyRetry = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private string pendingCopy = "";
    private int attempts;
    public MainWindow()
    {
        InitializeComponent();
        // Fit this application's window to the available desktop; the fixed
        // content scales down instead of introducing scroll bars.
        var area = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, area.Width);
        MinHeight = Math.Min(MinHeight, area.Height);
        Width = Math.Min(Width, area.Width);
        Height = Math.Min(Height, area.Height);
        DataContext = ViewModel;
        ViewModel.QueryChanged += (_, _) => { copyRetry.Stop(); CopyStatus.Text = "数值不含单位，可直接粘贴到绘图软件"; };
        copyRetry.Tick += (_, _) => TryCopy();
        Closed += (_, _) => copyRetry.Stop();
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
        pendingCopy = mode switch { "mm" => ViewModel.Millimeters, "μm" => ViewModel.MicrometerValue, _ => ViewModel.FullResult };
        attempts = 0;
        TryCopy();
    }
    private void TryCopy()
    {
        try
        {
            Clipboard.SetText(pendingCopy);
            copyRetry.Stop();
            CopyStatus.Text = "已复制到剪贴板。";
        }
        catch (ExternalException)
        {
            if (++attempts < 5) copyRetry.Start();
            else { copyRetry.Stop(); CopyStatus.Text = "剪贴板正被占用，请稍后再次点击复制。"; }
        }
    }
}
