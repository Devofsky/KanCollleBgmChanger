using System;
using System.ComponentModel.Composition;
using Grabacr07.KanColleViewer.Composition;
using System.Threading;
using System.Windows.Controls;

namespace KancolleBgmChenger
{
    [Export(typeof(IPlugin))]
    [Export(typeof(ITool))]
    [ExportMetadata("Guid", "55F1599E-5FAD-4696-A972-BF8C4B3C1B79")]
    [ExportMetadata("Title", "BgmChanger")]
    [ExportMetadata("Description", "艦これのBGMを変更します。")]
    [ExportMetadata("Version", "0.2.0")]
    [ExportMetadata("Author", "@Devofsky")]
    public class Plugin : IPlugin,IDisposable, ITool
    {
        private string name = "BgmChanger";
        private object view = new UiBgmChanger { DataContext = "Test" }; 

        public Plugin()
        {
        }

        public void Initialize()
        {
        }

        //デストラクタも呼ばれる
        ~Plugin()
        {
            //System.Media.SystemSounds.Hand.Play();        
        }
        //DisPoseも呼ばれる
        public void Dispose()
        {
        }

        public string Name
        {
            get { return name; }
        }

        public object View
        {
            get { return view; }
        }
    }
}