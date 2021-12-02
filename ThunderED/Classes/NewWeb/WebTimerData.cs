using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using ThunderED.Helpers;
using ThunderED.Json.Internal;

namespace ThunderED.Classes
{
    public class WebTimerData: INotifyPropertyChanged
    {
        private int _type;
        //private int _stage;
        private string _location = "-";
        private string _owner = "-";
        private DateTime _date;

        [System.ComponentModel.DataAnnotations.Required]
        [Range(1, 2, ErrorMessage = "Invalid value")]
        public int Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged();}
        }

        [System.ComponentModel.DataAnnotations.Required]
        [Range(1, 4, ErrorMessage = "Invalid value")]
        public int Stage { get; set; }

        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged();}
        }

        public string Owner
        {
            get => _owner;
            set { _owner = value; OnPropertyChanged();}
        }

        [System.ComponentModel.DataAnnotations.Required]
        public DateTime Date
        {
            get => _date;
            set { _date = value; OnPropertyChanged();}
        }

        public string Notes { get; set; }

        public long Id { get; set; }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        public void FromItem(TimerItem entry)
        {
            Type = entry.timerType;
            Stage = entry.timerStage;
            Location = entry.timerLocation;
            Owner = entry.timerOwner;
            Date = entry.GetDateTime() ?? DateTime.MinValue;
            Notes = entry.timerNotes;
            Id = entry.Id;
        }
    }

    public class WebTimerDataRf : WebTimerData
    {
        private int _intHour;
        private int _intMinute;
        private int _intDay;

        public WebTimerDataRf()
        {
            Date = DateTime.MinValue;
        }

        [System.ComponentModel.DataAnnotations.Required]
        [Range(0, int.MaxValue, ErrorMessage = "Invalid value")]
        public int IntHour
        {
            get => _intHour;
            set
            {
                _intHour = value; OnPropertyChanged();
            }
        }

        [System.ComponentModel.DataAnnotations.Required]
        [Range(0, int.MaxValue, ErrorMessage = "Invalid value")]
        public int IntMinute
        {
            get => _intMinute;
            set
            {
                _intMinute = value; OnPropertyChanged();
            }
        }

        [System.ComponentModel.DataAnnotations.Required]
        [Range(0, int.MaxValue, ErrorMessage = "Invalid value")]
        public int IntDay
        {
            get => _intDay;
            set
            {
                _intDay = value; OnPropertyChanged();
            }
        }

        public void PushDate()
        {
            Date = DateTime.UtcNow.AddDays(_intDay).AddHours(_intHour).AddMinutes(_intMinute);
        }
    }
}
