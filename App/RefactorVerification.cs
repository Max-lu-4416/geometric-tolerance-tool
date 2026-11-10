using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Tolerance.Core;

namespace Tolerance.App;

internal static class RefactorVerification
{
    private sealed record CopyFixture(string PageId,PageInput Input,int Decimals,string Unit,string Expected);

    internal static void Run(ShellWindow window,string directory,Action<bool,string> check)
    {
        using(var stream=typeof(RefactorVerification).Assembly.GetManifestResourceStream("Tolerance.App.CopyBaseline.json")!)
        {
            var fixtures=JsonSerializer.Deserialize<CopyFixture[]>(stream)!;
            check(fixtures.Length==88,"All independently captured v1.3 copy baselines");
            foreach(var fixture in fixtures)
            {
                window.Settings.CopyUnit=fixture.Unit;window.Settings.DecimalPlaces=fixture.Decimals;window.ApplySettings();
                var page=window.SizePages[fixture.PageId];page.Restore(fixture.Input);
                var expected=fixture.Expected.Replace($"偏差复制单位：{fixture.Unit}；尺寸始终为 mm。",CopyText.WithTitle);
                if(!page.HasResult||page.FullText!=expected)
                    File.WriteAllText(Path.Combine(directory,"copy-mismatch.txt"),$"EXPECTED:\n{fixture.Expected}\nACTUAL:\n{page.FullText}\nERROR: {page.Error}");
                check(page.HasResult&&page.FullText==expected,$"Copy compatibility {fixture.PageId}/{fixture.Decimals}/{fixture.Unit}/{fixture.Input.HoleZone}/{fixture.Input.ShaftZone}");
                page.Input.Nominal="invalid";page.Refresh();
                check(page.FullText=="","Invalid query has no copy text");
            }
        }
        window.Reset();

        bool busy=false,scheduled=false;int calls=0;string? copied=null,status=null;
        using var clipboard=new ClipboardService(text=>{calls++;if(busy)throw new ExternalException("busy");copied=text;},active=>scheduled=active);
        clipboard.Copy("now",message=>status=message);
        check(calls==1&&copied=="now"&&!clipboard.HasPending&&!scheduled&&status!.StartsWith("已复制"),"Immediate copy success");
        busy=true;copied=null;status=null;calls=0;
        clipboard.Copy("later",message=>status=message);
        check(calls==1&&clipboard.HasPending&&scheduled&&copied is null,"Busy clipboard schedules retry");
        clipboard.RetryPending();busy=false;clipboard.RetryPending();
        check(calls==3&&copied=="later"&&!clipboard.HasPending&&!scheduled,"Retry succeeds and stops");
        busy=true;calls=0;status=null;clipboard.Copy("exhaust",message=>status=message);
        for(int i=0;i<10;i++)clipboard.RetryPending();
        check(calls==6&&!scheduled&&!clipboard.HasPending&&status!.Contains("正被占用"),"Exactly six bounded attempts");
        clipboard.Copy("old",_=>throw new InvalidOperationException("Obsolete completion"));
        clipboard.Copy("new",message=>status=message);busy=false;clipboard.RetryPending();
        check(copied=="new"&&!clipboard.HasPending,"New copy replaces pending copy");
        busy=true;clipboard.Copy("cancel",_=>throw new InvalidOperationException("Cancelled completion"));clipboard.Cancel();
        calls=0;busy=false;clipboard.RetryPending();check(calls==0&&!scheduled,"Cancel drops payload and callback");
        busy=true;status=null;clipboard.Copy("+0.039",message=>status=message,"已复制孔上偏差：+0.039 mm");
        busy=false;clipboard.RetryPending();check(copied=="+0.039"&&status=="已复制孔上偏差：+0.039 mm","Context message survives clipboard retry");

        var testPath=Path.Combine(directory,"clipboard-test.json");
        var shell=new ShellWindow(testPath,clipboard);
        shell.Navigate("shaft");var shaft=shell.SizePages["shaft"];
        void Pending(){busy=true;copied=null;shell.Copy("obsolete");check(clipboard.HasPending,"Prepare pending query copy");}
        void Cancelled(string reason){busy=false;clipboard.RetryPending();check(!clipboard.HasPending&&copied is null,reason);}
        Pending();shaft.Input.Nominal="51";shaft.Refresh();Cancelled("Valid size change cancels old copy");
        Pending();shaft.Input.Nominal="invalid";shaft.Refresh();Cancelled("Invalid size cancels old copy");
        shaft.Input.Nominal="50";shaft.Refresh();Pending();shaft.Input.ShaftGrade=7;shaft.Refresh();Cancelled("Grade change cancels old copy");
        Pending();shaft.Input.ShaftZone="h";shaft.Refresh();Cancelled("Zone change cancels old copy");
        Pending();shell.Navigate("hole");Cancelled("Page change cancels old copy");
        Pending();shell.Settings.CopyUnit="μm";shell.ApplySettings();Cancelled("Copy unit change cancels old copy");
        Pending();shell.Settings.DecimalPlaces=4;shell.ApplySettings();Cancelled("Decimal setting change cancels old copy");
        Pending();shell.Settings.Theme="dark";shell.ApplySettings();
        check(clipboard.HasPending,"Appearance-only refresh preserves valid pending copy");
        busy=false;clipboard.RetryPending();check(copied=="obsolete","Unchanged result may finish copying");
        shell.Navigate("geometry");Pending();shell.Geometry.ViewModel.ParameterText="10.1";Cancelled("Geometry condition change cancels pending copy");
        Pending();shell.Close();Cancelled("Closing window drops pending copy");
        window.Reset();
    }
}
