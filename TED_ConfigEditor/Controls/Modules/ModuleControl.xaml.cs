namespace TED_ConfigEditor.Controls.Modules
{
    /// <summary>
    /// Interaction logic for ConfigModuleControl.xaml
    /// </summary>
    public partial class ModuleControl
    {
        public IModuleControl Settings { get; set; }
        public string Title { get; set; }

        public ModuleControl(IModuleControl control, string title)
        {
            InitializeComponent();
            DataContext = this;
            Title = title;
            Settings = control;
            Settings.ContainerControl = container;
        }
    }
}
