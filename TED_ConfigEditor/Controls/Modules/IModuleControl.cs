using System.Windows.Controls;

namespace TED_ConfigEditor.Controls.Modules
{
    public interface IModuleControl
    {
        DockPanel ContainerControl { get; set; }
        void GenerateFields();
    }
}