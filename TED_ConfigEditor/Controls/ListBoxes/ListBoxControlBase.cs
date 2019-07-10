using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MahApps.Metro.Controls.Dialogs;
using TED_ConfigEditor.Classes;

namespace TED_ConfigEditor.Controls
{
    [TemplatePart(Name = "PART_ListBox", Type = typeof(ListBox))]
    public class ListBoxControlBase: UserControl, INotifyPropertyChanged
    {
        protected ListBox ListBox;

        public ICommand AddCommand { get; set; }
        public ICommand RemoveCommand { get; set; }

        public Type ItemType { get; set; }


        protected void RemoveCommandMethod(object obj)
        {
            var list = new List<object>();
            foreach (var item in ListBox.SelectedItems)
                list.Add(item);
            list.ForEach(item=>
            {
                if (!IsDictionary)
                    ((IList) ItemsList).Remove(item);
                else
                    ((IDictionary) ItemsList).Remove(item.GetKeyFromPair());
            });
            OnPropertyChanged2("ItemsList");
        }

        protected async void AddCommandMethod(object obj)
        {
            if (IsValidatableCollection)
            {
                ((IList) ItemsList).Add(Activator.CreateInstance(ItemType));
                OnPropertyChanged2("ItemsList");
                return;
            }

            var isDoubleEntry = IsDictionary && !ItemType.IsClass;

            var res = isDoubleEntry ? await MainWindow.Instance.ShowInputAsync("Enter new key", "Key:") : await MainWindow.Instance.ShowInputAsync("Enter new value", "Value:");
            res = res?.Replace("\"", "");
            if(string.IsNullOrEmpty(res)) return;
            try
            {
                if (IsDictionary)
                {
                    if (isDoubleEntry)
                    {
                        var res2 = await MainWindow.Instance.ShowInputAsync("Enter new value", "Value:");
                        if(string.IsNullOrEmpty(res2)) return;
                        ((IDictionary) ItemsList).Add(res, Convert.ChangeType(res2, ItemType));
                    }
                    else ((IDictionary) ItemsList).Add(res, Activator.CreateInstance(ItemType));
                }
                else
                {
                    if (IsMixedList)
                    {
                        if(long.TryParse(res, out var parsedResult))
                            ((IList) ItemsList).Add(parsedResult);
                        else ((IList) ItemsList).Add(res);
                    }
                    else ((IList) ItemsList).Add(Convert.ChangeType(res, ItemType));
                }

                OnPropertyChanged2("ItemsList");
            }
            catch
            {
                await MainWindow.Instance.ShowMessageAsync("ERROR", $"Invalid value format! Must be {ItemType?.Name ?? "???"}");
            }
        }
        
        static ListBoxControlBase() 
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ListBoxControlBase), new FrameworkPropertyMetadata());
        }

        public ListBoxControlBase()
        {
            DataContext = this;
            AddCommand = new SimpleCommand(AddCommandMethod);
            RemoveCommand = new SimpleCommand(RemoveCommandMethod);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnPropertyChanged2(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override void OnApplyTemplate()
        {
            ListBox = ListBox ?? VisualHelper.FindVisualChildren<ListBox>(this).FirstOrDefault();

            if(ListBox != null)
            {
                var myBinding = new Binding
                {
                    Source = this,
                    Path = new PropertyPath("ItemsList"),
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };

                ListBox.SetBinding(ItemsControl.ItemsSourceProperty, myBinding);
            }
        }

        public static readonly DependencyProperty ItemsListProperty =
            DependencyProperty.Register("ItemsList", typeof(object), typeof(ListBoxControlBase), new UIPropertyMetadata(null, (o, args) => {}));


        public object ItemsList
        {
            get => (object)GetValue(ItemsListProperty);
            set => SetValue(ItemsListProperty, value);
        }

        public bool IsDictionary { get; set; }
        public bool IsMixedList { get; set; }
        public bool IsValidatableCollection { get; set; }
    }
}
