using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using TED_ConfigEditor.Classes;
using TED_ConfigEditor.Controls.Modules;

namespace TED_ConfigEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: INotifyPropertyChanged
    {
        private string _fileName;
        private ModulesEnum? _selectedModuleToAdd;
        public ObservableCollection<ModulesEnum> AvailableModulesList { get; set; }
        public ObservableCollection<ModulesEnum> ModulesList { get; set; } = new ObservableCollection<ModulesEnum>();

        public ModulesEnum? SelectedModuleToAdd
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

            ResetFile();
        }

        private void OnSaveFileExecuted(object obj)
        {
            var props = typeof(ThunderSettings).GetProperties();
            var cProps = typeof(ConfigSettings).GetProperties();
            //null left modules
            if (App.Options.NullifyDisabledModules)
            {
                foreach (var modulesEnum in AvailableModulesList)
                {
                    var moduleName = modulesEnum.DescriptionAttr().ToLower();
                    var p = props.FirstOrDefault(a => a.Name.ToLower() == moduleName);
                    if (p == null) continue;
                    p.SetValue(Settings, null);
                }
            }

            //check added modules
            foreach (var modulesEnum in ModulesList)
            {
                var moduleName = modulesEnum.ToString();
                var p = cProps.FirstOrDefault(a => a.Name == moduleName);
                if(p == null) continue;
                p.SetValue(Settings.Config, true);
            }

            Settings.Save(FileName);
        }

        private void UpdateTitle(string file = null)
        {
            Title = $"ThunderED Bot Config Tool {(string.IsNullOrEmpty(file) ? "" : $" - {Path.GetFileNameWithoutExtension(file)}")}";

        }

        private void ResetFile()
        {
            FileName = null;
            UpdateTitle();
            AvailableModulesList = new ObservableCollection<ModulesEnum>(Enum.GetValues(typeof(ModulesEnum)).Cast<ModulesEnum>().Where(a=> !string.IsNullOrEmpty(a.DescriptionAttr())));
            SelectedModuleToAdd = AvailableModulesList.FirstOrDefault();
            ModulesList.Clear();
        }

        private async void AddFileExecuted(object obj)
        {
            if (!string.IsNullOrEmpty(FileName) || ModulesList.Count > 0)
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

            FileName = dlg.FileName;
            UpdateTitle(FileName);
            UpdateBindings();
        }

        private async void OpenFileExecuted(object obj)
        {
            if (!string.IsNullOrEmpty(FileName) || ModulesList.Count > 0)
            {
                if(await this.ShowMessageAsync("Warning", "Open new settings file? All unsaved changed will be lost.", MessageDialogStyle.AffirmativeAndNegative) == MessageDialogResult.Negative)
                    return;
            }

            var dlg = new OpenFileDialog {Filter = "JSON Files (*.json)|*.json", CheckFileExists = true };
            if(dlg.ShowDialog() != true)
                return;

            ResetFile();

            Settings = ThunderSettings.Load(dlg.FileName);
            if (Settings == null)
            {
                await this.ShowMessageAsync("Error", "Can't load settings file!");
                ResetFile();
                return;
            }


            FileName = dlg.FileName;
            UpdateTitle(FileName);
            LoadModules();
            UpdateBindings();
        }

        private void UpdateBindings()
        {
            configModuleControl.Settings = Settings.Config;
        }

        private void LoadModules()
        {
            foreach (var propertyInfo in Settings.Config.GetType().GetProperties().Where(a=> a.Name.StartsWith("Module")))
            {

                if(!Enum.TryParse(propertyInfo.Name, true, out ModulesEnum e))
                    continue;
                var value = (bool) propertyInfo.GetValue(Settings.Config);
                if (value)
                {
                    ModulesList.Add(e);
                    AvailableModulesList.Remove(e);
                }
                else
                {
                    AvailableModulesList.Add(e);
                    ModulesList.Remove(e);
                }
            }
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
        }

        private void DeleteModuleExecuted(object obj)
        {
            var m = (ModulesEnum)obj;
            ModulesList.Remove(m);
            AvailableModulesList.Add(m);
        }

        private void EditModuleExecuted(object obj)
        {
            UIElement element = null;

            switch ((ModulesEnum)obj)
            {
                case ModulesEnum.ModuleWebServer:
                {
                    var s = new ConfigModuleBase<WebServerModuleSettings>();
                    element = new ModuleControl(s, "Web Server Module Settings");
                    s.Settings = Settings.WebServerModule;
                }
                    break;
                case ModulesEnum.ModuleIRC:
                {
                    var s = new ConfigModuleBase<IRCModuleSettings>();
                    element = new ModuleControl(s, "IRC Module Settings");
                    s.Settings = Settings.IrcModule;
                }
                    break;
                case ModulesEnum.ModuleAuthWeb:
                {
                    var s = new ConfigModuleBase<WebAuthModuleSettings>();
                    element = new ModuleControl(s, "Web Auth Module Settings");
                    s.Settings = Settings.WebAuthModule;
                }
                    break;
                case ModulesEnum.ModuleChatRelay:
                {
                    var s = new ConfigModuleBase<ChatRelayModuleSettings>();
                    element = new ModuleControl(s, "Chat Relay Module Settings");
                    s.Settings = Settings.ChatRelayModule;
                }
                    break;
                case ModulesEnum.ModuleFleetup:
                {
                    var s = new ConfigModuleBase<FleetupModuleSettings>();
                    element = new ModuleControl(s, "Fleetup Module Settings");
                    s.Settings = Settings.FleetupModule;
                }
                    break;
                case ModulesEnum.ModuleIncursionNotify:
                {
                    var s = new ConfigModuleBase<IncursionNotificationModuleSettings>();
                    element = new ModuleControl(s, "Incursion Module Settings");
                    s.Settings = Settings.IncursionNotificationModule;
                }
                    break;
                case ModulesEnum.ModuleJabber:
                {
                    var s = new ConfigModuleBase<JabberModuleSettings>();
                    element = new ModuleControl(s, "Jabber Module Settings");
                    s.Settings = Settings.JabberModule;
                }
                    break;
                case ModulesEnum.ModuleLiveKillFeed:
                {
                    var s = new ConfigModuleBase<LiveKillFeedModuleSettings>();
                    element = new ModuleControl(s, "Live Kill Feed Module Settings");
                    s.Settings = Settings.LiveKillFeedModule;
                }
                    break;
                case ModulesEnum.ModuleMail:
                {
                    var s = new ConfigModuleBase<MailModuleSettings>();
                    element = new ModuleControl(s, "Mail Module Settings");
                    s.Settings = Settings.MailModule;
                }
                    break;
                case ModulesEnum.ModuleNotificationFeed:
                {
                    var s = new ConfigModuleBase<NotificationFeedSettings>();
                    element = new ModuleControl(s, "Notification Feed Module Settings");
                    s.Settings = Settings.NotificationFeedModule;
                }
                    break;
                case ModulesEnum.ModuleRadiusKillFeed:
                {
                    var s = new ConfigModuleBase<RadiusKillFeedModuleSettings>();
                    element = new ModuleControl(s, "Radius Kill Feed Module Settings");
                    s.Settings = Settings.RadiusKillFeedModule;
                }
                    break;
                case ModulesEnum.ModuleTelegram:
                {
                    var s = new ConfigModuleBase<TelegramModuleSettings>();
                    element = new ModuleControl(s, "Telegram Module Settings");
                    s.Settings = Settings.TelegramModule;
                }
                    break;
                case ModulesEnum.ModuleStats:
                {
                    var s = new ConfigModuleBase<StatsModuleSettings>();
                    element = new ModuleControl(s, "Stats Module Settings");
                    s.Settings = Settings.StatsModule;
                }
                    break;
                case ModulesEnum.ModuleTimers:
                {
                    var s = new ConfigModuleBase<TimersModuleSettings>();
                    element = new ModuleControl(s, "Timers Module Settings");
                    s.Settings = Settings.TimersModule;
                }
                    break;
            }

            if(element != null)
                LoadOverlay(element);
        }

        private void AddModuleExecuted(object obj)
        {
            if(_selectedModuleToAdd == null) return;

            ModulesList.Add(_selectedModuleToAdd.Value);
            AvailableModulesList.Remove(_selectedModuleToAdd.Value);
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
    }
}
