using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace TED_ConfigEditor.Classes
{
    public static class VisualHelper
    {
        public static List<T> FindVisualChildren<T>(DependencyObject obj) where T : DependencyObject
        {
            var children = new List<T>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var o = VisualTreeHelper.GetChild(obj, i);
                if (o == null) continue;
                var item = o as T;
                if (item != null)
                    children.Add(item);

                children.AddRange(FindVisualChildren<T>(o)); // recursive
            }
            return children;
        }

        public static T FindFirstVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            T child = null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var o = VisualTreeHelper.GetChild(obj, i);
                if (o == null) continue;
                var item = o as T;
                if (item != null)
                {
                    child = item;
                    break;
                }
                child = FindFirstVisualChild<T>(o); // recursive
            }
            return child;
        }

        public static T FindUpVisualTree<T>(DependencyObject initial) where T : DependencyObject
        {
            var current = initial;

            while (current != null && current.GetType() != typeof(T))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as T;
        }
    }
}
