using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KancolleBgmChenger
{
    /// <summary>
    /// 音楽管理用のクラス
    /// </summary>
    public class Bgm
    {
        //---------------メンバ変数--------------------------------------------
        private uint m_ID;                          //ID
        private string m_scheneName;               //シーン名
        private string m_filePath;                 //ファイル名
        private string m_key;                       //参照用文字列
        private Uri m_uri;                          //Uri名
        private uint m_interval;                    //再生までの時間

        //---------------外部クラスからのアクセサ--------------------
        public uint ID { get { return m_ID; } set { m_ID = value; } }
        public string ScheneName { get { return m_scheneName; } set { m_scheneName = value; } }
        public string FilePath { get { return m_filePath; } set { m_filePath = value; } }
        public string Key { get { return m_key; } set { m_key = value; } }
        public Uri URI { get { return m_uri; } }
        public uint Interval { get { return m_interval; } set { m_interval = value; } }

        //コンストラクタ
        public Bgm()
        { }

        //コンストラクタ
        public Bgm(
            uint _id,
            string _scheneName,
            string _filePath,
            string _key,
            uint _interval)
        {
            //ID
            m_ID = _id;
            //ファイル名
            m_filePath = _filePath;
            //シーン名
            m_scheneName = _scheneName;
            //URIの実体を作成
            m_uri = new Uri(_filePath, UriKind.Relative);
            m_key = _key;
            m_interval = _interval;
        }

        //Uriの更新を行う関数
        public void refreshUri()
        {
            m_uri = null;
            m_uri = new Uri(m_filePath, UriKind.Relative);
        }

    }
}
