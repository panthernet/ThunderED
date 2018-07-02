using System.Windows;

namespace TED_ConfigEditor.Controls
{

    /// <summary>
    /// Help label custom control. Incorporates label and help icon.
    /// </summary>
    public partial class HelpLabel
    {
        public double LabelMinWidth { get; set; } = 160;

        public string LabelText { get; set; }
        public object ToolTipText { get; set; }
        public Visibility HelpVisibility { get; set; } = Visibility.Visible;

        public HelpLabel()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void SetBoldName()
        {
            label.FontWeight = FontWeights.Bold;
        }
    }
}
