namespace Tolerance.Core;

public enum SelectionRank { Other, Secondary, Preferred }
public sealed record BandChoice(string Zone,int Grade,SelectionRank Rank)
{
    public string Band=>Zone+Grade;
}
public static class BandSelection
{
    // Explicitly collected band-selection diagrams; ranking applies up to 500 mm.
    private static readonly string[] HoleRows=["H JS","H JS","H JS","H JS K M","G H JS K M N P R S","F G H J JS K M N P R S T U V X Y Z","D E F G H J JS K M N P R S T U V X Y Z","C D E F G H J JS K M N P R S T U V X Y Z","A B C D E F H JS N P","A B C D E H JS","A B C D H JS","A B C H JS","H JS"];
    private static readonly string[] ShaftRows=["h js","h js","h js","g h js k m n p r s","f g h j js k m n p r s t u v x","e f g h j js k m n p r s t u v x y z","d e f g h j js k m n p r s t u v x y z","c d e f g h js k m n p r s t u v x y z","a b c d e f h js","a b c d e h js","a b c d h js","a b c h js","a b h js"];
    private static readonly HashSet<string> HoleFramed=Split("F6 G6 H6 JS6 K6 M6 N6 P6 R6 S6 T6 F7 G7 H7 JS7 K7 M7 N7 P7 R7 S7 T7 U7 D8 E8 F8 H8 JS8 K8 M8 N8 D9 E9 F9 H9 D10 H10 A11 B11 C11 D11 H11 B12 H12");
    private static readonly HashSet<string> ShaftFramed=Split("f5 g5 h5 js5 k5 m5 n5 p5 r5 s5 t5 f6 g6 h6 js6 k6 m6 n6 p6 r6 s6 t6 u6 v6 x6 y6 z6 e7 f7 g7 h7 js7 k7 m7 n7 p7 r7 s7 t7 u7 d8 e8 f8 h8 c9 d9 e9 f9 h9 c10 d10 h10 a11 b11 c11 d11 h11 b12 h12");
    private static readonly HashSet<string> HolePreferred=Split("G7 H7 K7 N7 P7 S7 U7 F8 H8 D9 H9 C11 H11");
    private static readonly HashSet<string> ShaftPreferred=Split("g6 h6 k6 n6 p6 s6 u6 f7 h7 d9 h9 c11 h11");
    private static readonly HashSet<string> HoleLarge=Split("G6 H6 JS6 K6 M6 N6 F7 G7 H7 JS7 K7 M7 N7 D8 E8 F8 H8 JS8 D9 E9 F9 H9 JS9 D10 H10 JS10 D11 H11 JS11 H12 JS12");
    private static readonly HashSet<string> ShaftLarge=Split("g6 h6 js6 k6 m6 n6 p6 r6 s6 t6 u6 f7 g7 h7 js7 k7 m7 n7 p7 r7 s7 t7 u7 d8 e8 f8 h8 js8 d9 e9 f9 h9 js9 d10 h10 js10 d11 h11 js11 h12 js12");
    private static HashSet<string> Split(string value)=>value.Split(' ').ToHashSet(StringComparer.Ordinal);
    private static readonly IReadOnlyList<BandChoice> Holes=Build(HoleRows,HoleFramed,HolePreferred);
    private static readonly IReadOnlyList<BandChoice> Shafts=Build(ShaftRows,ShaftFramed,ShaftPreferred);
    private static IReadOnlyList<BandChoice> Build(string[] rows,HashSet<string> framed,HashSet<string> preferred)=>Array.AsReadOnly(rows.SelectMany((zones,index)=>zones.Split(' ').Select(z=>new BandChoice(z,index+1,preferred.Contains(z+(index+1))?SelectionRank.Preferred:framed.Contains(z+(index+1))?SelectionRank.Secondary:SelectionRank.Other))).ToArray());
    public static IReadOnlyList<BandChoice> For(FeatureKind kind)=>kind==FeatureKind.Hole?Holes:Shafts;
    public static bool RecommendedAt(FeatureKind kind,decimal size,BandChoice band)=>size>0&&size<=3150&&For(kind).Any(b=>b.Band==band.Band)&&(size<=500||(kind==FeatureKind.Hole?HoleLarge:ShaftLarge).Contains(band.Band));
    public static SelectionRank RankAt(decimal size,BandChoice band)=>size<=500?band.Rank:SelectionRank.Secondary;
}
