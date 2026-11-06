using System.Globalization;
using System.Text.Json;
namespace Tolerance.Core;

public sealed class PageInput
{
    public string Nominal { get; set; }="50";
    public string HoleZone { get; set; }="H";
    public int HoleGrade { get; set; }=7;
    public string ShaftZone { get; set; }="g";
    public int ShaftGrade { get; set; }=6;
    public string TypeId { get; set; }="flatness";
    public string GradeCode { get; set; }="6";
    public string Parameter { get; set; }="100";
    public string SizeTolerance { get; set; }="";
}
public sealed class ToolSettings
{
    public int Version { get; set; }=1;
    public string StartupPage { get; set; }="last";
    public string LastPage { get; set; }="geometry";
    public bool RememberInputs { get; set; }=true;
    public bool Topmost { get; set; }
    public string CopyUnit { get; set; }="mm";
    public int DecimalPlaces { get; set; }
    public string Theme { get; set; }="light";
    public bool LargeFont { get; set; }
    public Dictionary<string,PageInput> Inputs { get; set; }=[];
}
public sealed class SettingsStore(string path)
{
    public string Path { get; }=path;
    public static string DefaultPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ToleranceTool","settings.json");
    public string Status { get; private set; }="设置自动保存";
    public ToolSettings Load()
    {
        try
        {
            if(!File.Exists(Path))return new();
            var s=JsonSerializer.Deserialize<ToolSettings>(File.ReadAllText(Path)) ?? throw new InvalidDataException();
            if(s.Version!=1 || !Pages.Contains(s.LastPage) || !(Pages.Contains(s.StartupPage)||s.StartupPage=="last") || s.CopyUnit is not("mm" or "μm") || s.Theme is not("light" or "dark" or "system") || s.DecimalPlaces is not(0 or 3 or 4 or 6) || s.Inputs is null)throw new InvalidDataException();
            if(!s.RememberInputs)s.Inputs.Clear();
            return s;
        }
        catch(Exception ex) when(ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {Status="设置文件不可用，已使用默认设置";return new();}
    }
    public static readonly string[] Pages=["shaft","hole","shaft-fit","hole-fit","geometry"];
    public bool Save(ToolSettings settings)
    {
        string? temp=null;
        try
        {
            if(!settings.RememberInputs)settings.Inputs.Clear();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            temp=Path+"."+Guid.NewGuid().ToString("N")+".tmp";
            File.WriteAllText(temp,JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));
            File.Move(temp,Path,true);Status="设置已自动保存";return true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        {Status="设置保存失败；当前会话仍可使用";return false;}
        finally {if(temp is not null && File.Exists(temp))try{File.Delete(temp);}catch(IOException){}}
    }
}
public static class NumberDisplay
{
    public static int MinimumDecimals { get; set; }
    public static string Format(decimal value,int? places=null)
    {
        var text=value.ToString("0.############################",CultureInfo.InvariantCulture);
        int count=places??MinimumDecimals;
        if(count<=0)return text;
        int index=text.IndexOf('.');
        if(index<0)return text+"."+new string('0',count);
        return text+new string('0',Math.Max(0,count-(text.Length-index-1)));
    }
    public static string Signed(decimal value)=>value>0?"+"+Format(value):Format(value);
}
