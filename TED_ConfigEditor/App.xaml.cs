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

        public App()
        {
            Options = new Options();
            //TODO load options
        }
    }
}
