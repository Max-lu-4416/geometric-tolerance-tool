using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace Tolerance.App;

/// <summary>One pending copy, with bounded retries on the owning dispatcher.</summary>
internal sealed class ClipboardService : IDisposable
{
    private readonly Action<string> write;
    private readonly Action<bool> schedule;
    private readonly DispatcherTimer? timer;
    private string? pending;
    private Action<string>? report;
    private string successMessage="已复制到剪贴板。";
    private int attempts;
    private bool disposed;
    internal bool HasPending => pending is not null;

    internal ClipboardService(Action<string>? write = null, Action<bool>? schedule = null)
    {
        this.write = write ?? Clipboard.SetText;
        if (schedule is null)
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += OnTick;
            this.schedule = active => { if (active) timer.Start(); else timer.Stop(); };
        }
        else this.schedule = schedule;
    }

    internal void Copy(string text, Action<string> report, string? successMessage=null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Cancel();
        pending = text;
        this.report = report;
        this.successMessage=successMessage??"已复制到剪贴板。";
        RetryPending();
    }

    private void OnTick(object? sender, EventArgs e) => RetryPending();

    internal void RetryPending()
    {
        if (pending is null || disposed) return;
        try
        {
            write(pending);
            var completed = report;
            var message=successMessage;
            Cancel();
            completed?.Invoke(message);
        }
        catch (ExternalException)
        {
            if (++attempts < 6) schedule(true);
            else
            {
                var failed = report;
                Cancel();
                failed?.Invoke("剪贴板正被占用，请稍后再次点击复制。");
            }
        }
    }

    internal void Cancel()
    {
        schedule(false);
        pending = null;
        report = null;
        attempts = 0;
    }

    public void Dispose()
    {
        if (disposed) return;
        Cancel();
        if (timer is not null) timer.Tick -= OnTick;
        disposed = true;
    }
}
