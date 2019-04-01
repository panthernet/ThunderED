using System;

namespace ThunderED.Classes
{
    /// <summary>
    /// Specifies that underlying property is a list containing mixed type values
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class MixedListAttribute: Attribute
    {
    }
}
