using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Tolerance.Core;
namespace Tolerance.App;

internal sealed class ShellWindow : Window
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
    private readonly Dictionary<string,Button> nav=[];
    private readonly Dictionary<DependencyObject,double> originalFonts=[];
    private readonly ClipboardService clipboard;
    private (string Unit,int Places)? copyFormat;
    private readonly DispatcherTimer systemTimer=new(){Interval=TimeSpan.FromSeconds(2)};
    private bool restoring;
    internal ShellWindow(string? configPath=null, ClipboardService? clipboard=null)
    {
        this.clipboard=clipboard??new ClipboardService();
        Geometry=new(this.clipboard);
        Store=new(configPath??SettingsStore.DefaultPath);Settings=Store.Load();
        Title="公差查询工具";Width=1240;Height=870;MinWidth=1000;MinHeight=700;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=14;UseLayoutRounding=true;
        var area=SystemParameters.WorkArea;MinWidth=Math.Min(MinWidth,area.Width);MinHeight=Math.Min(MinHeight,area.Height);Width=Math.Min(Width,area.Width);Height=Math.Min(Height,area.Height);
        SetResourceReference(BackgroundProperty,"CanvasBackground");SetResourceReference(ForegroundProperty,"Ink");
        geometryContent=(UIElement)Geometry.Content;Geometry.Content=null;DataContext=Geometry.ViewModel;
        var outer=new Grid{Margin=new Thickness(16)};Content=outer;
        var viewbox=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Child=Design};outer.Children.Add(viewbox);
        Design.RowDefinitions.Add(new(){Height=new GridLength(48)});Design.RowDefinitions.Add(new(){Height=new GridLength(700)});Design.RowDefinitions.Add(new(){Height=new GridLength(22)});
        var navigation=new Grid();Design.Children.Add(navigation);
        foreach(string id in new[]{"shaft","hole","shaft-fit","hole-fit","geometry","settings"})
        {
            navigation.ColumnDefinitions.Add(new());var b=new Button{Content=PageTitle(id),Style=(Style)FindResource("NavigationButton"),Margin=new Thickness(0,0,12,8),Padding=new Thickness(12,4,12,4)};
            Grid.SetColumn(b,nav.Count);navigation.Children.Add(b);nav.Add(id,b);b.Click+=(_,_)=>Navigate(id);
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
        Closed+=(_,_)=>{SaveInputs();this.clipboard.Dispose();systemTimer.Stop();Geometry.Close();};
        ApplySettings();Navigate(Settings.StartupPage=="last"?Settings.LastPage:Settings.StartupPage);
    }
    internal static string PageTitle(string id)=>id switch{"shaft"=>"轴偏差","hole"=>"孔偏差","shaft-fit"=>"基轴制配合","hole-fit"=>"基孔制配合","settings"=>"设置",_=>"形位公差"};
    internal void Navigate(string id)
    {
        if(id!=ActivePage)clipboard.Cancel();
        ActivePage=id;Host.Content=id=="geometry"?geometryContent:id=="settings"?BuildSettings():SizePages.GetValueOrDefault(id);
        if(id!="settings"){Settings.LastPage=id;Store.Save(Settings);}
        foreach(var (key,b) in nav){b.BorderBrush=key==id?UI.Brush("Accent"):Brushes.Transparent;b.Foreground=UI.Brush(key==id?"Accent":"Secondary");}
        UpdateLayout();ApplyFonts(Design);status.Text=Store.Status;
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
        Store.Save(Settings);status.Text=Store.Status;
    }
    internal void ApplySettings()
    {
        var format=(Settings.CopyUnit,Settings.DecimalPlaces);
        if(copyFormat!=format)clipboard.Cancel();
        copyFormat=format;
        NumberDisplay.MinimumDecimals=Settings.DecimalPlaces;Topmost=Settings.Topmost;ApplyTheme();
        foreach(var p in SizePages.Values)p.Refresh();Geometry.ViewModel.RefreshFormatting();
        foreach(var (name,unit) in new[]{("CopyMm","mm"),("CopyUm","μm")})
        {
            var button=(Button)Geometry.FindName(name);
            button.SetResourceReference(BackgroundProperty,Settings.CopyUnit==unit?"PrimarySurface":"ControlSurface");
            button.SetResourceReference(ForegroundProperty,Settings.CopyUnit==unit?"PrimaryInk":"Ink");
            button.SetResourceReference(BorderBrushProperty,Settings.CopyUnit==unit?"PrimarySurface":"Hairline");
        }
        ApplyFonts(Design);SaveInputs();
    }
    private void ApplyTheme()
    {
        bool dark=Settings.Theme=="dark";
        if(Settings.Theme=="system")
        {
            try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");dark=key?.GetValue("AppsUseLightTheme") is int value&&value==0;}catch(System.Security.SecurityException){}
        }
        Appearance.Apply(dark);
        foreach(var p in SizePages.Values)p.Refresh();
        foreach(var(key,b) in nav){b.BorderBrush=key==ActivePage?UI.Brush("Accent"):Brushes.Transparent;b.Foreground=UI.Brush(key==ActivePage?"Accent":"Secondary");}
    }
    internal void ApplyFonts(DependencyObject root)
    {
        var targets=new List<(DependencyObject Node,DependencyProperty Property)>();
        void Collect(DependencyObject node)
        {
            DependencyProperty? property=node is TextBlock?TextBlock.FontSizeProperty:node is Control?Control.FontSizeProperty:null;
            if(property is not null)
            {
                var source=DependencyPropertyHelper.GetValueSource(node,property);
                // Template text inherits the control's scaled font. Scaling it again compounds size.
                if(originalFonts.ContainsKey(node)||source.BaseValueSource!=BaseValueSource.Inherited&&source.BaseValueSource!=BaseValueSource.Default)
                {
                    if(!originalFonts.ContainsKey(node))originalFonts[node]=(double)node.GetValue(property);
                    targets.Add((node,property));
                }
            }
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Collect(VisualTreeHelper.GetChild(node,i));
        }
        Collect(root);
        foreach(var (node,property) in targets)node.SetValue(property,originalFonts[node]*(Settings.LargeFont?1.12:1));
    }
    internal void Copy(string text) => clipboard.Copy(text,message=>status.Text=message);
    internal void CancelCopy(string pageId) { if(ActivePage==pageId)clipboard.Cancel(); }
    private UIElement BuildSettings()
    {
        var card=UI.Card();var root=new StackPanel();card.Child=root;
        root.Children.Add(UI.Text("设置",24,FontWeights.Medium));root.Children.Add(UI.Text("偏好自动保存到当前 Windows 用户",14,margin:new Thickness(0,4,0,20)));
        var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=new GridLength(40)});grid.ColumnDefinitions.Add(new());root.Children.Add(grid);
        var left=new StackPanel();var right=new StackPanel();grid.Children.Add(left);Grid.SetColumn(right,2);grid.Children.Add(right);
        void Select(Panel panel,string label,string[] labels,string[] values,string current,Action<string> set)
        {
            panel.Children.Add(UI.Text(label,14,FontWeights.Medium,new Thickness(0,0,0,8)));
            var wrap=new WrapPanel{Margin=new Thickness(0,0,0,24)};panel.Children.Add(wrap);
            for(int i=0;i<labels.Length;i++)
            {
                string value=values[i];var b=new Button{Content=labels[i],Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,8,8)};
                b.BorderBrush=UI.Brush(value==current?"Accent":"Hairline");b.BorderThickness=new Thickness(1);b.SetResourceReference(BackgroundProperty,value==current?"SelectionSurface":"ControlSurface");
                b.Click+=(_,_)=>{set(value);ApplySettings();Host.Content=BuildSettings();UpdateLayout();ApplyFonts(Design);};wrap.Children.Add(b);
            }
        }
        Select(left,"启动页面",["上次查询页","轴偏差","孔偏差","基轴制","基孔制","形位公差"],["last","shaft","hole","shaft-fit","hole-fit","geometry"],Settings.StartupPage,v=>Settings.StartupPage=v);
        Select(left,"记住各页面输入",["开启","关闭并清除记录"],["on","off"],Settings.RememberInputs?"on":"off",v=>{Settings.RememberInputs=v=="on";if(!Settings.RememberInputs)Settings.Inputs.Clear();});
        Select(left,"窗口置顶",["关闭","开启"],["off","on"],Settings.Topmost?"on":"off",v=>Settings.Topmost=v=="on");
        Select(left,"默认复制单位",["mm","μm"],["mm","μm"],Settings.CopyUnit,v=>Settings.CopyUnit=v);
        Select(right,"小数显示（至少保留，不截断）",["自动","3 位","4 位","6 位"],["0","3","4","6"],Settings.DecimalPlaces.ToString(),v=>Settings.DecimalPlaces=int.Parse(v));
        Select(right,"主题",["浅色","深色","跟随系统"],["light","dark","system"],Settings.Theme,v=>Settings.Theme=v);
        Select(right,"字号",["标准","大号"],["standard","large"],Settings.LargeFont?"large":"standard",v=>Settings.LargeFont=v=="large");
        var reset=new Button{Content="恢复默认设置及输入",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,12)};right.Children.Add(reset);
        reset.Click+=(_,_)=>Reset();
        right.Children.Add(UI.Text("图例与公差带保留白底绘图区。\r\n输入公称尺寸和复制极限尺寸的单位始终为 mm。",13));
        root.Children.Add(UI.Text(Store.Status,13,margin:new Thickness(0,16,0,0)));return card;
    }
    internal void Reset()
    {
        restoring=true;Settings=new();
        foreach(var(id,p) in SizePages)p.Restore(id=="shaft-fit"?new(){HoleZone="G",ShaftZone="h"}:new());
        var vm=Geometry.ViewModel;vm.SelectedType=vm.Types.Single(t=>t.Id=="flatness");vm.SelectedGradeCode="6";vm.ParameterText="100";vm.SizeToleranceText="";
        restoring=false;ApplySettings();Navigate("settings");
    }
}
