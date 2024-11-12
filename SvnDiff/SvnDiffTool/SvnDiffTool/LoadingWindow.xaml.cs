using System.Windows;
using static SvnDiffTool.SvnHelper;

namespace SvnDiffTool
{
    /// <summary>
    /// LoadingWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LoadingWindow : Window
    {
        public delegate void OnUpdate(string text, int cur, int max);
        public delegate void OnFinish();
        public OnUpdate OnUpdatedelegate;
        public LoadingWindow()
        {
            InitializeComponent();
            OnUpdatedelegate = Update;
        }

        void Update(string text, int cur, int max)
        {
            if (max == 0)
                return;

            loadingText.Text = text;
            loadingBar.Value = (double)cur / max;
        }
    }
}
