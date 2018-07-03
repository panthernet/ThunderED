using System.Collections.Generic;
using System.ComponentModel;

namespace TED_ConfigEditor.Classes
{
    public interface IValidatable: IDataErrorInfo
    {
       string Validate(bool sub = false);
    }
}
