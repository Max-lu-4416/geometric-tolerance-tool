using System.Globalization;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using Tolerance.Core;
internal static class SizeTests
{
    internal static void Run(Action<bool,string> check)
    {
        var catalog=new SizeToleranceCatalog();
        check(catalog.Cells.Count==7283,"All collected size cells embedded");
        check(catalog.Zones(FeatureKind.Hole).Count()==28&&catalog.Zones(FeatureKind.Shaft).Count()==28,"All 56 zones");
        using(var csv=new TextFieldParser(Path.Combine(AppContext.BaseDirectory,"size-source-fixture.csv")))
        {
            csv.SetDelimiters(",");csv.HasFieldsEnclosedInQuotes=true;csv.ReadFields();int cells=0;
            while(!csv.EndOfData)
            {
                var f=csv.ReadFields()!;string zone=f[1];int grade=int.Parse(f[2]);decimal lo=decimal.Parse(f[3],CultureInfo.InvariantCulture),hi=decimal.Parse(f[4],CultureInfo.InvariantCulture);
                var kind=char.IsUpper(zone[0])?FeatureKind.Hole:FeatureKind.Shaft;
                var lines=f[5].Split('\n',StringSplitOptions.TrimEntries);bool symmetric=f[5].StartsWith('±');
                decimal rawUpper=decimal.Parse(symmetric?f[5][1..]:lines[0],CultureInfo.InvariantCulture);
                decimal rawLower=symmetric?-rawUpper:decimal.Parse(lines[1],CultureInfo.InvariantCulture);
                decimal multiplier=zone is "H" or "h" or "JS" or "js" && grade>=12?1000m:1m;
                foreach(decimal d in new[]{hi,(lo+hi)/2})
                {
                    var r=catalog.LookupDeviation(kind,d,zone,grade);
                    check(r.Cell.LowerMm==lo&&r.Cell.UpperMm==hi,"Size partition");
                    check(r.UpperUm==decimal.Parse(f[6],CultureInfo.InvariantCulture)&&r.LowerUm==decimal.Parse(f[7],CultureInfo.InvariantCulture),"Source cell deviations");
                    check(r.UpperUm==rawUpper*multiplier&&r.LowerUm==rawLower*multiplier,"Raw source unit and sign audit");
                    check(r.MaximumMm-r.MinimumMm==r.ToleranceUm/1000m,"Units and limit sizes");
                }
                decimal next=hi+.000001m;
                var expected=catalog.Cells.SingleOrDefault(c=>c.Zone==zone&&c.Grade==grade.ToString()&&c.LowerMm<next&&next<=c.UpperMm);
                try{var r=catalog.LookupDeviation(kind,next,zone,grade);check(expected is not null&&r.Cell==expected,"Upper epsilon enters next partition");}
                catch(ArgumentException){check(expected is null,"No extrapolation");}
                cells++;
            }
            check(cells==catalog.Cells.Count,"Fixture complete");
        }
        var fit=catalog.CalculateFit(BasisSystem.Hole,50,"H",7,"g",6);
        check(fit.MinimumDifferenceUm==9&&fit.MaximumDifferenceUm==50&&fit.Nature=="间隙配合","50H7/g6 = .009..050mm");
        var basicShaft=catalog.CalculateFit(BasisSystem.Shaft,50,"G",7,"h",6);
        check(basicShaft.MinimumDifferenceUm==9&&basicShaft.MaximumDifferenceUm==50,"G7/h6");
        check(catalog.CalculateFit(BasisSystem.Hole,50,"H",7,"h",6).MinimumDifferenceUm==0,"Zero clearance");
        check(catalog.CalculateFit(BasisSystem.Hole,50,"H",7,"k",6).Nature=="过渡配合","Transition");
        check(catalog.CalculateFit(BasisSystem.Hole,50,"H",7,"s",6).Nature=="过盈配合","Interference");
        void Cross(BasisSystem basis,string hz,int hg,string sz,int sg,decimal min,decimal max)
        {var r=catalog.CalculateFit(basis,50,hz,hg,sz,sg);check(r.MinimumDifferenceUm==min&&r.MaximumDifferenceUm==max,$"Independent fit-result cell {hz}{hg}/{sz}{sg}");}
        foreach(var x in new[]{("f",5,6,25m,52m),("g",5,6,9m,36m),("h",5,6,0m,27m),("f",6,7,25m,66m),("g",6,7,9m,50m),("h",6,7,0m,41m),("e",7,8,50m,114m),("f",7,8,25m,89m),("g",7,8,9m,73m),("h",7,8,0m,64m),("d",8,8,80m,158m),("e",8,8,50m,128m),("f",8,8,25m,103m),("h",8,8,0m,78m),("c",9,9,130m,254m),("d",9,9,80m,204m)})
            Cross(BasisSystem.Hole,"H",x.Item3,x.Item1,x.Item2,x.Item4,x.Item5);
        Cross(BasisSystem.Hole,"H",7,"k",6,-18,23);Cross(BasisSystem.Shaft,"K",7,"h",6,-18,23);Cross(BasisSystem.Shaft,"JS",7,"h",6,-12,28);
        Cross(BasisSystem.Hole,"H",7,"js",6,-8,33);Cross(BasisSystem.Hole,"H",6,"p",5,-37,-10);
        Cross(BasisSystem.Hole,"H",6,"s",5,-54,-27);Cross(BasisSystem.Hole,"H",7,"p",6,-42,-1);
        Cross(BasisSystem.Hole,"H",8,"p",7,-51,13);
        foreach(var d in new[]{1m,3,6,10,18,30,50,500,3150})
        {
            check(catalog.LookupDeviation(FeatureKind.Hole,d,"H",7).MinimumMm==d,"H datum at boundaries");
            check(catalog.LookupDeviation(FeatureKind.Shaft,d,"h",6).MaximumMm==d,"h datum at boundaries");
        }
        check(!catalog.Available(FeatureKind.Hole,1,"H",14),"No IT14 <=1");
        check(!catalog.Available(FeatureKind.Hole,.999m,"A",9)&&catalog.Available(FeatureKind.Hole,1,"A",9),"A inclusive minimum");
        check(catalog.LookupDeviation(FeatureKind.Hole,1,"A",9).Interval.StartsWith("1 ≤"),"Effective A interval includes 1");
        check(catalog.LookupDeviation(FeatureKind.Hole,2,"H",14).Interval.StartsWith("1 <"),"Effective IT14 interval excludes 1");
        check(catalog.LookupDeviation(FeatureKind.Shaft,501,"h",1).Cell.Trial,"Trial flags");
        check(catalog.Cells.Count(c=>c.WidthWarning)==63,"Source width exceptions retained and flagged");
        var standard=JsonSerializer.Deserialize<string[][]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"standard-width-fixture.json")))!.Select(row=>row.Select(v=>decimal.Parse(v,CultureInfo.InvariantCulture)).ToArray()).ToArray();
        foreach(var c in catalog.Cells)
        {
            var row=standard.Single(r=>c.UpperMm>r[0]&&c.UpperMm<=r[1]);int g=int.Parse(c.Grade);
            var width=row[g+1]*(g>=12?1000m:1m);
            check((c.UpperUm-c.LowerUm!=width)==c.WidthWarning,"Independent standard-width audit");
            check(c.Trial==(g<=5&&c.LowerMm>=500),"All trial conditions");
        }
        foreach(var b in Enum.GetValues<BasisSystem>())foreach(var preset in CommonFits.For(b))
        {check(catalog.CalculateFit(b,50,preset.HoleZone,preset.HoleGrade,preset.ShaftZone,preset.ShaftGrade).Hole.NominalMm==50,"Checked preset at default size");}
        check(NumberDisplay.Format(.00012345m,3)=="0.00012345","No truncation");
        check(NumberDisplay.Format(.01m,6)=="0.010000","Pad zeros");
        NumberDisplay.MinimumDecimals=6;var geometry=new ToleranceCatalog();
        check(geometry.TryQuery("flatness",6,"100",out var gResult,out _)&&gResult!.Micrometers==10,"Display precision does not alter geometric input validation");
        var general=new GeneralToleranceCatalog(geometry);
        check(general.TryQuery("flatness","K","100","",out var hkl,out _)&&hkl is not null,"Display precision does not alter H/K/L input validation");NumberDisplay.MinimumDecimals=0;
        var dir=Path.Combine(AppContext.BaseDirectory,"ToleranceTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try
        {
            var path=Path.Combine(dir,"settings.json");var store=new SettingsStore(path);check(store.Load().LastPage=="geometry","First startup");
            var s=new ToolSettings{Theme="dark",LargeFont=true,DecimalPlaces=6,LastPage="shaft",CopyUnit="μm"};s.Inputs["shaft"]=new(){Nominal="123.456"};
            check(store.Save(s),"Atomic save");var saved=store.Load();check(saved.Theme=="dark"&&saved.LargeFont&&saved.DecimalPlaces==6&&saved.Inputs["shaft"].Nominal=="123.456","Restart persistence");
            s.RememberInputs=false;store.Save(s);check(store.Load().Inputs.Count==0,"Clear inputs");
            File.WriteAllText(path,"broken{");check(store.Load().Theme=="light","Corrupt settings fallback");
            File.WriteAllText(path,"{\"Version\":999}");check(store.Load().Version==1,"Unknown version fallback");
            check(!Directory.GetFiles(dir,"*.tmp").Any(),"No partial JSON left");
        }
        finally {Directory.Delete(dir,true);}
    }
}
