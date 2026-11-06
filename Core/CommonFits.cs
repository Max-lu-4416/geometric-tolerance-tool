namespace Tolerance.Core;
public sealed record FitPreset(string HoleZone,int HoleGrade,string ShaftZone,int ShaftGrade,string Use,bool IsPreferred=false)
{
    public string Label=>$"{HoleZone}{HoleGrade}/{ShaftZone}{ShaftGrade}  ·  {Use}";
}
public static class CommonFits
{
    // Explicitly collected common combinations; applicable nominal sizes <=500 mm.
    public static IReadOnlyList<FitPreset> For(BasisSystem basis)
    {
        List<FitPreset> list=[];
        void Add(int h,int s,string zones)
        {
            foreach(var z in zones.Split(' '))
            {
                var lower=z.ToLowerInvariant();
                string use=lower switch { "a" or "b" or "c" or "d"=>"大间隙、宽松装配", "e" or "f"=>"一般转动或滑动", "g"=>"精密滑动、较小间隙", "h"=>"定位与可拆装连接", "js" or "k" or "m" or "n"=>"定位连接，核对实际配合范围", _=>"紧固连接，核对过盈与装配工艺" };
                list.Add(basis==BasisSystem.Hole?new("H",h,lower,s,use):new(z.ToUpperInvariant(),h,"h",s,use));
            }
        }
        if(basis==BasisSystem.Hole)
        {
            Add(6,5,"f g h js k m n p r s t");Add(7,6,"f g h js k m n p r s t u v x y z");
            Add(8,7,"e f g h js k m n p r s t u");Add(8,8,"d e f h");Add(9,9,"c d e f h");
            Add(10,10,"c d h");Add(11,11,"a b c d h");Add(12,12,"b h");
        }
        else
        {
            Add(6,5,"f g h js k m n p r s t");Add(7,6,"f g h js k m n p r s t u");Add(8,7,"e f h js k m n");
            Add(8,8,"d e f h");Add(9,9,"d e f h");Add(10,10,"d h");Add(11,11,"a b c d h");Add(12,12,"b h");
        }
        string[] preferred=basis==BasisSystem.Hole
            ? ["H7/g6","H7/h6","H7/k6","H7/n6","H7/p6","H7/s6","H7/u6","H8/f7","H8/h7","H9/d9","H9/h9","H11/c11","H11/h11"]
            : ["G7/h6","H7/h6","K7/h6","N7/h6","P7/h6","S7/h6","U7/h6","F8/h7","H8/h7","D9/h9","H9/h9","C11/h11","H11/h11"];
        return list.Select(p=>p with {IsPreferred=preferred.Contains($"{p.HoleZone}{p.HoleGrade}/{p.ShaftZone}{p.ShaftGrade}")}).ToArray();
    }
}
