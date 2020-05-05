using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace ThunderED.Classes
{
    public class WebTimerData: INotifyPropertyChanged
    {
        private int _type;
        private int _stage;
        private string _location;
        private string _owner;
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

        [System.ComponentModel.DataAnnotations.Required]
        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged();}
        }

        [System.ComponentModel.DataAnnotations.Required]
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

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
