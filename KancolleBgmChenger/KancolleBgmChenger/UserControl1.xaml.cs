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



namespace KancolleBgmChenger
{
    /// <summary>
    /// UserControl1.xaml の相互作用ロジック
    /// </summary>
    public partial class UiBgmChanger : UserControl
    {
        //---------メンバ変数-----------------------

        private MediaPlayer player = null;                                     //メディアプレイヤーのインスタンス用
        private System.Timers.Timer timerFadeOut;                              //音楽再生用(フェードアウト)のタイマ
        private System.Timers.Timer timerBgmChange;                            //音楽再生用(音楽変更)のタイマ
        public Uri UriCurrentBgm;                                               //メディアプレイヤーが参照する現在再生しているBGMのURI
        private double defaultVolume = VALUE_BGM_VOLUME_DEFAULT;               //音楽再生時のデフォルト音量(0.0-1.0 デフォルト0.5)

        private List<Bgm> bgmList = null;                              //BGMリスト
        private List<Bgm> defaultBgmList = null;                       //BGMリスト(デフォルト)設定読み込みに失敗したときや、デフォルトのリストの復元に使用
        private List<EndPointPath> endPointPathList;                     //APIリスト

        //--------定数-----------------------------
        const uint TIME_BGM_CHANGE_IMMEDIATELY = 50;
        const uint TIME_BGM_CHANGE_NORMAL = 2000;
        const uint TIME_BGM_CHANGE_RESULT = 5000;
        const string PATH_BGM_LIST_XML = "BgmList.xml";
        const double VALUE_BGM_VOLUME_DEFAULT = 0.5;
        const double VALUE_BGM_VOLUME_MUTE = 0.0;



        KanColleProxy Proxy = KanColleClient.Current.Proxy;

        public UiBgmChanger()
        {
#if DEBUG
            //プロセスにアタッチできるよう待機
            //Thread.Sleep(10000);
#endif
            //------初期化------------------------------------
            UriCurrentBgm = null;
            //------インスタンスの作成-------------------------
            bgmList = new List<Bgm>();
            defaultBgmList = new List<Bgm>(); 
            player = new MediaPlayer();
            endPointPathList = new List<EndPointPath>();

            //------タイマの作成-------------------------------
            player.MediaEnded += new EventHandler(onBgmEnded);           // メディア再生が完了したとき用イベント

            timerFadeOut = new System.Timers.Timer(100);                 // 音声フェードアウト用のタイマ、イベント
            timerFadeOut.Elapsed += onTimedEventFadeOut;
            timerFadeOut.AutoReset = true;

            timerBgmChange = new System.Timers.Timer(2000);//初期値      // BGM切り替え用のタイマ、イベント
            timerBgmChange.Elapsed += onTimedEventPlayBgm;
            timerBgmChange.AutoReset = false;

            //コンポーネントの初期化
            InitializeComponent();
            
            //デフォルトBGMリストを読み込み
            loadDefaultBgmList();

            //XMLからBGMリストを読み込み
            if (!loadBgmListFromXml())
            {//設定ファイルからBGMリスト読み込みに失敗した場合は、空リストを作成する
                resumeBgmList();
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
            bgmList = null;
            defaultBgmList = null;
            player = null;
            endPointPathList = null;
        }


        /// <summary>
        /// エンド ポイントにBGMとコールバック関数を登録する
        /// </summary>
        private void registBgmToEndpoint()
        {
            //母港APIの登録
            endPointPathList.Add(new EndPointPath(0, "/kcsapi/api_port/port", new AnalysisEndPointDefault(new List<Bgm> { 
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MOTHER_BASE))
                                                                                                    }, ChengeBgm)));
            //昼戦APIの登録
            endPointPathList.Add(new EndPointPath(1, "/kcsapi/api_req_sortie/battle", new AnalysisEndPointDefault(new List<Bgm> {
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_DAY_BATTLE))
                                                                                                    }, ChengeBgm)));
            //演習APIの登録
            endPointPathList.Add(new EndPointPath(2, "/kcsapi/api_req_practice/battle", new AnalysisEndPointDefault(new List<Bgm> { 
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_PRACTICE_DAY_BATTLE))
                                                                                                    }, ChengeBgm)));
            //作戦MAPAPIの登録
            endPointPathList.Add(new EndPointPath(3, "/kcsapi/api_req_map/start", new AnalysisEndPointDefault(new List<Bgm> {
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MAP))
                                                                                                    }, ChengeBgm)));
            endPointPathList.Add(new EndPointPath(4, "/kcsapi/api_get_member/ship_deck", new AnalysisEndPointDefault(new List<Bgm> {
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MAP)) 
                                                                                                    }, ChengeBgm)));
            //戦果API(通常戦闘)の登録
            endPointPathList.Add(new EndPointPath(5, "/kcsapi/api_req_sortie/battleresult", new AnalysisEndPointResult(new List<Bgm> {
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_S)),
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_A)),
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_B)),
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_C)),
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_D)),
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_E))
                                                                                                    }, ChengeBgm)));
            //戦果API(演習)の登録
            endPointPathList.Add(new EndPointPath(5, "/kcsapi/api_req_practice/battle_result", new AnalysisEndPointResult(new List<Bgm> {
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_S)),
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_A)),
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_B)),
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_C)),
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_D)),
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_RESULT_E))
                                                                                                    }, ChengeBgm)));
            //夜戦(通常)APIの登録
            endPointPathList.Add(new EndPointPath(7, "/kcsapi/api_req_battle_midnight/battle", new AnalysisEndPointDefault(new List<Bgm> {
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_MIDNIGHT_BATTLE)) 
                                                                                                    }, ChengeBgm)));
            //夜戦(演習)APIの登録
            endPointPathList.Add(new EndPointPath(7, "/kcsapi/api_req_practice/midnight_battle", new AnalysisEndPointDefault(new List<Bgm> {
                                                                                                    bgmList.Find(x => x.ID.Equals((uint)Bgmtype.TYPE_PRACTICE_MIDNIGHT_BATTLE)) 
                                                                                                    }, ChengeBgm)));

            
        }

        /// <summary>
        /// BGMリストをXMLに保存する
        /// </summary>
        private bool saveBgmListToXml()
        {
            bool ret = false;
            string fileName = PATH_BGM_LIST_XML;

            //保存用データ作成
            BgmSetting bgmSetting = new BgmSetting();
            bgmSetting.BgmList = bgmList;
            if (checkBoxMute.HasContent)
            {//nullじゃなかったらboolでキャスト
                bgmSetting.isMuteAtLaunch = (bool)checkBoxMute.IsChecked;
            }
            else
            {
                bgmSetting.isMuteAtLaunch = false;            
            }

            try
            {
                //XmlSerializerオブジェクトを作成
                //オブジェクトの型を指定する
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(BgmSetting));
                //書き込むファイルを開く（UTF-8 BOM無し）
                System.IO.StreamWriter sw = new System.IO.StreamWriter(fileName, false, new System.Text.UTF8Encoding(false));
                //シリアル化し、XMLファイルに保存する
                serializer.Serialize(sw, bgmSetting);
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
            string fileName = PATH_BGM_LIST_XML;             //保存元のファイル名

            //読み込み用データ作成
            BgmSetting bgmSetting = new BgmSetting();
            bgmSetting.BgmList = bgmList;

            try
            {
                //XmlSerializerオブジェクトを作成
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(BgmSetting));
                //読み込むファイルを開く
                System.IO.StreamReader sr = new System.IO.StreamReader(fileName, new System.Text.UTF8Encoding(false));
                //XMLファイルから読み込み、逆シリアル化する
                bgmSetting = (BgmSetting)serializer.Deserialize(sr);
                //ファイルを閉じる
                sr.Close();

                //BgmListのインスタンスをコピー
                bgmList = bgmSetting.BgmList; 
                //読み込んだBGMリストに、項目漏れがあった場合、補充
                resumeBgmList();

                //起動時のMuteチェックボックスの状態を設定
                setMuteCheckBox(bgmSetting.isMuteAtLaunch);


                ret = true;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message.ToString());
            }

            return ret;
        }

        /// <summary>
        /// XMLから読み込んだBGMリストと、デフォルトBGMリストの差分を補充する
        /// </summary>
        private void resumeBgmList()
        {
            foreach (Bgm bgm in defaultBgmList)
            {//デフォルトBGMセットから検索
                if (bgmList.Find(x => x.ID.Equals(bgm.ID)) == null)
                {//もし、デフォルトBGMセットのうち、ロードしたBGMセットに含まれていないものがあった場合
                    //現行のBGMリストに追加しておく。
                    bgmList.Add(bgm);
                }
            }

            foreach (Bgm bgm in bgmList)
            {//現行のBGMリストについて
                //URIの更新
                bgm.refreshUri();
            }        
        }


        /// <summary>
        /// デフォルトのBGMリストを読み込む
        /// </summary>
        private void loadDefaultBgmList()
        {
            defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_MOTHER_BASE, "母港", "", "", TIME_BGM_CHANGE_NORMAL));
            defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_DAY_BATTLE, "昼戦", "", "", TIME_BGM_CHANGE_NORMAL));
            defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_MIDNIGHT_BATTLE, "夜戦", "", "", TIME_BGM_CHANGE_NORMAL));
            defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_MAP, "作戦MAP", "", "", TIME_BGM_CHANGE_NORMAL));
            defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_PRACTICE_DAY_BATTLE, "演習", "", "", TIME_BGM_CHANGE_NORMAL));
            defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_PRACTICE_MIDNIGHT_BATTLE, "演習(夜戦)", "", "", TIME_BGM_CHANGE_NORMAL));
            defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_RESULT_S, "戦果S", "", "S", TIME_BGM_CHANGE_RESULT));
            defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_RESULT_A, "戦果A", "", "A", TIME_BGM_CHANGE_RESULT));
            defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_RESULT_B, "戦果B", "", "B", TIME_BGM_CHANGE_RESULT));
            defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_RESULT_C, "戦果C", "", "C", TIME_BGM_CHANGE_RESULT));
            defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_RESULT_D, "戦果D", "", "D", TIME_BGM_CHANGE_RESULT));
            defaultBgmList.Add(new Bgm((uint)Bgmtype.TYPE_RESULT_E, "戦果E", "", "E", TIME_BGM_CHANGE_RESULT));
            return;
        }

        /// <summary>
        /// エンドポイントの監視を開始
        /// </summary>
        private void startWaitEndPoint()
        {
            foreach (EndPointPath x in endPointPathList)
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
            foreach (Bgm bgm in bgmList)
            {
                listViewBgm.Items.Add(new string[] {bgm.ScheneName,System.IO.Path.GetFileName(bgm.FilePath),bgm.ID.ToString()});
            }
        }

        /// <summary>
        /// 再生中のBGMをフェードアウトして止める
        /// </summary>
        private void fadeOutPlayerVolume()
        {
            if (player.Volume >= 0.05)
            {
                player.Volume = player.Volume - 0.05;
            }
            else
            {//フェードアウト完了
                player.Stop();
                player.Volume = defaultVolume;
                timerFadeOut.Enabled = false;

            }
        }
        /// <summary>
        /// BGMを再生する
        /// </summary>
        private void playBgm()
        {
            player.Close();
            player.Open(UriCurrentBgm);

            if (checkBoxMute.HasContent && (bool)checkBoxMute.IsChecked)
            {//Muteにチェック入っていたら再生しない
            }
            else
            {//それ以外は再生する
                player.Play();            
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
        /// BGM再生ボタン
        /// </summary>
        private void ButtonPlay_Click(object sender, RoutedEventArgs e)
        {
            player.Volume = defaultVolume;
            player.Play();
        }

        /// <summary>
        /// BGM停止ボタン
        /// </summary>
        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            player.Stop();
        }

        /// <summary>
        /// BGMが終わった時に呼ばれるイベント
        /// </summary>
        private void onBgmEnded(object sender, EventArgs e)
        {
            //BGMすぐ変更する
            timerBgmChange.Interval = TIME_BGM_CHANGE_IMMEDIATELY;
            //BGMを変更する(再生しなおし)
            timerBgmChange.Enabled = true;
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
                //BGMリストからIDでサーチ
                Bgm bgm = bgmList.Find(x => x.ID.Equals(ID));
                //ファイル名の更新
                bgm.FilePath = ofd.FileName;
                //参照先のURIの更新
                bgm.refreshUri();
                //ListViewの表示更新
                updateBgmListView();
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
            //ボリュームをMUTEにする。
            defaultVolume = VALUE_BGM_VOLUME_MUTE;
            //BGMも停止
            player.Stop();
        }

        /// <summary>
        /// Muteのチェックボックスにチェックが外れた際のイベント
        /// </summary>
        private void checkBoxMute_UnChecked(object sender, RoutedEventArgs e)
        {
            //ボリュームを通常にする。
            defaultVolume = VALUE_BGM_VOLUME_DEFAULT;
            //BGMも開始
            player.Play();

        }

        /// <summary>
        /// 外部クラスから、BGMの変更を起こす際の関数
        /// </summary>
        public void ChengeBgm(Bgm bgm)
        {
            if (UriCurrentBgm != bgm.URI)
            {//もし、現在のBGMと異なったらBGM変更
                UriCurrentBgm = bgm.URI;
                timerBgmChange.Interval = bgm.Interval;
                //直前のBGMのフェードアウトさせる
                timerFadeOut.Enabled = true;
                //BGMを変更する
                timerBgmChange.Enabled = true;
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
            public List<Bgm> BgmList;
            public bool isMuteAtLaunch;

            public BgmSetting()
            {
            }
        }
    }

}
