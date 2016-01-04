using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KancolleBgmChenger
{
    /// <summary>
    /// 作戦MAPの地点の種類
    /// </summary>
    public enum MapPointType : int
    {
        POINT_TYPE_START = 0,//スタート
        POINT_TYPE_RESOURCE = 2,//緑（資源）
        POINT_TYPE_WHIRLPOOL = 3,//うずしお
        POINT_TYPE_NORMAL_BATTLE = 4,//赤（戦闘）
        POINT_TYPE_BOSS_BATTLE = 5,//ボス
    }
}
