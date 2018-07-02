namespace TED_ConfigEditor.Controls
{
    /// <summary>
    /// Interaction logic for ConfigModuleControl.xaml
    /// </summary>
    public partial class ConfigModuleControl
    {
        public ConfigModuleControl()
        {
            InitializeComponent();
            DataContext = this;
            ContainerControl = container;
            GenerateFields();
        }
    }
}
