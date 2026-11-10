using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Tolerance.App;

internal static class AboutInfo
{
    internal const string Product="公差查询工具",Author="Max-lu-4416";
    internal const string Repository="https://github.com/Max-lu-4416/geometric-tolerance-tool";
    internal const string DesignReference="https://github.com/heydamir/unslop";
    internal const string UiReference="https://github.com/nextlevelbuilder/ui-ux-pro-max-skill";
    internal static string Version=>typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ??typeof(App).Assembly.GetName().Version?.ToString(3)??"未知版本";
}

internal sealed partial class ShellWindow
{
    private Border BuildAbout()
    {
        var border=new Border{BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(0,12,0,0),Margin=new Thickness(0,8,0,0)};
        border.SetResourceReference(Border.BorderBrushProperty,"Hairline");
        var content=new StackPanel();border.Child=content;
        content.Children.Add(UI.Text("关于",16,FontWeights.Medium,new Thickness(0,0,0,8)));
        content.Children.Add(UI.Text($"{AboutInfo.Product} · v{AboutInfo.Version}",14,FontWeights.Medium,new Thickness(0,0,0,4)));
        void Detail(string text)
        {
            var label=UI.Text(text,12,margin:new Thickness(0,0,0,4));label.SetResourceReference(TextBlock.ForegroundProperty,"Secondary");content.Children.Add(label);
        }
        Detail("作者："+AboutInfo.Author);
        TextBlock Link(string prefix,string title,string url)
        {
            var label=UI.Text("",12,margin:new Thickness(0,0,0,4));label.SetResourceReference(TextBlock.ForegroundProperty,"Secondary");
            label.Inlines.Add(new Run(prefix));
            var link=new Hyperlink(new Run(title)){NavigateUri=new Uri(url),ToolTip=url};link.SetResourceReference(TextElement.ForegroundProperty,"Accent");
            link.RequestNavigate+=(_,e)=>
            {
                // Opening a browser is exclusively a user-initiated link action.
                try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
                catch(Exception error) when(error is Win32Exception or InvalidOperationException){ReportFeedback("无法打开浏览器，请在浏览器中访问："+url);}
                e.Handled=true;
            };
            label.Inlines.Add(link);return label;
        }
        content.Children.Add(Link("项目仓库：","Max-lu-4416 / geometric-tolerance-tool",AboutInfo.Repository));
        Detail("自有代码采用 MIT 许可；.NET/WPF 第三方许可随程序提供");
        content.Children.Add(Link("设计参考：","unslop",AboutInfo.DesignReference));
        Detail("Damir Sokolovsky · CC BY 4.0");
        content.Children.Add(Link("UI/UX 指南：","UI UX Pro Max · MIT",AboutInfo.UiReference));
        return border;
    }
}
