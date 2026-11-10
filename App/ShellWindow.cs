using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Tolerance.Core;
namespace Tolerance.App;

internal sealed partial class ShellWindow : Window
{
    internal ToolSettings Settings { get; private set; }
    internal SettingsStore Store { get; }
    internal readonly MainWindow Geometry;
    internal readonly Dictionary<string,SizePage> SizePages=[];
    internal string ActivePage { get; private set; }="geometry";
    internal readonly Grid Design=new(){Width=1180,Height=770};
    internal readonly ContentControl Host=new();
    private readonly UIElement geometryContent;
    private readonly TextBlock status=UI.Text("",12);
    private readonly FeedbackStatus feedback=new();
    private readonly Dictionary<string,Button> nav=[];
    private sealed record FontBaseline(double Size);
    private readonly ConditionalWeakTable<DependencyObject,FontBaseline> originalFonts=new();
    private UIElement? settingsView;
    private readonly List<Action> settingsRefreshers=[];
    private readonly Func<bool> systemThemeIsDark;
    private readonly ClipboardService clipboard;
    private (string Unit,int Places)? copyFormat;
    private readonly DispatcherTimer systemTimer=new(){Interval=TimeSpan.FromSeconds(2)};
    private bool restoring;
    internal ShellWindow(string? configPath=null, ClipboardService? clipboard=null,Func<bool>? systemThemeIsDark=null)
    {
        this.clipboard=clipboard??new ClipboardService();
        this.systemThemeIsDark=systemThemeIsDark??ReadSystemDark;
        PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape&&ContextHelp.Close())e.Handled=true;};
        Geometry=new(this.clipboard,feedback:ReportFeedback);
        Store=new(configPath??SettingsStore.DefaultPath);Settings=Store.Load();
        feedback.Loaded(Store.Status);
        Title="公差查询工具";Width=1240;Height=870;MinWidth=1000;MinHeight=700;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=14;UseLayoutRounding=true;
        SetResourceReference(BackgroundProperty,"CanvasBackground");SetResourceReference(ForegroundProperty,"Ink");
        geometryContent=(UIElement)Geometry.Content;Geometry.Content=null;DataContext=Geometry.ViewModel;
        var outer=new Grid{Margin=new Thickness(16)};Content=outer;
        var viewbox=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Child=Design};outer.Children.Add(viewbox);
        _=new ContentWindowSizing(this,new Size(Design.Width,Design.Height),outer.Margin);
        Design.RowDefinitions.Add(new(){Height=new GridLength(48)});Design.RowDefinitions.Add(new(){Height=new GridLength(700)});Design.RowDefinitions.Add(new(){Height=new GridLength(22)});
        var navigation=new Grid{Name="NavigationBar"};Design.Children.Add(navigation);
        var rail=new Border{BorderThickness=new Thickness(0,0,0,1),Margin=new Thickness(0,0,0,8)};rail.SetResourceReference(Border.BackgroundProperty,"Surface");rail.SetResourceReference(Border.BorderBrushProperty,"Hairline");Grid.SetColumnSpan(rail,9);navigation.Children.Add(rail);
        foreach(var width in new[]{new GridLength(112),new GridLength(112),new GridLength(16),new GridLength(132),new GridLength(132),new GridLength(16),new GridLength(120),new GridLength(1,GridUnitType.Star),new GridLength(88)})navigation.ColumnDefinitions.Add(new(){Width=width});
        foreach(int column in new[]{2,5})
        {
            var divider=new Border{Width=1,Height=16,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,0,8)};
            divider.SetResourceReference(Border.BackgroundProperty,"Hairline");Grid.SetColumn(divider,column);navigation.Children.Add(divider);
        }
        int[] columns=[0,1,3,4,6,8];
        foreach(string id in new[]{"shaft","hole","shaft-fit","hole-fit","geometry","settings"})
        {
            var b=new Button{Content=PageTitle(id),Style=(Style)FindResource("NavigationButton"),Margin=new Thickness(0,0,4,8)};
            Grid.SetColumn(b,columns[nav.Count]);navigation.Children.Add(b);nav.Add(id,b);b.Click+=(_,_)=>Navigate(id);
        }
        Grid.SetRow(Host,1);Design.Children.Add(Host);Grid.SetRow(status,2);Design.Children.Add(status);
        var catalog=new SizeToleranceCatalog();
        foreach(string id in new[]{"shaft","hole","shaft-fit","hole-fit"})
        {
            var initial=id=="shaft-fit"?new PageInput{HoleZone="G",ShaftZone="h"}:new PageInput();
            if(Settings.RememberInputs&&Settings.Inputs.TryGetValue(id,out var cached)&&cached is not null)initial=cached;
            if(id=="shaft-fit")initial.ShaftZone="h";if(id=="hole-fit")initial.HoleZone="H";
            SizePages[id]=new(this,id,initial,catalog);
        }
        RestoreGeometry();Geometry.ViewModel.QueryChanged+=(_,_)=>SaveInputs();
        systemTimer.Tick+=(_,_)=>{if(Settings.Theme=="system")ApplyTheme();};systemTimer.Start();
        Closed+=(_,_)=>{ContextHelp.Close();SaveInputs();this.clipboard.Dispose();systemTimer.Stop();Geometry.Close();};
        ApplySettings();Navigate(Settings.StartupPage=="last"?Settings.LastPage:Settings.StartupPage);
    }
    internal static string PageTitle(string id)=>id switch{"shaft"=>"轴偏差","hole"=>"孔偏差","shaft-fit"=>"基轴制配合","hole-fit"=>"基孔制配合","settings"=>"设置",_=>"形位公差"};
    internal void Navigate(string id)
    {
        if(id!=ActivePage){clipboard.Cancel();ReportFeedback("");Geometry.CopyStatus.Text="";ContextHelp.Close();}
        ActivePage=id;Host.Content=id=="geometry"?geometryContent:id=="settings"?(settingsView??=BuildSettings()):SizePages.GetValueOrDefault(id);
        if(id!="settings"){Settings.LastPage=id;SaveSettings();}
        UpdateNavigation();
        UpdateLayout();ApplyFonts(Design);RefreshStatus();
    }
    private void UpdateNavigation()
    {
        foreach(var(key,button) in nav)
        {
            bool active=key==ActivePage;
            if(active){button.SetResourceReference(BorderBrushProperty,"Accent");button.SetResourceReference(BackgroundProperty,"SelectionSurface");}
            else{button.BorderBrush=Brushes.Transparent;button.Background=Brushes.Transparent;}
            button.SetResourceReference(ForegroundProperty,active?"Accent":"Secondary");button.FontWeight=active?FontWeights.Medium:FontWeights.Normal;
        }
    }
    private void RestoreGeometry()
    {
        if(!Settings.RememberInputs||!Settings.Inputs.TryGetValue("geometry",out var input)||input is null)return;
        var vm=Geometry.ViewModel;var type=vm.Types.FirstOrDefault(t=>t.Id==input.TypeId);
        if(type is not null)vm.SelectedType=type;vm.SelectedGradeCode=input.GradeCode??"6";vm.ParameterText=input.Parameter??"100";vm.SizeToleranceText=input.SizeTolerance??"";
    }
    internal void SaveInputs()
    {
        if(restoring)return;
        if(Settings.RememberInputs)
        {
            foreach(var(id,p) in SizePages)Settings.Inputs[id]=p.Input;
            var vm=Geometry.ViewModel;Settings.Inputs["geometry"]=new(){TypeId=vm.SelectedType.Id,GradeCode=vm.SelectedGradeCode,Parameter=vm.ParameterText,SizeTolerance=vm.SizeToleranceText};
        }
        else Settings.Inputs.Clear();
        SaveSettings();
    }
    internal void ApplySettings()
    {
        var format=(Settings.CopyUnit,Settings.DecimalPlaces);
        bool refreshFormatting=copyFormat is null||NumberDisplay.MinimumDecimals!=Settings.DecimalPlaces;
        if(copyFormat!=format){clipboard.Cancel();ReportFeedback("");}
        copyFormat=format;
        NumberDisplay.MinimumDecimals=Settings.DecimalPlaces;Topmost=Settings.Topmost;ApplyTheme();
        if(refreshFormatting){foreach(var p in SizePages.Values)p.Refresh();Geometry.ViewModel.RefreshFormatting();}
        foreach(var refresh in settingsRefreshers)refresh();
        ApplyFonts(Design);SaveInputs();
    }
    internal void ApplyTheme()
    {
        bool dark=Settings.Theme=="dark"||Settings.Theme=="system"&&systemThemeIsDark();
        if(!Appearance.Apply(dark))return;
        foreach(var p in SizePages.Values)p.RefreshAppearance();
    }
    private static bool ReadSystemDark()
    {
        try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");return key?.GetValue("AppsUseLightTheme") is int value&&value==0;}
        catch(System.Security.SecurityException){return false;}
    }
    internal void ApplyFonts(DependencyObject root)
    {
        var targets=new List<(DependencyObject Node,DependencyProperty Property,double Size)>();
        void Collect(DependencyObject node)
        {
            DependencyProperty? property=node is TextBlock?TextBlock.FontSizeProperty:node is Control?Control.FontSizeProperty:null;
            if(property is not null)
            {
                var source=DependencyPropertyHelper.GetValueSource(node,property);
                // Template text inherits the control's scaled font. Scaling it again compounds size.
                bool cached=originalFonts.TryGetValue(node,out var baseline);
                if(cached||source.BaseValueSource!=BaseValueSource.Inherited&&source.BaseValueSource!=BaseValueSource.Default)
                {
                    if(baseline is null){baseline=new((double)node.GetValue(property));originalFonts.Add(node,baseline);}
                    targets.Add((node,property,baseline.Size));
                }
            }
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Collect(VisualTreeHelper.GetChild(node,i));
        }
        Collect(root);
        foreach(var (node,property,size) in targets)node.SetValue(property,size*(Settings.LargeFont?1.12:1));
    }
    private void RefreshStatus()=>status.Text=feedback.Text;
    internal void ReportFeedback(string message){feedback.Report(message);RefreshStatus();}
    private void SaveSettings(){bool saved=Store.Save(Settings);feedback.Saved(saved,Store.Status);RefreshStatus();}
    internal void Copy(string text,string? successMessage=null) => clipboard.Copy(text,ReportFeedback,successMessage);
    internal string CopyStatus=>status.Text;
    internal void CancelCopy(string pageId) { if(ActivePage==pageId){clipboard.Cancel();ReportFeedback("");} }
    private UIElement BuildSettings()
    {
        var card=UI.Card();var root=new StackPanel();card.Child=root;
        root.Children.Add(UI.SectionHeading("工作环境", "显示、窗口与查询偏好",new Thickness(0,0,0,20)));
        var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=new GridLength(40)});grid.ColumnDefinitions.Add(new());root.Children.Add(grid);
        var left=new StackPanel();var right=new StackPanel();grid.Children.Add(left);Grid.SetColumn(right,2);grid.Children.Add(right);
        void Select(Panel panel,string label,string[] labels,string[] values,Func<string> current,Action<string> set,string? help=null)
        {
            panel.Children.Add(UI.SectionHeading(label,null,new Thickness(0,0,0,8)));
            var wrap=new WrapPanel{Margin=new Thickness(0,0,0,20)};panel.Children.Add(wrap);
            for(int i=0;i<labels.Length;i++)
            {
                string value=values[i];var b=new Button{Content=labels[i],Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,8,8)};
                b.BorderThickness=new Thickness(1);
                System.Windows.Automation.AutomationProperties.SetName(b,label+"："+labels[i]);
                void RefreshSelection()
                {
                    bool selected=value==current();
                    b.SetResourceReference(BorderBrushProperty,selected?"Accent":"Hairline");b.SetResourceReference(BackgroundProperty,selected?"SelectionSurface":"ControlSurface");
                    System.Windows.Automation.AutomationProperties.SetItemStatus(b,selected?"已选中":"未选中");
                }
                settingsRefreshers.Add(RefreshSelection);RefreshSelection();
                if(help is not null)ContextHelp.Attach(b,help);
                b.Click+=(_,_)=>{set(value);ApplySettings();};wrap.Children.Add(b);
            }
        }
        Select(left,"启动页面",["上次查询页","轴偏差","孔偏差","基轴制","基孔制","形位公差"],["last","shaft","hole","shaft-fit","hole-fit","geometry"],()=>Settings.StartupPage,v=>Settings.StartupPage=v);
        Select(left,"记住各页面输入",["开启","关闭并清除记录"],["on","off"],()=>Settings.RememberInputs?"on":"off",v=>{Settings.RememberInputs=v=="on";if(!Settings.RememberInputs)Settings.Inputs.Clear();});
        Select(left,"窗口置顶",["关闭","开启"],["off","on"],()=>Settings.Topmost?"on":"off",v=>Settings.Topmost=v=="on");
        left.Children.Add(BuildAbout());
        Select(right,"小数显示（至少保留，不截断）",["自动","3 位","4 位","6 位"],["0","3","4","6"],()=>Settings.DecimalPlaces.ToString(),v=>Settings.DecimalPlaces=int.Parse(v),"只补零，不截断有效精度。输入公称尺寸和极限尺寸的单位始终为 mm；点击数值按显示内容复制，不附带单位。");
        Select(right,"主题",["浅色","深色","跟随系统"],["light","dark","system"],()=>Settings.Theme,v=>Settings.Theme=v,"主题仅影响本工具；图例与公差带保留白底绘图区。");
        Select(right,"字号",["标准","大号"],["standard","large"],()=>Settings.LargeFont?"large":"standard",v=>Settings.LargeFont=v=="large");
        var reset=new Button{Content="恢复默认设置及输入",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,12)};right.Children.Add(reset);
        reset.Click+=(_,_)=>Reset();
        return card;
    }
    internal void Reset()
    {
        restoring=true;Settings=new();
        foreach(var(id,p) in SizePages)p.Restore(id=="shaft-fit"?new(){HoleZone="G",ShaftZone="h"}:new());
        var vm=Geometry.ViewModel;vm.SelectedType=vm.Types.Single(t=>t.Id=="flatness");vm.SelectedGradeCode="6";vm.ParameterText="100";vm.SizeToleranceText="";
        restoring=false;ApplySettings();Navigate("settings");
    }
}
