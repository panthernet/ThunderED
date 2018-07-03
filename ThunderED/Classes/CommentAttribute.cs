using System;

#if EDITOR
namespace TED_ConfigEditor.Classes
#else
namespace ThunderED.Classes
#endif
{
    public class CommentAttribute: Attribute
    {
        public string Comment { get; set; }

        public CommentAttribute(string comment)
        {
            Comment = comment;
        }
    }

    public class RequiredAttribute: Attribute
    {
    }

    public class PropertyNameAttribute : Attribute
    {
        public string Name { get; set; }

        public PropertyNameAttribute(string name)
        {
            Name = name;
        }
    }

    public class ConfigEntryNameAttribute : Attribute
    {
        public string Name { get; set; }

        public ConfigEntryNameAttribute(string name)
        {
            Name = name;
        }
    }
}
