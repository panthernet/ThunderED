using System;

namespace ThunderED
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class DBUpgradeAttribute: Attribute
    {
        public string Version { get;}
        public Version VersionNumber { get; }

        public DBUpgradeAttribute(string version)
        {
            Version = version;
            VersionNumber = new Version(version);
        }
    }
}
