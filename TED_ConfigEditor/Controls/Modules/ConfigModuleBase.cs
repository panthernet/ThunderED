using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using MahApps.Metro.Controls;
using TED_ConfigEditor.Classes;
using ThunderED.Classes;
using Brushes = System.Windows.Media.Brushes;
using Extensions = TED_ConfigEditor.Classes.Extensions;
using Size = System.Windows.Size;

namespace TED_ConfigEditor.Controls.Modules
{
    public class ConfigModuleBaseSettings: ConfigModuleBase<ConfigSettings>
    {
    }


    public class ConfigModuleBase<T>: UserControl, INotifyPropertyChanged, IModuleControl
    {
        public DockPanel ContainerControl { get; set; }

        public static readonly DependencyProperty SettingsProperty =
            DependencyProperty.Register("Settings", typeof(T), typeof(ConfigModuleBase<T>), new UIPropertyMetadata(null, OnSettingsChanged));

        private static void OnSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (IModuleControl) d;
            ctrl.GenerateFields();
        }


        public T Settings
        {
            get => (T)GetValue(SettingsProperty);
            set => SetValue(SettingsProperty, value);
        }

        public ConfigModuleBase() {}

        public ConfigModuleBase(object settings, DockPanel container)
        {
            ContainerControl = container;
            Settings = (T) settings;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static Size MeasureString(string text, Control tb)
        {
            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                tb.FontSize,
                Brushes.Black,
                new NumberSubstitution(),
                TextFormattingMode.Display);

            return new Size(formattedText.Width, formattedText.Height);
        }

        public void GenerateFields()
        {
            if(ContainerControl == null || Settings == null) return;

            var props = Settings.GetType().GetProperties().Where(a=> a.CanWrite);
            var label = new Label { FontWeight =  FontWeights.Bold};
            var labelMinWidth = props.Max(a => MeasureString(a.Name, label).Width) + 10;

            var configurableModuleNames = MainWindow.Instance.GetAvailableModuleNames().Select(a=> a.ToLower());

            if (Settings.GetType().UnderlyingSystemType.Name == "ObservableCollection`1" || Settings.GetType().UnderlyingSystemType.Name == "List`1")
            {                
                var d = new DockPanel();
                DockPanel.SetDock(d, Dock.Top);

               // var property = Settings.GetType()
               //     .GetProperties().FirstOrDefault(p => p.Name=="Item");

                var property = this.GetType().GetProperty("Settings");
                GenerateListBoxInstance(property, d);
                ContainerControl.Children.Add(d);
                return;
            }

            foreach (var property in props)
            {
                if(property.Name.StartsWith("Module") && configurableModuleNames.Contains(property.Name.ToLower()))
                        continue;
                //config specific
                
                var d = new DockPanel();
                DockPanel.SetDock(d, Dock.Top);

                var comment = property.GetAttributeValue<CommentAttribute>("Comment") as string;
                var required = property.HasAttribute<RequiredAttribute>();
                var t = new HelpLabel {LabelText = property.Name, ToolTipText = comment};
                if (required)
                    t.SetBoldName();
                if (labelMinWidth > 0)
                    t.LabelMinWidth = labelMinWidth;
                if (string.IsNullOrEmpty(comment))
                    t.HelpVisibility = Visibility.Hidden;
                DockPanel.SetDock(t, Dock.Left);
                d.Children.Add(t);

                if (property.PropertyType == typeof(int) || property.PropertyType == typeof(long)
                    || property.PropertyType == typeof(uint) || property.PropertyType == typeof(ulong) || property.PropertyType == typeof(float))
                {
                    var tb = new TextBox();
                    tb.MinWidth = 50;
                    tb.PreviewTextInput += (sender, e) => { e.Handled = !Extensions.IsNumericValue(e.Text); };
                    DataObject.AddPastingHandler(tb, (sender, e) =>
                    {
                        if (e.DataObject.GetDataPresent(typeof(string)))
                        {
                            var text = (string) e.DataObject.GetData(typeof(string));
                            if (Extensions.IsNumericValue(text))
                                return;
                        }
                        e.CancelCommand();
                    });
                    if (property.PropertyType == typeof(int))
                        tb.MaxLength = int.MaxValue.ToString().Length;
                    else if (property.PropertyType == typeof(long))
                        tb.MaxLength = long.MaxValue.ToString().Length;
                    else if (property.PropertyType == typeof(uint))
                        tb.MaxLength = uint.MaxValue.ToString().Length;
                    else if (property.PropertyType == typeof(ulong))
                        tb.MaxLength = ulong.MaxValue.ToString().Length;

                    var myBinding = new Binding
                    {
                        Source = Settings,
                        Path = new PropertyPath(property.Name),
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                        ValidatesOnDataErrors = true,
                    };
                    tb.SetBinding(TextBox.TextProperty, myBinding);
                    d.Children.Add(tb);
                } 
                else if (property.PropertyType == typeof(string))
                {
                    var tb = new TextBox();
                    tb.MinWidth = 50;
                    var myBinding = new Binding
                    {
                        Source = Settings,
                        Path = new PropertyPath(property.Name),
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                        ValidatesOnDataErrors = true,
                        //NotifyOnValidationError = true
                    };
                    tb.SetBinding(TextBox.TextProperty, myBinding);
                    d.Children.Add(tb);
                }else if (property.PropertyType == typeof(bool))
                {
                    var cb = new CheckBox();
                    var myBinding = new Binding
                    {
                        Source = Settings,
                        Path = new PropertyPath(property.Name),
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    };
                    cb.SetBinding(ToggleButton.IsCheckedProperty, myBinding);
                    d.Children.Add(cb);
                }
                else if (property.PropertyType == typeof(StandingsAuthGroupExtension))
                {
                    var value = property.GetValue(Settings) ?? new StandingsAuthGroupExtension();
                    property.SetValue(Settings, value);

                    var dp = new DockPanel();
                    DockPanel.SetDock(dp, Dock.Top);
                    d.Children.Add(dp);
                    new ConfigModuleBase<StandingsAuthGroupExtension>(value, dp);
                }
                else
                {
                    //var makeme = Type.GetType("TED_ConfigEditor.Controls.ListBoxControlBase`1").MakeGenericType(property.PropertyType.GenericTypeArguments);

                    GenerateDictionaryBoxInstance(property, d);
                }
                ContainerControl.Children.Add(d);
            }
        }

        public void GenerateDictionaryBoxInstance(PropertyInfo property, DockPanel d)
        {
            if (Activator.CreateInstance(typeof(ListBoxControlBase)) is ListBoxControlBase el)
            {
                el.IsDictionary = typeof(IDictionary).IsAssignableFrom(property.PropertyType);                
                el.IsValidatableCollection = typeof(IList).IsAssignableFrom(property.PropertyType) && typeof(IValidatable).IsAssignableFrom(property.PropertyType.GenericTypeArguments.Last());
                el.ItemType = el.IsDictionary ? property.PropertyType.GenericTypeArguments.LastOrDefault() : property.PropertyType.GenericTypeArguments.FirstOrDefault();
                el.IsMixedList = el.ItemType == typeof(object);
                var dp = el.GetType()
                    .GetFields(BindingFlags.Static | BindingFlags.Public).FirstOrDefault(p => p.FieldType == typeof(DependencyProperty) && p.Name=="ItemsListProperty");
                        
                var myBinding = new Binding//(name.GetValue(dp))
                {
                    Source = Settings,
                    Path = new PropertyPath(property.Name),
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };

                el.SetBinding((DependencyProperty)dp.GetValue(el), myBinding);
                d.Children.Add(el);
            }
        }

        public void GenerateListBoxInstance(PropertyInfo property, DockPanel d)
        {
            if (Activator.CreateInstance(typeof(ListBoxControlBase)) is ListBoxControlBase el)
            {
                el.IsDictionary = false;
                el.IsValidatableCollection = typeof(IList).IsAssignableFrom(property.PropertyType) && typeof(IValidatable).IsAssignableFrom(property.PropertyType.GenericTypeArguments.Last());
                el.ItemType = property.PropertyType.GenericTypeArguments.FirstOrDefault();
                var dp = el.GetType()
                    .GetFields(BindingFlags.Static | BindingFlags.Public).FirstOrDefault(p => p.FieldType == typeof(DependencyProperty) && p.Name=="ItemsListProperty");
                        
                var myBinding = new Binding//(name.GetValue(dp))
                {
                    Source = this,
                    Path = new PropertyPath(property.Name),
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };

                el.SetBinding((DependencyProperty)dp.GetValue(el), myBinding);
                d.Children.Add(el);
            }
        }
    }
}
