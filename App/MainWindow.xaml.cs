using System.Windows;
using System.Windows.Controls;
using Tolerance.Core;

namespace Tolerance.App;

public partial class MainWindow : Window
{
    public QueryViewModel ViewModel { get; } = new();
    private readonly ClipboardService clipboard;
    private readonly bool ownsClipboard;
    private readonly ClickCopy mmCopy,umCopy;
    private readonly Action<string>? feedback;
    public MainWindow() : this(new ClipboardService(), true) { }
    internal MainWindow(ClipboardService clipboard, bool ownsClipboard = false,Action<string>? feedback=null)
    {
        this.clipboard = clipboard;
        this.ownsClipboard = ownsClipboard;
        this.feedback=feedback;
        InitializeComponent();
        _=new ContentWindowSizing(this,new Size(DesignSurface.Width,DesignSurface.Height),LayoutHost.Margin);
        DataContext = ViewModel;
        mmCopy=new(ResultNumber,v=>clipboard.Copy(v.Text,ReportCopy,v.SuccessMessage),CopyMm);
        umCopy=new(ResultUmNumber,v=>clipboard.Copy(v.Text,ReportCopy,v.SuccessMessage),CopyUm);
        void UpdateTargets()
        {
            mmCopy.Update(new(ViewModel.Millimeters,"公差值","mm",ViewModel.HasResult));
            umCopy.Update(new(ViewModel.MicrometerValue,"公差值","μm",ViewModel.HasResult));
        }
        ViewModel.PropertyChanged+=(_,_)=>UpdateTargets();UpdateTargets();
        ViewModel.QueryChanged += (_, _) => { clipboard.Cancel(); ReportCopy(""); };
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
    private void CopyFull_Click(object sender, RoutedEventArgs e) => Copy("full");
    internal void Copy(string mode)
    {
        if (!ViewModel.HasResult) return;
        var text = mode switch { "mm" => ViewModel.Millimeters, "μm" => ViewModel.MicrometerValue, _ => ViewModel.FullResult };
        clipboard.Copy(text, ReportCopy,"已复制完整结果。");
    }
    private void ReportCopy(string message){if(feedback is null)CopyStatus.Text=message;else feedback(message);}
}
