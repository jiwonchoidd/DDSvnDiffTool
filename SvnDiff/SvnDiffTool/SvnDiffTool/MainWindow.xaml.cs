using SharpSvn;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;


namespace SvnDiffTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadUI();
        }

        private void LoadUI()
        {
            SavePreference.LoadUserPreference();

            object? Save = SavePreference.GetValue("AsSameCheckBox.IsChecked");
            if (Save != null)
            {
                AsSameCheckBox.IsChecked = (bool)Save;
            }
            else // 없을때 기본값
            {
                AsSameCheckBox.IsChecked = true;
            }

            Save = SavePreference.GetValue("txtRepositoryURL_1.Content");
            if (Save != null)
            {
                txtRepositoryURL_1.Content = (string)Save;
            }
            else // 없을때 기본값
            {
                txtRepositoryURL_1.Content = SvnHelper.DefaultSVNUrl;
            }
            UpdateUrlCheck(txtRepositoryURL_1);

            Save = SavePreference.GetValue("txtRepositoryURL_2.Content");
            if (Save != null)
            {
                txtRepositoryURL_2.Content = (string)Save;
            }
            else // 없을때 기본값
            {
                txtRepositoryURL_2.Content = SvnHelper.DefaultSVNUrl;
            }
            AsSameCheckBox_Click(AsSameCheckBox, new RoutedEventArgs());
            
            UpdateUrlCheck(txtRepositoryURL_2);
        }

        protected override void OnClosed(EventArgs e) 
        {
            SavePreference.SaveUserPreference();
        }

        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;

            string ArepositoryURL = txtRepositoryURL_1.Content == null ? "" : txtRepositoryURL_1.Content.ToString().Trim();
            string BrepositoryURL = txtRepositoryURL_2.Content == null ? "" : txtRepositoryURL_2.Content.ToString().Trim();
            
            string previousRevision = txtPreviousRevision.Content == null ? "" : txtPreviousRevision.Content.ToString().Trim();
            string currentRevision = txtCurrentRevision.Content == null ? "" : txtCurrentRevision.Content.ToString().Trim();

            if (string.IsNullOrEmpty(ArepositoryURL) || string.IsNullOrEmpty(BrepositoryURL) ||  string.IsNullOrEmpty(previousRevision) || string.IsNullOrEmpty(currentRevision))
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show("비어있는 값이 있습니다. 다시 입력해주세요.", "예외사항", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.None);
                return;
            }

            if(!SvnHelper.CheckRepositoryUrl(ArepositoryURL) || !SvnHelper.CheckRepositoryUrl(BrepositoryURL))
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show("유효하지 않는 SVN Url로 지정되었습니다. 다시 입력해주세요.", "예외사항", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.None);
                return;
            }
            long PrevRev = long.Parse(previousRevision);
            long CurRev = long.Parse(currentRevision);
            if(PrevRev > CurRev)
            {
                MessageBoxResult Result = MessageBox.Show(
                    string.Format("선택된 이전 리비전이 비교할 이후 리비전보다 최신 리비전입니다. \n({0} -> {1}).\n그래도 비교하시겠습니까?", PrevRev, CurRev),
                    "경고", MessageBoxButton.YesNo);

                if (Result == MessageBoxResult.No)
                {
                    Mouse.OverrideCursor = null;
                    return;
                }
            }
            SvnDiffInfo DiffInfo = new SvnDiffInfo();
            DiffInfo.ArepositoryURL = ArepositoryURL;
            DiffInfo.BrepositoryURL = BrepositoryURL;
            DiffInfo.previousRevision = PrevRev;
            DiffInfo.currentRevision = CurRev;
            Mouse.OverrideCursor = null;

            SvnHelper.BackgroundProcess process = new SvnHelper.BackgroundProcess();
            process.Loading = new LoadingWindow();

            process.Func = delegate (LoadingWindow Loading)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Loading.OnUpdatedelegate.Invoke("변경 파일 탐지 중..", 0, 1);
                });

                List<KeyValuePair<string, SvnDiffKind>> diffOutput = SvnHelper.GetDiffSummary(ArepositoryURL, BrepositoryURL, PrevRev, CurRev);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Loading.OnUpdatedelegate.Invoke(string.Format("{0}개의 변경 파일 탐지 완료!", diffOutput.Count), 0, 1);
                });

                Dictionary<string, Dictionary<string, List<SvnLogEventArgs>>> History = new Dictionary<string, Dictionary<string, List<SvnLogEventArgs>>>();

                SvnHelper.GetFileHistory(diffOutput, BrepositoryURL, PrevRev, CurRev, History, Loading);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Loading.Close();
                    
                    DiffResult Popup = new DiffResult(diffOutput, DiffInfo, History);
                    Popup.ShowDialog();
                });
            };
            SvnHelper.StartWork(process);
            process.Loading.ShowDialog();
        }

        private void UpdateUrlCheck(System.Windows.Controls.Button _Button)
        {
            if (false == SvnHelper.CheckRepositoryUrl(_Button.Content.ToString().Trim()))
            {
                _Button.Background = Brushes.LightSalmon;
            }
            else
            {
                _Button.Background = Brushes.LightGreen;
            }
        }

        private void txtRepositoryURL_Click(object sender, RoutedEventArgs e)
        {
            Button? Target = null;
            if (sender == txtRepositoryURL_1)
            {
                txtPreviousRevision.Content = "";
                Target = txtRepositoryURL_1;
            }
            else if(sender == txtRepositoryURL_2)
            {
                if (AsSameCheckBox.IsChecked == true)
                {
                    MessageBox.Show("오른쪽 상단 A,B 동일한 주소 사용하게 되어 있습니다. 체크 해제해야 동작합니다.", "예외사항", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.None);
                    return;
                }

                txtCurrentRevision.Content = "";
                Target = txtRepositoryURL_2;
            }
            SvnReposSelector Popup = new SvnReposSelector(Target.Content.ToString().Trim());
            Popup.ShowDialog();

            if (Popup.DialogResult == true)
            {
                string selectedPath = Popup.SelectedPath;
                Target.Content = selectedPath;

                if (Target == txtRepositoryURL_1)
                {
                    SavePreference.SetValue("txtRepositoryURL_1.Content", selectedPath);
                    AsSameCheckBox_Click(AsSameCheckBox, new RoutedEventArgs());
                }
            }

            UpdateUrlCheck(txtRepositoryURL_1);
            UpdateUrlCheck(txtRepositoryURL_2);
        }

        private void txtPreviousRevision_Click(object sender, RoutedEventArgs e)
        {
            string TargetRev = "";
            if(txtPreviousRevision.Content != null)
            {
                TargetRev = txtPreviousRevision.Content.ToString().Trim();
            }
            SvnRevisionSelector Popup = new SvnRevisionSelector(txtRepositoryURL_1.Content.ToString().Trim(), TargetRev);
            Popup.ShowDialog();

            if (Popup.DialogResult == true)
            {
                txtPreviousRevision.Content = Popup.SelectedRevision;
                SavePreference.SetValue("txtPreviousRevision.Content", txtPreviousRevision.Content);
            }
        }

        private void txtCurrentRevision_Click(object sender, RoutedEventArgs e)
        {
            string TargetRev = "";
            if (txtCurrentRevision.Content != null)
            {
                TargetRev = txtCurrentRevision.Content.ToString().Trim();
            }
            SvnRevisionSelector Popup = new SvnRevisionSelector(txtRepositoryURL_2.Content.ToString().Trim(), TargetRev);
            Popup.ShowDialog();

            if (Popup.DialogResult == true)
            {
                txtCurrentRevision.Content = Popup.SelectedRevision;
                //SavePreference.SetValue("txtPreviousRevision.Content", txtCurrentRevision.Content);
            }
        }

        private void AsSameCheckBox_Click(object sender, RoutedEventArgs e)
        {
            SavePreference.SetValue("AsSameCheckBox.IsChecked", AsSameCheckBox.IsChecked);

            // A B를 같게 만듬
            if (AsSameCheckBox.IsChecked == true)
            {
                if(txtRepositoryURL_1.Content != null)
                {
                    if(SvnHelper.CheckRepositoryUrl(txtRepositoryURL_1.Content.ToString()))
                    {
                        txtRepositoryURL_2.Content = txtRepositoryURL_1.Content;
                        return;
                    }
                }
            }

            txtRepositoryURL_2.Content = SvnHelper.DefaultSVNUrl;

        }
    }
}
