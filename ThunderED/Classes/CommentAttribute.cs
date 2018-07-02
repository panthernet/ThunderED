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
}
