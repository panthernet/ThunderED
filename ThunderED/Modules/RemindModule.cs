using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class RemindModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Remind;

        public override Task Run(object prm)
        {
            return base.Run(prm);
        }
    }
}
