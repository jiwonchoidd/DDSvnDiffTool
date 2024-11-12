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
    public partial class SvnReposSelector : Window
    {
        public string SelectedPath { get; private set; }

        public SvnReposSelector(string _Path)
        {
            InitializeComponent();

            SelectedPath = "";
            CurrentPath.Text = _Path;
            LoadFolders(_Path);
        }

        private void LoadFolders(string _Path)
        {
            FoldersList.Items.Clear();
            if (_Path.Length <= 0)
                return;
            
            Uri url = new Uri(_Path);
            string myPath = url.LocalPath;

            Collection<SvnListEventArgs> list;
            list = SvnHelper.GetRepoList(url);

            List<ListBoxItem> FileList = new List<ListBoxItem>();
            foreach (var l in list)
            {
                if (l.Uri.LocalPath == l.RepositoryRoot.LocalPath) continue;

                switch (l.Entry.NodeKind)
                {
                    case SvnNodeKind.Directory:
                        {
                            string PathLastTrim = l.Uri.LocalPath.TrimEnd('/');
                            if (PathLastTrim != myPath)
                            {
                                ListBoxItem item = new ListBoxItem();
                                System.IO.DirectoryInfo directoryInfo = new System.IO.DirectoryInfo(l.Uri.LocalPath);
                                string folderName = directoryInfo.Name;
                                item.Content = folderName;
                                item.Background = folderName.Contains("Table") || folderName.Contains("_Branch") ? Brushes.Yellow : Brushes.LightYellow;
                                FoldersList.Items.Add(item);
                            }
                        }break;
                    case SvnNodeKind.File:
                        {
                            string PathLastTrim = l.Uri.LocalPath.TrimEnd('/');
                            if (PathLastTrim != myPath)
                            {
                                ListBoxItem item = new ListBoxItem();
                                item.Foreground = Brushes.Gray;
                                item.Content = l.Name;
                                FileList.Add(item);
                            }
                        }
                        break;
                }
            }
            foreach (var l in FileList)
            {
                FoldersList.Items.Add(l);
            }
        }

        private void FoldersList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ListBoxItem selectedFolder = (ListBoxItem)FoldersList.SelectedItem;
            if (selectedFolder != null && selectedFolder.Foreground != Brushes.Gray)
            {
                string newPath = $"{CurrentPath.Text}/{selectedFolder.Content}";
                LoadFolders(newPath);
                CurrentPath.Text = newPath;
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            // 사용자가 선택한 경로를 반환
            SelectedPath = CurrentPath.Text.TrimEnd('/');
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 사용자가 취소하면 선택된 경로를 null로 설정
            SelectedPath = null;
            DialogResult = false;
        }

        private void CurrentPath_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void PathBackward(object sender, RoutedEventArgs e)
        {
            CurrentPath.Text = MakePathBackward(CurrentPath.Text.TrimEnd('/'));
            LoadFolders(CurrentPath.Text);
        }

        private string MakePathBackward(string _Path)
        {
            string[] pathComponents = CurrentPath.Text.Split('/');

            if(4 >= pathComponents.Length)
            {
                Console.WriteLine("최상위로 칩시다");
                return _Path;
            }

            string[] parentComponents = new string[pathComponents.Length - 1];
            Array.Copy(pathComponents, parentComponents, parentComponents.Length);

            string parentPath = string.Join("/", parentComponents);

            return parentPath;
        }
    }
}