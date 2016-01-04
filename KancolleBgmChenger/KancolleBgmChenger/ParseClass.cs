using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KancolleBgmChenger
{
    //戦果結果のデータをパースする用のクラス
    public class BattleResult
    {
        public string api_win_rank { get; set; }
    }

    //作戦MAPのデータをパースする用のクラス
    public class StrategyMap
    {
        public int api_color_no { get; set; }
    }

    //デフォルトのクラス(何もパースしない)
    public class NullClass
    {
    }

}
