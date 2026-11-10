using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Tolerance.App;

// The content stays in DIPs; only monitor and non-client metrics cross the pixel boundary.
internal sealed class ContentWindowSizing
{
    private readonly Window window;
    private readonly Size content;
    private readonly Thickness padding;
    private readonly double minimumWidth,minimumHeight;
    private HwndSource? source;
    private nint monitor;
    private bool queued,closed;

    internal ContentWindowSizing(Window window,Size content,Thickness padding)
    {
        this.window=window;this.content=content;this.padding=padding;
        minimumWidth=window.MinWidth;minimumHeight=window.MinHeight;
        window.WindowStartupLocation=WindowStartupLocation.CenterScreen;
        // Safe provisional limits until Windows supplies this window's actual frame and DPI.
        var work=SystemParameters.WorkArea;
        SetLimits(Limit(content,padding,new Size(2*SystemParameters.ResizeFrameVerticalBorderWidth,
            SystemParameters.CaptionHeight+2*SystemParameters.ResizeFrameHorizontalBorderHeight),work.Size),true);
        window.SourceInitialized+=Initialize;
        window.DpiChanged+=DpiChanged;
        window.Closed+=Closed;
    }
    internal static Size Limit(Size content,Thickness padding,Size frame,Size work)
    {
        double surroundWidth=padding.Left+padding.Right+frame.Width;
        double surroundHeight=padding.Top+padding.Bottom+frame.Height;
        double scale=Math.Clamp(Math.Min((work.Width-surroundWidth)/content.Width,
            (work.Height-surroundHeight)/content.Height),0,1);
        return new(Math.Min(content.Width*scale+surroundWidth,work.Width),
            Math.Min(content.Height*scale+surroundHeight,work.Height));
    }

    private void Initialize(object? sender,EventArgs e)
    {
        source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        source?.AddHook(Messages);
        Update(true);
    }
    private void DpiChanged(object sender,DpiChangedEventArgs e)=>QueueUpdate();
    private void QueueUpdate()
    {
        if(queued||closed)return;
        queued=true;
        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
        {
            queued=false;if(!closed)Update(false);
        }));
    }
    private nint Messages(nint hwnd,int message,nint wParam,nint lParam,ref bool handled)
    {
        // WPF retains its native maximize/restore and caption double-click handling.
        if(message is 0x02E0 or 0x007E or 0x001A)QueueUpdate();
        else if(message==0x0047&&(MonitorFromWindow(hwnd,2)!=monitor||window.Width>window.MaxWidth||window.Height>window.MaxHeight))QueueUpdate();
        return 0;
    }
    private void Update(bool initial)
    {
        var hwnd=new WindowInteropHelper(window).Handle;
        monitor=MonitorFromWindow(hwnd,2);
        var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
        if(!GetMonitorInfo(monitor,ref info))return;
        uint dpi=GetDpiForWindow(hwnd);double scale=(dpi==0?96:dpi)/96d;
        uint style=unchecked((uint)GetWindowLongPtr(hwnd,-16).ToInt64())&~0x21000000u;
        uint extended=unchecked((uint)GetWindowLongPtr(hwnd,-20).ToInt64());
        var frame=new NativeRect();
        if(!AdjustWindowRectExForDpi(ref frame,style,false,extended,dpi==0?96:dpi))return;
        var limits=Limit(content,padding,new Size((frame.Right-frame.Left)/scale,(frame.Bottom-frame.Top)/scale),
            new Size((info.Work.Right-info.Work.Left)/scale,(info.Work.Bottom-info.Work.Top)/scale));
        SetLimits(limits,initial);
        if(initial)
        {
            window.Left=(info.Work.Left+info.Work.Right)/(2*scale)-limits.Width/2;
            window.Top=(info.Work.Top+info.Work.Bottom)/(2*scale)-limits.Height/2;
        }
        if(!initial&&window.WindowState==WindowState.Normal&&GetWindowRect(hwnd,out var bounds))
        {
            int left=Math.Clamp(bounds.Left,info.Work.Left,Math.Max(info.Work.Left,info.Work.Right-(bounds.Right-bounds.Left)));
            int top=Math.Clamp(bounds.Top,info.Work.Top,Math.Max(info.Work.Top,info.Work.Bottom-(bounds.Bottom-bounds.Top)));
            if(left!=bounds.Left||top!=bounds.Top)SetWindowPos(hwnd,0,left,top,0,0,0x0015);
        }
    }
    private void SetLimits(Size limits,bool initial)
    {
        window.MinWidth=Math.Min(minimumWidth,limits.Width);window.MinHeight=Math.Min(minimumHeight,limits.Height);
        window.MaxWidth=limits.Width;window.MaxHeight=limits.Height;
        if(initial){window.Width=limits.Width;window.Height=limits.Height;}
        else if(window.WindowState==WindowState.Normal)
        {
            window.Width=Math.Clamp(window.Width,window.MinWidth,limits.Width);
            window.Height=Math.Clamp(window.Height,window.MinHeight,limits.Height);
        }
    }
    private void Closed(object? sender,EventArgs e)
    {
        closed=true;source?.RemoveHook(Messages);source=null;
        window.SourceInitialized-=Initialize;window.DpiChanged-=DpiChanged;window.Closed-=Closed;
    }
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect{internal int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo{internal int Size;internal NativeRect Monitor,Work;internal uint Flags;}
    [DllImport("user32.dll")]private static extern nint MonitorFromWindow(nint hwnd,uint flags);
    [DllImport("user32.dll",EntryPoint="GetMonitorInfoW")]private static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern nint GetWindowLongPtr(nint hwnd,int index);
    [DllImport("user32.dll")]private static extern bool AdjustWindowRectExForDpi(ref NativeRect rect,uint style,bool menu,uint extended,uint dpi);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(nint hwnd,out NativeRect rect);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
}
