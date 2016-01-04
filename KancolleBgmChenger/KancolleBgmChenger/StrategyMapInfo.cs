using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KancolleBgmChenger
{
    /// <summary>
    /// 作戦MAPで取得した情報を保持するためのクラス
    /// </summary>
    public class StrategyMapInfo
    {
        public int PointType { get; set; }  //次に進む地点の種類

        public StrategyMapInfo()
        {
        }
    }

}
