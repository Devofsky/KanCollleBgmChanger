using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KancolleBgmChenger
{
    /// <summary>
    /// KanColleViewerからもらうエンドポイントからのセッション通信を管理するクラス
    /// </summary>
    public class EndPointPath
    {
        private uint id;                 //ID
        private string endPointPath;                  //受信待ちするエンドポイントのパス
        private AnalysisEndPoint callBackClass;

        public uint ID { get { return id; } }
        public string EventPath { get { return endPointPath; } }
        public AnalysisEndPoint CallbackClass { get { return callBackClass; } }


        public EndPointPath(
            uint _id,
            string _endPointPath,
             AnalysisEndPoint _callBackClass)
        {
            //ID
            id = _id;
            endPointPath = _endPointPath;
            callBackClass = _callBackClass;
        }

    }


}
