namespace TED_ConfigEditor.Controls
{
    /// <summary>
    /// Interaction logic for ConfigModuleControl.xaml
    /// </summary>
    public partial class ErrorsControl
    {
        public string Text { get; set; }

        public ErrorsControl(string text)
        {
            InitializeComponent();
            DataContext = this;
            Text = text;

        }
    }
}
