using System.Text;

namespace ThunderED.Classes
{
    public class WebLysItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public bool IsCategory { get; set; }

        public WebLysItem() {}

        public WebLysItem(string name, string value, bool isCategory = false)
        {
            Name = name;
            Value = value;
            IsCategory = isCategory;
        }

    }
}
