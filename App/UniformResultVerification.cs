using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Tolerance.Core;

namespace Tolerance.App;

internal static class UniformResultVerification
{
    internal static void Run(ShellWindow window,string directory,Action<bool,string> check)
    {
        foreach(var sample in new[]{
            (Page:"shaft",Zone:"g",Grade:6,Upper:-.009m,Lower:-.025m),
            (Page:"shaft",Zone:"n",Grade:6,Upper:.033m,Lower:.017m),
            (Page:"shaft",Zone:"h",Grade:7,Upper:0m,Lower:-.025m),
            (Page:"hole",Zone:"H",Grade:7,Upper:.025m,Lower:0m),
            (Page:"hole",Zone:"E",Grade:8,Upper:.089m,Lower:.050m)})
        {
            var page=window.SizePages[sample.Page];window.Navigate(sample.Page);
            var input=new PageInput();if(sample.Page=="hole"){input.HoleZone=sample.Zone;input.HoleGrade=sample.Grade;}else{input.ShaftZone=sample.Zone;input.ShaftGrade=sample.Grade;}
            page.Restore(input);window.UpdateLayout();
            check(page.HasResult&&page.FitRangeNumbers.Select(t=>decimal.Parse(t.Text,CultureInfo.InvariantCulture)).SequenceEqual(new[]{sample.Upper,sample.Lower}),"Single-element headline: "+sample.Zone+sample.Grade);
            check(page.FitRangeLabels.SequenceEqual(new[]{"上偏差 / mm","下偏差 / mm"}),"Single headline unit and meanings");
            foreach(var column in new[]{("上偏差",sample.Upper),("下偏差",sample.Lower)})
            {
                var name=(sample.Page=="hole"?"孔":"轴")+column.Item1;
                var mm=page.NumericCopies.Single(c=>c.Value.Name==name&&c.Value.Unit=="mm");
                var um=page.NumericCopies.Single(c=>c.Value.Name==name&&c.Value.Unit=="μm");
                check(decimal.Parse(mm.Value.Text,CultureInfo.InvariantCulture)==column.Item2&&decimal.Parse(um.Value.Text,CultureInfo.InvariantCulture)==column.Item2*1000m,"Dual-unit rows retain exact conversion");
                check(column.Item2<=0||mm.Value.Text.StartsWith('+'),"Positive deviations keep explicit sign");
            }
            check(page.RowDefinitions[3].Height.Value==312&&page.RowDefinitions[5].Height.Value==276,"Shared matrix and result area heights");
            check(page.NumericCopies.Count(c=>c.Value.CanCopy)==10,"Two headline values and eight table values clickable");
            var matrix=page.Children.OfType<Grid>().Single(g=>Grid.GetRow(g)==3&&g.ColumnDefinitions.Count>3);
            check(matrix.RowDefinitions.Skip(1).All(r=>r.Height.Value==22),"Single-band rows fit compact matrix");
            FitPanelVerification.RenderResult(page,Path.Combine(directory,$"result-panel-{sample.Page}-{sample.Zone}{sample.Grade}.png"));
            page.Input.Nominal="0";page.Refresh();
            check(page.NumericCopies.All(c=>!c.Value.CanCopy)&&page.FitRangeNumbers.All(t=>t.Text=="—"),"Invalid single query clears all headline and table copies");
            check(ShellVerification.Descendants<TextBlock>(page).Any(t=>t.Text==page.Error),"Invalid single query shows reason");
        }
        window.Navigate("settings");window.UpdateLayout();CheckAbout(window,check);
        window.Reset();window.Navigate("geometry");
    }

    internal static void CheckAbout(ShellWindow window,Action<bool,string> check)
    {
        var labels=ShellVerification.Descendants<TextBlock>(window.Design).Where(t=>t.IsVisible).ToArray();
        var version=typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        check(labels.Any(t=>t.Text==$"公差查询工具 · v{version}"),"About shows the actual assembly version");
        check(labels.Any(t=>t.Text=="关于")&&labels.Any(t=>t.Text=="作者：Max-lu-4416"),"About directly visible in settings");
        check(labels.Any(t=>t.Text.Contains("MIT")&&t.Text.Contains(".NET/WPF")),"Own and runtime license notes present");
        check(labels.Any(t=>t.Text.Contains("Damir Sokolovsky")&&t.Text.Contains("CC BY 4.0")),"Design attribution retained");
        var links=labels.SelectMany(t=>t.Inlines.OfType<Hyperlink>()).ToArray();
        check(links.Any(l=>l.NavigateUri?.AbsoluteUri==AboutInfo.Repository)&&links.Any(l=>l.NavigateUri?.AbsoluteUri==AboutInfo.DesignReference),"About links point to the expected repositories without opening them");
    }
}
