using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using TED_ConfigEditor.Classes;
using TED_ConfigEditor.Controls;
using TED_ConfigEditor.Controls.Modules;
using ThunderED;

namespace TED_ConfigEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: INotifyPropertyChanged
    {
        private string _fileName;
        private string _selectedModuleToAdd;
        public ObservableCollection<string> AvailableModulesList { get; set; }
        public ObservableCollection<string> ModulesList { get; set; } = new ObservableCollection<string>();

        private bool isStartup = true;

        public string SelectedModuleToAdd
        {
            get => _selectedModuleToAdd;
            set { _selectedModuleToAdd = value; OnPropertyChanged();}
        }

        public ThunderSettings Settings { get; set; } = new ThunderSettings();

        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged();}
        }

        public ICommand AddFileCommand { get; set; }
        public ICommand OpenFileCommand { get; set; }
        public ICommand AddModuleCommand { get; set; }
        public ICommand EditModuleCommand { get; set; }
        public ICommand DeleteModuleCommand { get; set; }
        public ICommand SaveFileCommand { get; set; }
        public ICommand ValidateCommand { get; set; }

        public static MainWindow Instance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Instance = this;
            AddFileCommand = new SimpleCommand(AddFileExecuted);
            OpenFileCommand = new SimpleCommand(OpenFileExecuted);
            AddModuleCommand = new SimpleCommand(o=> AvailableModulesList.Count > 0 && !string.IsNullOrEmpty(_fileName), AddModuleExecuted);
            EditModuleCommand = new SimpleCommand(EditModuleExecuted);
            DeleteModuleCommand = new SimpleCommand(DeleteModuleExecuted);
            SaveFileCommand = new SimpleCommand(o => !string.IsNullOrEmpty(_fileName), OnSaveFileExecuted);
            ValidateCommand = new SimpleCommand(o=> !string.IsNullOrEmpty(_fileName), async o =>
            {
                var text = Settings.Validate(ModulesList.ToList());
                if (!string.IsNullOrEmpty(text))
                    EditModuleExecuted(new ErrorsControl(text));
                else await this.ShowMessageAsync("SUCCESS", "Validation successful!");
            });

            configModuleControl.Visibility = Visibility.Collapsed;
            modulesPanel.Visibility = Visibility.Collapsed;
            listBox.Visibility = Visibility.Collapsed;

            ResetFile();
            //Title = $"ThunderED Bot Config Tool v{Program.VERSION}";
            UpdateTitle();
        }

        private async void OnSaveFileExecuted(object obj)
        {
            try
            {
                var props = typeof(ThunderSettings).GetProperties();
                var cProps = typeof(ConfigSettings).GetProperties();
                //null left modules
                if (App.Options.NullifyDisabledModules)
                {
                    foreach (var modulesEnum in AvailableModulesList)
                    {
                        var moduleName = modulesEnum.DescriptionAttr().ToLower();
                        var p = props.FirstOrDefault(a => a.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
                        if (p == null) continue;
                        p.SetValue(Settings, null);
                    }
                }

                //check added modules
                foreach (var modulesEnum in ModulesList)
                {
                    var p = cProps.FirstOrDefault(a => a.Name.Equals(modulesEnum, StringComparison.OrdinalIgnoreCase));
                    if (p == null) continue;
                    p.SetValue(Settings.Config, true);
                }
                //uncheck skipped modules
                foreach (var modulesEnum in AvailableModulesList)
                {
                    var p = cProps.FirstOrDefault(a => a.Name.Equals(modulesEnum, StringComparison.OrdinalIgnoreCase));
                    if (p == null) continue;
                    p.SetValue(Settings.Config, false);
                }

                var text = Settings.Validate(ModulesList.ToList());
                if (!string.IsNullOrEmpty(text))
                {
                    continueButton.Visibility = Visibility.Visible;
                    EditModuleExecuted(new ErrorsControl(text));
                    return;
                }

                ActualSaveRoutine();
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("ERROR", "Error saving file! Read logs for details.");
                App.Logger.Log(ex, nameof(OnSaveFileExecuted));
            }
        }

        private void UpdateTitle(string file = null)
        {
            Title = $"ThunderED Bot Config Tool v{Program.VERSION} {(string.IsNullOrEmpty(file) ? "" : $" - {Path.GetFileNameWithoutExtension(file)}")}";
        }

        private void ResetFile()
        {
            FileName = null;
            UpdateTitle();
            AvailableModulesList = new ObservableCollection<string>(GetAvailableModuleNames());
            SelectedModuleToAdd = AvailableModulesList.FirstOrDefault();
            ModulesList.Clear();
            GetStaticModulesList().ForEach(ModulesList.Add);


        }

        public List<string> GetStaticModulesList()
        {
            return Settings.GetType().GetProperties().Where(a => a.Name != "Config" && a.HasAttribute<StaticConfigEntryAttribute>()).Select(a => a.Name)
                .Where(a => !string.IsNullOrEmpty(a)).ToList();
        }

        public List<string> GetAvailableModuleNames()
        {
            return Settings.GetType().GetProperties().Where(a => a.Name != "Config").Select(a => (string)a.GetAttributeValue<ConfigEntryNameAttribute>("Name"))
                .Where(a => !string.IsNullOrEmpty(a)).ToList();
        }

        public PropertyInfo GetPropertyByEntryName(string name)
        {
            return Settings.GetType().GetProperties().FirstOrDefault(a => (string) a.GetAttributeValue<ConfigEntryNameAttribute>("Name") == name);
        }

        public PropertyInfo GetPropertyByName(string name)
        {
            return Settings.GetType().GetProperties().FirstOrDefault(a => a.Name == name);
        }

        private async void AddFileExecuted(object obj)
        {
            if (!isStartup && (!string.IsNullOrEmpty(FileName) || (ModulesList.Count > 0 && ModulesList.Count != GetStaticModulesList().Count)))
            {
                if(await this.ShowMessageAsync("Warning", "Create new settings file? All unsaved changed will be lost.", MessageDialogStyle.AffirmativeAndNegative) == MessageDialogResult.Negative)
                    return;
            }
            var dlg = new SaveFileDialog() {Filter = "JSON Files (*.json)|*.json"};
            if(dlg.ShowDialog() != true)
                return;
            Settings = ThunderSettings.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.def.json"));
            if (Settings == null)
            {
                await this.ShowMessageAsync("Error", "Default settings file not found!");
                return;
            }

            isStartup = false;
            ResetFile();
            FileName = dlg.FileName;
            UpdateTitle(FileName);
            LoadModules();
            UpdateBindings();
            configModuleControl.Visibility = Visibility.Visible;
            modulesPanel.Visibility = Visibility.Visible;
            listBox.Visibility = Visibility.Visible;
        }

        private async void OpenFileExecuted(object obj)
        {
            try
            {
                if (!isStartup && (!string.IsNullOrEmpty(FileName) || ModulesList.Count > 0))
                {
                    if (await this.ShowMessageAsync("Warning", "Open new settings file? All unsaved changed will be lost.", MessageDialogStyle.AffirmativeAndNegative) ==
                        MessageDialogResult.Negative)
                        return;
                }

                var dlg = new OpenFileDialog {Filter = "JSON Files (*.json)|*.json", CheckFileExists = true};
                if (dlg.ShowDialog() != true)
                    return;

                ResetFile();

                Settings = ThunderSettings.Load(dlg.FileName);
                if (Settings == null)
                {
                    await this.ShowMessageAsync("Error", "Can't load settings file!");
                    ResetFile();
                    return;
                }

                isStartup = false;
                FileName = dlg.FileName;
                UpdateTitle(FileName);
                LoadModules();
                UpdateBindings();
                configModuleControl.Visibility = Visibility.Visible;
                modulesPanel.Visibility = Visibility.Visible;
                listBox.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("ERROR", "Error opening file! Read logs for details.");
                App.Logger.Log(ex, nameof(OpenFileExecuted));
            }
        }

        private void UpdateBindings()
        {
            configModuleControl.Settings = Settings.Config;
        }

        private void LoadModules()
        {
            ModulesList.Clear();
            GetStaticModulesList().ForEach(ModulesList.Add);
            AvailableModulesList.Clear();

            var configurableList = GetAvailableModuleNames();
            AvailableModulesList.Add("moduleWebServer");

            foreach (var propertyInfo in Settings.Config.GetType().GetProperties().Where(a=> a.Name.ToLower().StartsWith("module")))
            {
                var entry = configurableList.FirstOrDefault(a => a.Equals(propertyInfo.Name, StringComparison.OrdinalIgnoreCase));
                //(string)propertyInfo.GetAttributeValue<ConfigEntryNameAttribute>("Name");
                if(string.IsNullOrEmpty(entry)) continue;

                var value = (bool) propertyInfo.GetValue(Settings.Config);
                if (value)
                {
                    ModulesList.Add(entry);
                    AvailableModulesList.Remove(entry);
                }
                else
                {
                    AvailableModulesList.Add(entry);
                    ModulesList.Remove(entry);
                }
            }
            OnPropertyChanged(nameof(AvailableModulesList));
            SelectedModuleToAdd = AvailableModulesList.FirstOrDefault();
        }

        private void LoadOverlay(UIElement element)
        {
            layerGrid.Visibility = Visibility.Visible;
            layerContainer.Child = element;
        }

        private void UnloadOverlay()
        {
            layerGrid.Visibility = Visibility.Collapsed;
            layerContainer.Child = null;
            continueButton.Visibility = Visibility.Collapsed;
        }

        private void DeleteModuleExecuted(object obj)
        {
            if(GetStaticModulesList().Contains((string)obj))
                return;
            ModulesList.Remove((string)obj);
            AvailableModulesList.Add((string)obj);
        }

        private async void EditModuleExecuted(object obj)
        {
            if (obj is ErrorsControl control)
            {
                LoadOverlay(control);
                return;
            }

            try
            {
                var moduleEntryName = (string) obj;
                PropertyInfo prop = null;
                if (GetStaticModulesList().Contains(moduleEntryName))
                    prop = GetPropertyByName(moduleEntryName);
                else prop = GetPropertyByEntryName(moduleEntryName);
                var makeme = Type.GetType("TED_ConfigEditor.Controls.Modules.ConfigModuleBase`1").MakeGenericType(prop.PropertyType);
                var s = (IModuleControl) Activator.CreateInstance(makeme);
                UIElement element = new ModuleControl(s, $"{moduleEntryName} Settings");
                s.GetType().GetProperty("Settings").SetValue(s, prop.GetValue(Settings));

                if (element != null)
                    LoadOverlay(element);
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("ERROR", "Error loading settings section! Please contact developer!");
                App.Logger.Log(ex, nameof(EditModuleExecuted));
                CloseOverlay_OnClick(null, null);
            }
        }

        private void AddModuleExecuted(object obj)
        {
            if(_selectedModuleToAdd == null) return;

            ModulesList.Add(_selectedModuleToAdd);
            AvailableModulesList.Remove(_selectedModuleToAdd);
            SelectedModuleToAdd = AvailableModulesList.FirstOrDefault();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void CloseOverlay_OnClick(object sender, RoutedEventArgs e)
        {
            UnloadOverlay();
        }

        private void ContinueOverlay_OnClick(object sender, RoutedEventArgs e)
        {
            UnloadOverlay();
            ActualSaveRoutine();
        }

        private void ActualSaveRoutine()
        {
            Settings.BeforeEditorSave();
            Settings.Save(FileName);
        }
    }
}
