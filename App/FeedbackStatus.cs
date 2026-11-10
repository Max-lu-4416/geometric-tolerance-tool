namespace Tolerance.App;

// Successful background saves are silent and cannot overwrite user feedback.
internal sealed class FeedbackStatus
{
    private string startupWarning="",saveWarning="";
    internal string Feedback { get; private set; }="";
    internal string Text=>string.Join(" · ",new[]{startupWarning,saveWarning,Feedback}.Where(s=>s.Length>0).Distinct());
    internal void Loaded(string status)
    {
        startupWarning=status is "设置自动保存" or "设置已自动保存"?"":status;
    }
    internal void Saved(bool success,string status)=>saveWarning=success?"":status;
    internal void Report(string message)=>Feedback=message;
}
