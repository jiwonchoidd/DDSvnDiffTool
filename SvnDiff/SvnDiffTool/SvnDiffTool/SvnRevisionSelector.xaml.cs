using SharpSvn;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SvnDiffTool
{
    public partial class SvnRevisionSelector : Window
    {
        public string SelectedRevision { get; private set; }
        private long TargetRevision = -1;
        private string TargetUrl;
        public SvnRevisionSelector(string Path, string _Revision)
        {
            InitializeComponent();

            SelectedPath.Text += Path;
            CurrentRevision.Text = _Revision;
            TargetUrl = Path;
            LoadRevision(TargetUrl);

            FoldersList.SelectionChanged += FoldersList_SelectionChanged;
        }

        private void FoldersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = FoldersList.SelectedItem as ListViewItem;
            if (item != null)
            {
                dynamic content = item.Content;
                CurrentRevision.Text = content.Revision.ToString();
            }
        }

        private void LoadRevision(string _Path)
        {
            Uri url = new Uri(_Path);
            Collection<SvnLogEventArgs> list;
            long PrevTargetRevison = TargetRevision;
            TargetRevision = SvnHelper.GetRepoLogList(url, out list, TargetRevision);

            if(PrevTargetRevison == TargetRevision)
            {
                // 중복 리비전 일 경우 로드 하지 않음
                return;
            }
            foreach (var log in list)
            {
                ListViewItem item = new ListViewItem();
                item.Content = new { Revision = log.Revision, Author = log.Author, Time = log.Time.ToShortDateString(), LogMessage = log.LogMessage };
                FoldersList.Items.Add(item);
            }
        }

        private void FoldersList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = FoldersList.SelectedItem as ListViewItem;
            if (item != null)
            {
                dynamic content = item.Content;
                CurrentRevision.Text = content.Revision.ToString();
            }
            SelectedRevision = CurrentRevision.Text;
            DialogResult = true;
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            // 사용자가 선택한 경로를 반환
            SelectedRevision = CurrentRevision.Text;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 사용자가 취소하면 선택된 경로를 null로 설정
            SelectedRevision = null;
            DialogResult = false;
        }

        private void FindMore(object sender, RoutedEventArgs e)
        {
            LoadRevision(TargetUrl);
            FoldersList.ScrollIntoView(FoldersList.Items[FoldersList.Items.Count - 1]);
        }
    }
}