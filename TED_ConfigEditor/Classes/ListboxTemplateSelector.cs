using System.Windows;
using System.Windows.Controls;

namespace TED_ConfigEditor.Classes
{

    public class ListboxTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var elemnt = container as FrameworkElement;
            if((item.IsKeyValuePair() && item.GetValueFromPair().GetType().IsClass))
                return elemnt.FindResource("ComplexValueTemplate") as DataTemplate;

            if (item is IValidatable)
                return elemnt.FindResource("CollectionValueTemplate") as DataTemplate;

            return elemnt.FindResource("SingleValueTemplate") as DataTemplate;
        }
    }
}
