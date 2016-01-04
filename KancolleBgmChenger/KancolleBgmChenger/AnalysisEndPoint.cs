using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grabacr07.KanColleViewer.Composition;
using Grabacr07.KanColleWrapper;
using Grabacr07.KanColleWrapper.Models.Raw;
using Nekoxy;
using System.Reactive.Linq;

namespace KancolleBgmChenger
{
    /// <summary>
    /// KanColleViewerからもらうエンドポイントからのセッション通信を受けて処理する関数の基底クラス
    /// </summary>
    public class AnalysisEndPoint
    {
        //メンバ変数
        private List<Bgm> m_bgmList;
        public List<Bgm> BgmList { get { return m_bgmList; } }
        public delegate void ChengeBgmFunc(Bgm bgm);
        public ChengeBgmFunc callBack;

        public AnalysisEndPoint(List<Bgm> _bgmList,ChengeBgmFunc _func)
        {
            //ID
            m_bgmList = _bgmList;
            callBack = _func;
        }
        public AnalysisEndPoint()
        {
        }

        virtual public void Exec()
        {
        }

        virtual public void Regist(KanColleProxy Proxy, string event_path)
        {
        }

    }

    /// <summary>
    /// KanColleViewerからもらうエンドポイントからのセッション通信を受けて処理する関数(デフォルト)のクラス
    /// </summary>
    public class AnalysisEndPointDefault : AnalysisEndPoint
    {
        private List<Bgm> m_bgmList;

        public AnalysisEndPointDefault(List<Bgm> _bgmList,ChengeBgmFunc _func)
        {
            //ID
            m_bgmList = _bgmList;
            callBack = _func;
        }

        public void Exec(object x)
        {
            //BGM変更    
            callBack(m_bgmList[0]);
        }

        override public void Regist(KanColleProxy Proxy, string event_path)
        {
            Proxy.SessionSource.Where(x => x.Request.PathAndQuery.StartsWith(event_path)).TryParse<NullClass>().Subscribe(x => this.Exec(x));
        }

    }
    /// <summary>
    /// KanColleViewerからもらうエンドポイントからのセッション通信を受けて処理する関数(戦果確認用)のクラス
    /// </summary>
    public class AnalysisEndPointResult : AnalysisEndPoint
    {
        private List<Bgm> m_bgmList;

        public AnalysisEndPointResult(List<Bgm> _bgmList, ChengeBgmFunc _func)
        {
            //ID
            m_bgmList = _bgmList;
            callBack = _func;
        }

        public void Exec(string result)
        {
            if (result == null)
            {//文字がnull
            }
            else
            {
                //登録しておいたKeyと一致するものを検索
                Bgm bgm = m_bgmList.Find(x => x.Key.Equals(result));
                if (bgm != null)
                {
                    callBack(bgm);

                }
                else
                {
                    //do nothing 
                }

            }
        }

        override public void Regist(KanColleProxy Proxy, string event_path)
        {
            Proxy.SessionSource.Where(x => x.Request.PathAndQuery.StartsWith(event_path)).TryParse<BattleResult>().Subscribe(x => this.Exec(x.Data.api_win_rank));
        }

    }

    /// <summary>
    /// KanColleViewerからもらうエンドポイントからのセッション通信を受けて処理する関数(作戦MAP)のクラス
    /// </summary>
    public class AnalysisEndPointStrategytMap : AnalysisEndPoint
    {
        private List<Bgm> m_bgmList;
        private StrategyMapInfo strategyMapInfo;

        public AnalysisEndPointStrategytMap(List<Bgm> _bgmList, ChengeBgmFunc _func, StrategyMapInfo _strategyMapInfo)
        {
            //ID
            m_bgmList = _bgmList;
            callBack = _func;
            strategyMapInfo = _strategyMapInfo;
        }

        public void Exec(int api_color_no)
        {
            //次に進む地点の情報を登録
            strategyMapInfo.PointType = api_color_no;
            //BGM変更    
            callBack(m_bgmList[0]);
        }


        override public void Regist(KanColleProxy Proxy, string event_path)
        {
            Proxy.SessionSource.Where(x => x.Request.PathAndQuery.StartsWith(event_path)).TryParse<StrategyMap>().Subscribe(x => this.Exec(x.Data.api_color_no));
        }

    }

    /// <summary>
    /// KanColleViewerからもらうエンドポイントからのセッション通信を受けて処理する関数(通常戦闘用)のクラス
    /// </summary>
    public class AnalysisEndPointBattle : AnalysisEndPoint
    {
        private List<Bgm> m_bgmList;
        private StrategyMapInfo strategyMapInfo;

        public AnalysisEndPointBattle(List<Bgm> _bgmList, ChengeBgmFunc _func, StrategyMapInfo _strategyMapInfo)
        {
            //ID
            m_bgmList = _bgmList;
            callBack = _func;
            strategyMapInfo = _strategyMapInfo;
        }

        public void Exec(object xxx)
        {
            //到着地点の種別を取得
            int point_type = strategyMapInfo.PointType;
            Bgm bgm;


            if (point_type == (int)MapPointType.POINT_TYPE_BOSS_BATTLE)
            {//到着地点がボス戦だった場合
                //登録BGMからボス戦BGMを検索する
                bgm = m_bgmList.Find(x => x.Key.Equals(MapPointType.POINT_TYPE_BOSS_BATTLE.ToString()));
            }
            else
            {//そのほかの場合(基本的に通常戦闘)
                //登録BMGから通常戦闘BGM(Keyが空)を検索する
                bgm = m_bgmList.Find(x => x.Key.Equals(""));
            }

            if (bgm != null)
            {
                callBack(bgm);

            }
            else
            {
                //do nothing 
            }
        }


        override public void Regist(KanColleProxy Proxy, string event_path)
        {
            Proxy.SessionSource.Where(x => x.Request.PathAndQuery.StartsWith(event_path)).TryParse<NullClass>().Subscribe(x => this.Exec(x));
        }

    }
}
