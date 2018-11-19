using System;
using System.Reflection;
using System.Windows;
using TED_ConfigEditor.Classes;

namespace TED_ConfigEditor.Controls.Modules
{
    /// <summary>
    /// Interaction logic for ConfigModuleControl.xaml
    /// </summary>
    public partial class ModuleControlEx
    {
        public IModuleControl Settings { get; set; }
        public string Title { get; set; }

        public static readonly DependencyProperty ItemProperty =
            DependencyProperty.Register("Item", typeof(object), typeof(ModuleControlEx), new UIPropertyMetadata(null, OnItemChanged));

        private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = d as ModuleControlEx;
            c?.InstantiateConfigModule(e.NewValue.GetType());
        }

        public object Item
        {
            get => GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
        }

        private IModuleControl InstantiateConfigModule(Type getType)
        {
            Title = null;
            var curItem = Item;
            if (getType.IsList())
            {
              //  getType = getType.ExtractValueTypeFromPair();
            }
            else
            {
                if (getType.IsKeyValuePair())
                {
                    getType = getType.ExtractValueTypeFromPair();
                    curItem = Item.GetValueFromPair();
                }
            }


            var makeme = Type.GetType("TED_ConfigEditor.Controls.Modules.ConfigModuleBase`1").MakeGenericType(getType);
            Settings = (IModuleControl)Activator.CreateInstance(makeme, new object[] {curItem, container});

            return Settings;
        }

        public ModuleControlEx()
        {
            InitializeComponent();
        }
    }
}
