using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace ThunderED.Thd
{
    public class ThdTimer: INotifyPropertyChanged, IIdentifiable
    {
        private string _location = "-";
        private int _type;
        private string _owner = "-";
        private DateTime _date;

        public long Id { get; set; }

        [Required]
        [Range(1, 2, ErrorMessage = "Invalid value")]
        public int Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        [Required]
        [Range(1, 4, ErrorMessage = "Invalid value")]
        public int Stage { get; set; }

        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged(); }
        }

        public string Owner
        {
            get => _owner;
            set { _owner = value; OnPropertyChanged(); }
        }

        [Required]
        public DateTime Date
        {
            get => _date;
            set { _date = value; OnPropertyChanged(); }
        }

        public string Notes { get; set; }
        public string TimerChar { get; set; }
        public int Announce { get; set; }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    public class ThdTimerRf : ThdTimer
    {
        private int _intHour;
        private int _intMinute;
        private int _intDay;

        public ThdTimerRf()
        {
            Date = DateTime.MinValue;
        }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Invalid value")]
        public int IntHour
        {
            get => _intHour;
            set
            {
                _intHour = value; OnPropertyChanged();
            }
        }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Invalid value")]
        public int IntMinute
        {
            get => _intMinute;
            set
            {
                _intMinute = value; OnPropertyChanged();
            }
        }

        [Required]
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
