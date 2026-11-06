using System.Windows;

namespace Tolerance.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Appearance.Apply(false);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"操作未完成：{args.Exception.Message}", "几何公差查询工具", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        if (e.Args.Length == 2 && e.Args[0] == "--verify-ui")
        {
            var window = new MainWindow(); MainWindow=window;
            window.Show();
            window.Dispatcher.InvokeAsync(() =>
            {
                try { UiVerification.Run(window, e.Args[1]); Shutdown(0); }
                catch (Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "ui-failure.txt"), ex.ToString()); Shutdown(1); }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        else
        {
            var shell=new ShellWindow(e.Args.Length==2&&e.Args[0]=="--verify-all"?System.IO.Path.Combine(e.Args[1],"settings-test.json"):null);MainWindow=shell;
            shell.Show();
            if(e.Args.Length==2&&e.Args[0]=="--verify-all")shell.Dispatcher.InvokeAsync(()=>
            {
                try{ShellVerification.Run(shell,e.Args[1]);Shutdown(0);}
                catch(Exception ex){System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1],"shell-failure.txt"),ex.ToString());Shutdown(1);}
            },System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }
}
