using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KancolleBgmChenger
{
    /// <summary>
    /// Bgmのタイプ
    /// </summary>
    public enum Bgmtype : uint
    {
        TYPE_DAY_BATTLE = 0,
        TYPE_PRACTICE_DAY_BATTLE,
        TYPE_MOTHER_BASE,
        TYPE_MAP,
        TYPE_RESULT_S,
        TYPE_RESULT_A,
        TYPE_RESULT_B,
        TYPE_RESULT_C,
        TYPE_RESULT_D,
        TYPE_RESULT_E,
        TYPE_MIDNIGHT_BATTLE,
        TYPE_PRACTICE_MIDNIGHT_BATTLE,
    }

}
