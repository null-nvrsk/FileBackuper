using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileBackuper.Core
{
    [Flags]
    public enum GroupType
    {
        None = 0,
        Image = 1,
        Video = 2
    }
}
