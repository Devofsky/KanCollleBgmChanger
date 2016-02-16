using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Grabacr07.KanColleWrapper;
using System.Timers;
using Microsoft.Win32;

#if DEBUG
    using System.Threading;
#endif


namespace KancolleBgmChenger
{
    /// <summary>
    /// UserControl1.xaml の相互作用ロジック
    /// </summary>
    public partial class UiBgmChanger : UserControl
    {
        //---------メンバ変数-----------------------

        private MediaPlayer m_player = null;                                     //メディアプレイヤーのインスタンス用
        private System.Timers.Timer m_timerFadeOut;                              //音楽再生用(フェードアウト)のタイマ
        private System.Timers.Timer m_timerBgmChange;                            //音楽再生用(音楽変更)のタイマ
        public Uri UriCurrentBgm;                                               //メディアプレイヤーが参照する現在再生しているBGMのURI
        private double defaultVolume = VALUE_BGM_VOLUME_DEFAULT;               //音楽再生時のデフォルト音量(0.0-1.0 デフォルト0.5)

        private List<Bgm> m_bgmList = null;                              //BGMリスト
        private List<Bgm> m_defaultBgmList = null;                       //BGMリスト(デフォルト)設定読み込みに失敗したときや、デフォルトのリストの復元に使用
        private List<Bgm> m_cpyPstBuffbgmList = null;                    //BGMリスト(コピー&ペースト用のバッファ)
        private List<EndPointPath> m_endPointPathList;                     //APIリスト
        private BgmSetting m_bgmSetting = null;

        private bool m_isBgMFaiding = false;                            //音量変更のクリティカルセクション用
        //--------定数-----------------------------
        const uint TIME_BGM_CHANGE_IMMEDIATELY = 50;
        const uint TIME_BGM_CHANGE_NORMAL = 2000;
        const uint TIME_BGM_CHANGE_RESULT = 5000;
        const string PATH_BGM_LIST_XML = "BgmSetting.xml";
        const double VALUE_BGM_VOLUME_DEFAULT = 0.5;
        const double VALUE_BGM_VOLUME_MUTE = 0.0;
        const uint NUM_PLAYLIST = 5;



        KanColleProxy Proxy = KanColleClient.Current.Proxy;

        public UiBgmChanger()
        {
#if DEBUG
            //プロセスにアタッチできるよう待機
            Thread.Sleep(10000);
#endif
            //------初期化------------------------------------
            UriCurrentBgm = null;
            //------インスタンスの作成-------------------------
            m_bgmList = new List<Bgm>();
            m_defaultBgmList = new List<Bgm>(); 
            m_cpyPstBuffbgmList = new List<Bgm>();
            m_player = new MediaPlayer();
            m_endPointPathList = new List<EndPointPath>();
            m_bgmSetting = new BgmSetting(NUM_PLAYLIST);

            //------タイマの作成-------------------------------
            m_player.MediaEnded += new EventHandler(onBgmEnded);           // メディア再生が完了したとき用イベント

            m_timerFadeOut = new System.Timers.Timer(100);                 // 音声フェードアウト用のタイマ、イベント
            m_timerFadeOut.Elapsed += onTimedEventFadeOut;
            m_timerFadeOut.AutoReset = true;

            m_timerBgmChange = new System.Timers.Timer(2000);//初期値      // BGM切り替え用のタイマ、イベント
            m_timerBgmChange.Elapsed += onTimedEventPlayBgm;
            m_timerBgmChange.AutoReset = false;

            //コンポーネントの初期化
            InitializeComponent();
            
            //デフォルトBGMリストを読み込み
            loadDefaultBgmList();

            //コピー&ペースト用のBMGリストのインスタンス作成
            createCpyPstBgmList();
            //Bgmドロップダウンリストの初期化
            for (uint x = 0; x < NUM_PLAYLIST;x++)
            {
                comboBoxBgmList.Items.Add("BgmList" + x.ToString());
            }

            //BGMリストのインスタンス作成
            resumeBgmList();
            comboBoxBgmList.SelectedIndex = 0;
            //XMLからBGMリストを読み込み
            if (!loadBgmListFromXml())
            {
                //失敗時でもBGMリスト構造体のインスタンスは作成する
                refleshBgmList();
            }

            //エンドポイントにBGMとコールバック関数を登録
            registBgmToEndpoint();

            //ListViewの表示更新
            updateBgmListView();

            //エンドポイントの監視を開始
            startWaitEndPoint();

        }

        ~UiBgmChanger()
        {
            //オブジェクトの廃棄
            m_bgmList = null;
            m_defaultBgmList = null;
            m_player = null;
            m_endPointPathList = null;
        }


        /// <summary>
        /// エンド ポイントにBGMとコールバック関数を登録する
        /// </summary>
        private void registBgmToEndpoint()
        {
            //次に進む地点の情報保持用クラス作成
            StrategyMapInfo strategyMapInfo = new StrategyMapInfo();

            //母港APIの登録
            m_endPointPathList.Add(new EndPointPath(0, "/kcsapi/api_port/port", new AnalysisEndPointDefault(new List<Bgm> { 
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MOTHER_BASE))
                                                                                                    }, ChengeBgm)));
            //昼戦APIの登録
            m_endPointPathList.Add(new EndPointPath(1, "/kcsapi/api_req_sortie/battle", new AnalysisEndPointBattle(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BATTLE)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BOSS_BATTLE))
                                                                                                    }, ChengeBgm
                                                                                                    , strategyMapInfo)));
            //昼戦API(航空戦)の登録
            m_endPointPathList.Add(new EndPointPath(14, "/kcsapi/api_req_sortie/airbattle", new AnalysisEndPointBattle(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BATTLE)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BOSS_BATTLE))
                                                                                                    }, ChengeBgm
                                                                                        , strategyMapInfo)));
            //昼戦API(長距離空襲戦)の登録
            m_endPointPathList.Add(new EndPointPath(15, "/kcsapi/api_req_sortie/ld_airbattle", new AnalysisEndPointBattle(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BATTLE)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BOSS_BATTLE))
                                                                                                    }, ChengeBgm
                                                                                        , strategyMapInfo)));

            //昼戦API(連合艦隊)の登録
            m_endPointPathList.Add(new EndPointPath(8, "/kcsapi/api_req_combined_battle/airbattle", new AnalysisEndPointBattle(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BATTLE)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BOSS_BATTLE))
                                                                                                    }, ChengeBgm
                                                                                        , strategyMapInfo)));

            m_endPointPathList.Add(new EndPointPath(9, "/kcsapi/api_req_combined_battle/battle", new AnalysisEndPointBattle(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BATTLE)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BOSS_BATTLE))
                                                                                                    }, ChengeBgm
                                                                                        , strategyMapInfo)));

            m_endPointPathList.Add(new EndPointPath(10, "/kcsapi/api_req_combined_battle/battle_water", new AnalysisEndPointBattle(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BATTLE)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BOSS_BATTLE))
                                                                                                    }, ChengeBgm
                                                                                        , strategyMapInfo)));

            m_endPointPathList.Add(new EndPointPath(16, "/kcsapi/api_req_combined_battle/ld_airbattle", new AnalysisEndPointBattle(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BATTLE)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BOSS_BATTLE))
                                                                                                    }, ChengeBgm
                                                                                        , strategyMapInfo)));


            //演習APIの登録
            m_endPointPathList.Add(new EndPointPath(2, "/kcsapi/api_req_practice/battle", new AnalysisEndPointDefault(new List<Bgm> { 
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_PRACTICE_DAY_BATTLE))
                                                                                                    }, ChengeBgm)));
            //作戦MAPAPIの登録
            m_endPointPathList.Add(new EndPointPath(3, "/kcsapi/api_req_map/start", new AnalysisEndPointStrategytMap(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MAP))
                                                                                                    }, ChengeBgm
                                                                                                    , strategyMapInfo)));
            m_endPointPathList.Add(new EndPointPath(4, "/kcsapi/api_req_map/next", new AnalysisEndPointStrategytMap(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MAP)) 
                                                                                                    }, ChengeBgm
                                                                                                    , strategyMapInfo)));
            //戦果API(通常戦闘)の登録
            m_endPointPathList.Add(new EndPointPath(5, "/kcsapi/api_req_sortie/battleresult", new AnalysisEndPointResult(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_S)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_A)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_B)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_C)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_D)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_E))
                                                                                                    }, ChengeBgm)));

            //戦果API(連合艦隊)の登録
            m_endPointPathList.Add(new EndPointPath(11, "/kcsapi/api_req_combined_battle/battleresult", new AnalysisEndPointResult(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_S)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_A)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_B)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_C)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_D)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_E))
                                                                                                    }, ChengeBgm)));
            //戦果API(演習)の登録
            m_endPointPathList.Add(new EndPointPath(5, "/kcsapi/api_req_practice/battle_result", new AnalysisEndPointResult(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_S)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_A)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_B)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_C)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_D)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_E))
                                                                                                    }, ChengeBgm)));
            //夜戦(通常)APIの登録
            m_endPointPathList.Add(new EndPointPath(7, "/kcsapi/api_req_battle_midnight/battle", new AnalysisEndPointBattle(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MIDNIGHT_BATTLE)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MIDNIGHT_BOSS_BATTLE))
                                                                                                    }, ChengeBgm
                                                                                                    , strategyMapInfo)));
            //夜戦(特殊夜戦)APIの登録
            m_endPointPathList.Add(new EndPointPath(7, "/kcsapi/api_req_battle_midnight/sp_midnight", new AnalysisEndPointBattle(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MIDNIGHT_BATTLE)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MIDNIGHT_BOSS_BATTLE))
                                                                                                    }, ChengeBgm
                                                                                        , strategyMapInfo)));

            //夜戦(連合艦隊)APIの登録
            m_endPointPathList.Add(new EndPointPath(12, "/kcsapi/api_req_combined_battle/midnight_battle", new AnalysisEndPointBattle(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MIDNIGHT_BATTLE)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MIDNIGHT_BOSS_BATTLE))
                                                                                                    }, ChengeBgm
                                                                                        , strategyMapInfo)));

            m_endPointPathList.Add(new EndPointPath(13, "/kcsapi/api_req_combined_battle/sp_midnight", new AnalysisEndPointBattle(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MIDNIGHT_BATTLE)),
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MIDNIGHT_BOSS_BATTLE))
                                                                                                    }, ChengeBgm
                                                                                        , strategyMapInfo)));

            //夜戦(演習)APIの登録
            m_endPointPathList.Add(new EndPointPath(7, "/kcsapi/api_req_practice/midnight_battle", new AnalysisEndPointDefault(new List<Bgm> {
                                                                                                    m_bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_PRACTICE_MIDNIGHT_BATTLE)) 
                                                                                                    }, ChengeBgm)));

            
        }

        /// <summary>
        /// BGMリストをXMLに保存する
        /// </summary>
        private bool saveBgmListToXml()
        {
            bool ret = false;

            //KanColleViewer.exeの存在するディレクトリと、設定ファイル名を結合
            string filePath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location),
                PATH_BGM_LIST_XML);

            if (checkBoxMute.HasContent)
            {//nullじゃなかったらboolでキャスト
                m_bgmSetting.isMuteAtLaunch = (bool)checkBoxMute.IsChecked;
            }
            else
            {
                m_bgmSetting.isMuteAtLaunch = false;            
            }

            //音量の保存
            m_bgmSetting.volume = sliderVolume.Value / 100;//スライダーの値(0-100) 音量の値(0-1)

            try
            {
                //XmlSerializerオブジェクトを作成
                //オブジェクトの型を指定する
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(BgmSetting));
                //書き込むファイルを開く（UTF-8 BOM無し）
                System.IO.StreamWriter sw = new System.IO.StreamWriter(filePath, false, new System.Text.UTF8Encoding(false));
                //シリアル化し、XMLファイルに保存する
                serializer.Serialize(sw, m_bgmSetting);
                //ファイルを閉じる
                sw.Close();

                ret = true;
            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message.ToString());
            }

            return ret;

        }

        /// <summary>
        /// BGMリストをXMLから読み込む
        /// </summary>
        public bool loadBgmListFromXml()
        {
            bool ret = false;

            //KanColleViewer.exeの存在するディレクトリと、設定ファイル名を結合
            string filePath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location),
                PATH_BGM_LIST_XML);

            try
            {
                //XmlSerializerオブジェクトを作成
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(BgmSetting));
                //読み込むファイルを開く
                System.IO.StreamReader sr = new System.IO.StreamReader(filePath, new System.Text.UTF8Encoding(false));
                //XMLファイルから読み込み、逆シリアル化する
                m_bgmSetting = (BgmSetting)serializer.Deserialize(sr);
                //ファイルを閉じる
                sr.Close();

                if (refleshBgmList())
                {
                    //起動時のMuteチェックボックスの状態を設定
                    setMuteCheckBox(m_bgmSetting.isMuteAtLaunch);
                    //音量設定
                    defaultVolume = m_bgmSetting.volume;
                    sliderVolume.Value = defaultVolume * 100; //スライダーの値(0-100) 音量の値(0-1)
 
                    ret = true;
                }
                else
                { 
                    //do nothing 失敗
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message.ToString());
            }

            return ret;
        }

        /// <summary>
        /// BGMリストを構造体から読み込む
        /// </summary>
        public bool loadBgmListFromBgmSetting()
        {
            bool ret = false;

            try
            {
                ret = refleshBgmList();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message.ToString());
            }

            return ret;
        }

        /// <summary>
        /// BGMリストを構造体で更新する
        /// </summary>
        public bool refleshBgmList()
        {
            bool ret = false;

            try
            {
                //現行のBgmリストの参照先を、BGMリスト構造体から読み込んだ値に変更したいもろもろの処理

                BgmSetting.BgmPlayList bgmPlayList = m_bgmSetting.BgmPlayLists[comboBoxBgmList.SelectedIndex];
                if (bgmPlayList.Bgms.Count == 0)
                {//BGMリスト構造体が空っぽの場合
                    for (int i = 0; i < m_bgmList.Count; i++)
                    {
                        //BGMリストの参照先の値を、デフォルトのBGMリストの値に変えておく
                        Bgm.Copy(m_bgmList[i], m_defaultBgmList[i]);
                        //BGMリスト構造体にもインスタンスを作っておく(参照渡しではない)
                        m_bgmSetting.BgmPlayLists[comboBoxBgmList.SelectedIndex].Bgms.Add(new Bgm(m_defaultBgmList[i]));
                    }
                }
                else
                {//BGMリスト構造体に何らかのBGMリストがあった場合
                    for (int i = 0; i < m_bgmList.Count; i++)
                    {
                        Bgm bgm = bgmPlayList.Bgms.Find(x => x.ID.Equals(m_bgmList[i].ID));
                        if (bgm == null)
                        {   //BGMリスト構造体と現行のBGMリストとで、一致するBGMがなかった場合
                            //見つからない場合は、BGMリストの参照先の値を、デフォルトのBGMリストの値に変えておく
                            Bgm.Copy(m_bgmList[i], m_defaultBgmList[i]);
                            //BGMリスト構造体にもインスタンスを作っておく(参照渡しではない)
                            m_bgmSetting.BgmPlayLists[comboBoxBgmList.SelectedIndex].Bgms.Add(new Bgm(m_defaultBgmList[i]));
                        }
                        else
                        {   //BGMリスト構造体と現行のBGMリストで一致するBGMがあった場合は、
                            //BGMリストの参照先の値を、見つかったBGMのものに変えておく
                            Bgm.Copy(m_bgmList[i], bgm);
                        }
                    }
                }
                //ListViewの表示更新
                updateBgmListView();
                ret = true;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message.ToString());
            }

            return ret;
        }

        /// <summary>
        /// デフォルトBGMリストを復元する
        /// </summary>
        private void resumeBgmList()
        {
            foreach (Bgm bgm in m_defaultBgmList)
            {//デフォルトBGMセットから検索
                if (m_bgmList.Find(x => x.ID.Equals(bgm.ID)) == null)
                {//もし、デフォルトBGMセットのうち、ロードしたBGMセットに含まれていないものがあった場合
                    //現行のBGMリストに追加しておく。
                    m_bgmList.Add(new Bgm(bgm));
                }
            }
        }
        /// <summary>
        /// BGMリスト(コピー&ペースト用)のインスタンスを作成する
        /// </summary>
        private void createCpyPstBgmList()
        {
            foreach (Bgm bgm in m_defaultBgmList)
            {//デフォルトBGMセットから検索
                if (m_cpyPstBuffbgmList.Find(x => x.ID.Equals(bgm.ID)) == null)
                {//もし、デフォルトBGMセットのうち、ロードしたBGMセットに含まれていないものがあった場合
                    //現行のBGMリストに追加しておく。
                    m_cpyPstBuffbgmList.Add(new Bgm(bgm));
                }
            }
        }

        /// <summary>
        /// デフォルトのBGMリストを読み込む
        /// </summary>
        private void loadDefaultBgmList()
        {
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_MOTHER_BASE, "母港", "", "", TIME_BGM_CHANGE_NORMAL));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_DAY_BATTLE, "昼戦", "", "", TIME_BGM_CHANGE_NORMAL));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_MIDNIGHT_BATTLE, "夜戦", "", "", TIME_BGM_CHANGE_NORMAL));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_DAY_BOSS_BATTLE, "昼戦(ボス戦)", "", MapPointType.POINT_TYPE_BOSS_BATTLE.ToString(), TIME_BGM_CHANGE_NORMAL));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_MIDNIGHT_BOSS_BATTLE, "夜戦(ボス戦)", "", MapPointType.POINT_TYPE_BOSS_BATTLE.ToString(), TIME_BGM_CHANGE_NORMAL));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_MAP, "作戦MAP", "", "", TIME_BGM_CHANGE_NORMAL));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_PRACTICE_DAY_BATTLE, "演習", "", "", TIME_BGM_CHANGE_NORMAL));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_PRACTICE_MIDNIGHT_BATTLE, "演習(夜戦)", "", "", TIME_BGM_CHANGE_NORMAL));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_RESULT_S, "戦果S", "", "S", TIME_BGM_CHANGE_RESULT));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_RESULT_A, "戦果A", "", "A", TIME_BGM_CHANGE_RESULT));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_RESULT_B, "戦果B", "", "B", TIME_BGM_CHANGE_RESULT));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_RESULT_C, "戦果C", "", "C", TIME_BGM_CHANGE_RESULT));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_RESULT_D, "戦果D", "", "D", TIME_BGM_CHANGE_RESULT));
            m_defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_RESULT_E, "戦果E", "", "E", TIME_BGM_CHANGE_RESULT));
            return;
        }

        /// <summary>
        /// エンドポイントの監視を開始
        /// </summary>
        private void startWaitEndPoint()
        {
            foreach (EndPointPath x in m_endPointPathList)
            {
                x.CallbackClass.Regist(Proxy,x.EventPath);
            }
        }

        /// <summary>
        /// ListViewの表示を更新
        /// </summary>
        private void updateBgmListView()
        {
            //リストをクリア
            listViewBgm.Items.Clear();
            foreach (Bgm bgm in m_bgmList)
            {
                listViewBgm.Items.Add(new string[] {bgm.ScheneName,System.IO.Path.GetFileName(bgm.FilePath),bgm.ID.ToString()});
            }
        }

        /// <summary>
        /// 再生中のBGMをフェードアウトして止める
        /// </summary>
        private void fadeOutPlayerVolume()
        {
            if (m_player.Volume >= 0.05)
            {
                //フェードアウト開始。クリティカルセクション
                m_isBgMFaiding = true;
                m_player.Volume = m_player.Volume - 0.05;
            }
            else
            {//フェードアウト完了
                m_player.Stop();
                m_player.Volume = defaultVolume;
                m_timerFadeOut.Enabled = false;
                //クリティカルセクション終了
                m_isBgMFaiding = false;
            }
        }
        /// <summary>
        /// BGMを再生する
        /// </summary>
        private void playBgm()
        {
            m_player.Close();
            m_player.Open(UriCurrentBgm);
            m_player.Volume = defaultVolume;

            if (checkBoxMute.HasContent && (bool)checkBoxMute.IsChecked)
            {//Muteにチェック入っていたら再生しない
            }
            else
            {//それ以外は再生する
                m_player.Play();            
            }
        }

        /// <summary>
        /// フェードアウトを開始するイベント
        /// </summary>
        private void onTimedEventFadeOut(Object source, System.Timers.ElapsedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => { fadeOutPlayerVolume(); }));
        }
        /// <summary>
        /// BGMを再生するイベント
        /// </summary>
        private void onTimedEventPlayBgm(Object source, System.Timers.ElapsedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => { playBgm(); }));
        }

        /// <summary>
        /// BGMが終わった時に呼ばれるイベント
        /// </summary>
        private void onBgmEnded(object sender, EventArgs e)
        {
            //BGMすぐ変更する
            m_timerBgmChange.Interval = TIME_BGM_CHANGE_IMMEDIATELY;
            //BGMを変更する(再生しなおし)
            m_timerBgmChange.Enabled = true;
        }

        /// <summary>
        /// ListViewをダブルクリックされた際に呼ばれるイベント
        /// クリックされたBGMを変更する
        /// </summary>
        private void listView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var listView = (ListView)sender;
            var item = listView.ContainerFromElement((DependencyObject)e.OriginalSource) as ListViewItem;
            if (item != null)
            {
                uint id;
                var temp = (string[])item.DataContext;
                if (uint.TryParse(temp[2].ToString(), out id))
                {//数値変換成功
                    readMediaFile(id);
                }
                else
                {//数値への変換失敗
                
                }
            }
            else
            {
                //do nothing
            }
        }

        /// <summary>
        /// ディレクトリ内から音楽ファイルを選択し、BGMを更新する
        /// </summary>
        private void readMediaFile(uint ID)
        {
            //ダイアログの準備
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "ファイルを選択してください";
            ofd.FileName = "";
            ofd.DefaultExt = "*.mp3";
            ofd.Filter = "音楽ファイル|*.mp3;*.m4a";  // ファイルの種類
            ofd.FilterIndex = 0;            // 上記フィルタのどれを初期表示するか(今回はゼロなのでTXTが初期表示される)

            ofd.RestoreDirectory = true;    // ダイアログボックスを閉じる前に、現在のディレクトリを復元する
            ofd.CheckFileExists = true;     // 存在しないファイルを指定すると警告
            ofd.CheckPathExists = true;     // 存在しないパスを指定すると警告
            ofd.DereferenceLinks = true;    // ショートカットを選択した場合、参照先のパスを取得する(Falseだとショートカットファイルそのものを取得)
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    //BGMリストからIDでサーチ
                    Bgm bgm = m_bgmSetting.BgmPlayLists[comboBoxBgmList.SelectedIndex].Bgms.Find(x => x.ID.Equals(ID));
                    //ファイル名の更新
                    bgm.FilePath = ofd.FileName;
                    //参照先のURIの更新
                    bgm.refreshUri();
                    //Bgmリストの参照先の値を更新
                    Bgm.Copy(m_bgmList.Find(x => x.ID.Equals(ID)), bgm);
                    //ListViewの表示更新
                    updateBgmListView();
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message.ToString());
                }
            }
        }

        /// <summary>
        /// BGMリストをXMLから読み込む保存するボタンを押されたときのイベント
        /// </summary>
        private void butonLoadBgmSetting_Click(object sender, RoutedEventArgs e)
        {
            if (saveBgmListToXml())
            {//成功
                MessageBox.Show("Bgm設定を読み込みました。");
            }
            else
            {//失敗
            }
        }


        /// <summary>
        /// BGMリストをXMLに保存するボタンを押されたときのイベント
        /// </summary>
        private void buttonSaveBgmSettiing_Click(object sender, RoutedEventArgs e)
        {
            if(saveBgmListToXml())
            {//成功
                MessageBox.Show("Bgm設定を保存しました。");
            }
            else
            {//失敗
            }
        }

        /// <summary>
        /// Muteのチェックボックスにチェックが入った際のイベント
        /// </summary>
        private void checkBoxMute_Checked(object sender, RoutedEventArgs e)
        {
            //BGMを停止
            m_player.Stop();
        }

        /// <summary>
        /// Muteのチェックボックスにチェックが外れた際のイベント
        /// </summary>
        private void checkBoxMute_UnChecked(object sender, RoutedEventArgs e)
        {
            //BGMも開始
            m_player.Play();
        }

        /// <summary>
        /// 外部クラスから、BGMの変更を起こす際の関数
        /// </summary>
        public void ChengeBgm(Bgm bgm)
        {
            if (UriCurrentBgm != bgm.URI)
            {//もし、現在のBGMと異なったらBGM変更
                UriCurrentBgm = bgm.URI;
                m_timerBgmChange.Interval = bgm.Interval;
                //直前のBGMのフェードアウトさせる
                m_timerFadeOut.Enabled = true;
                //BGMを変更する
                m_timerBgmChange.Enabled = true;
            }
            else
            { //そうでなかったら何もしない
                //do nothing
            }
        }

        /// <summary>
        /// Muteのチェックボックスを操作する
        /// </summary>
        private void setMuteCheckBox(bool isMute)
        {
            if (isMute)
            {//Muteにする
                //チェックボックスを操作
                checkBoxMute.IsChecked = true;
                //ボリュームをMUTEにする。
                defaultVolume = VALUE_BGM_VOLUME_MUTE;
            }
            else
            {
                //do nothing

            }

        }

        /// <summary>
        /// XMLにBGMリストを保存するときに使用するクラス
        /// </summary>
        public class BgmSetting
        {
            public class BgmPlayList
            {
                [System.Xml.Serialization.XmlAttribute("ID")]
                public uint id { get; set; }

                [System.Xml.Serialization.XmlAttribute("name")]
                public string Name { get; set; }

                [System.Xml.Serialization.XmlArray("Bgms")]
                [System.Xml.Serialization.XmlArrayItem("Bgm")]
                public List<Bgm> Bgms;

                public BgmPlayList()
                {
                    Bgms = new List<Bgm>();
                }

                public BgmPlayList(uint _id)
                {
                    id = _id;
                    Bgms = new List<Bgm>();

                }

                // コピーを作成するメソッド
                public BgmPlayList Clone()
                {
                    return (BgmPlayList)MemberwiseClone();
                }

            }

            [System.Xml.Serialization.XmlArray("BgmPlayLists")]
            [System.Xml.Serialization.XmlArrayItem("BgmPlayList")]
            public List<BgmPlayList> BgmPlayLists;

            [System.Xml.Serialization.XmlElement("isMuteAtLaunch")]
            public bool isMuteAtLaunch;

            [System.Xml.Serialization.XmlElement("Volume")]
            public double volume;

            public BgmSetting()
            {
                BgmPlayLists = new List<BgmPlayList>();
            }

            public BgmSetting(uint list_num)
            {
                BgmPlayLists = new List<BgmPlayList>();
                for (uint x = 0;x < list_num; x++)
                {
                    BgmPlayLists.Add(new BgmPlayList(x));
                }
            }
        }



        private void comboBoxBgmList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            loadBgmListFromBgmSetting();
        }

        private void buttonCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //現行のBGMリストを選んでBGMリスト(バッファ)にコピー
                for (int i = 0; i < m_bgmList.Count; i++)
                {
                    Bgm.Copy(m_cpyPstBuffbgmList[i], m_bgmList[i]);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
            }
        }

        private void buttonPaste_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //BGMリスト(バッファ)からBGMリスト構造体の選択中のBGMリストにコピー
                for (int i = 0; i < m_bgmList.Count; i++)
                {
                    Bgm.Copy(m_bgmSetting.BgmPlayLists[comboBoxBgmList.SelectedIndex].Bgms[i],m_cpyPstBuffbgmList[i]);
                }
                //BGMリスト構造体を読み込む
                loadBgmListFromBgmSetting();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
            }


        }

        private void sliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //基準音量を変更
            defaultVolume = sliderVolume.Value / 100;//スライダーの値(0-100) 音量の値(0-1)
 
            if(m_isBgMFaiding)
            {   //フェードアウト中は、直接音量を変更しない。(フェードアウト終了して次にBGM流れるときに変更される)
            }
            else
            {   //それ以外は、直接プレイヤーの音量を変更する
                m_player.Volume = defaultVolume;

            }
        }

    }

}
