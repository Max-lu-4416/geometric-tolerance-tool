using Tolerance.Core;

internal static class RefactorTests
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(var (text,expected) in new (string,decimal)[]{(".5",.5m),("1.",1m),(" 00050.0100 ",50.01m),("0.0000000000000000000000000001",.0000000000000000000000000001m),("50.12345678901234567890123456",50.12345678901234567890123456m)})
            foreach(bool signed in new[]{false,true})
                check(ExactDecimal.TryParse(text,signed,out var value,out var error)&&value==expected&&error==DecimalParseError.None,"Exact decimal input: "+text);
        foreach(var text in new[]{"", " ","1,000","1e2","NaN","Infinity","1.2.3","+", "--1","++1","1 2","1\n2","99999999999999999999999999999999"})
            check(!ExactDecimal.TryParse(text,true,out _,out var error)&&error==DecimalParseError.InvalidFormat,"Reject malformed decimal: "+text);
        foreach(var text in new[]{"10.00000000000000000000000000001","50.12345678901234567890123456789","0.00000000000000000000000000001"})
            check(!ExactDecimal.TryParse(text,true,out _,out var error)&&error==DecimalParseError.PrecisionExceeded,"Reject silent decimal rounding: "+text);
        foreach(var text in new[]{"+1","-1","+.5","-0"})
            check(!ExactDecimal.TryParse(text,false,out _,out _),"Unsigned nominal rejects signs: "+text);
        check(ExactDecimal.TryParse("+001.50",true,out var plus,out _)&&plus==1.5m,"Geometry allows leading plus");
        check(ExactDecimal.TryParse("-1.5",true,out var minus,out _)&&minus==-1.5m,"Parser leaves range validation to caller");
        var catalog=new ToleranceCatalog();var general=new GeneralToleranceCatalog(catalog);
        foreach(var text in new[]{"+.5","00050.00","50."," .5 "})
        {
            check(catalog.TryQuery("flatness",6,text,out _,out _),"Numeric geometry format compatibility");
            check(general.TryQuery("flatness","H",text,"",out _,out _),"HKL format compatibility");
        }
        foreach(int places in new[]{0,3,4,6})
        {
            NumberDisplay.MinimumDecimals=places;
            check(ExactDecimal.TryParse("10.000",false,out var d,out _)&&d==10m,"Parser independent of display precision");
        }
        NumberDisplay.MinimumDecimals=0;
        for(int grade=1;grade<=18;grade++)
        {
            var advice=SizeToleranceCatalog.GuidanceFor(grade);
            check(advice.Summary.Length>0&&advice.Detail.StartsWith(grade<=4?"精密量规":grade<=6?"精密配合":grade<=8?"一般机械":grade<=10?"要求较宽松":grade<=12?"粗配合":"尺寸精度要求较低"),"Guidance grade group "+grade);
            check(SizeToleranceCatalog.GradeAdvice(grade)==advice.Detail&&advice.Detail.EndsWith("等级仅决定公差宽度，不能单独决定配合性质。"),"Compatible grade advice "+grade);
        }
    }
}
