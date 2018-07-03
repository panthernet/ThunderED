using System;
using System.IO;
using System.Windows;
using TED_ConfigEditor.Classes;

namespace TED_ConfigEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static Options Options { get; private set; }

        public static Logger Logger { get; set; }

        public App()
        {
            Options = new Options();
            Logger = new Logger(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "log.txt"));
            //TODO load options
            this.DispatcherUnhandledException += (sender, e) =>
            {
                Logger.Log(e.Exception, "Unhandled!!!");
                e.Handled = true;
                Application.Current.Shutdown();
            };
        }
    }
}
