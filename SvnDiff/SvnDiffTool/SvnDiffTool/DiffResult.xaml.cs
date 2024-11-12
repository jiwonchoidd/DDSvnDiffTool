using SharpSvn;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util;
using SvnDiffTool.GoogleSheet;

namespace SvnDiffTool
{
    public partial class DiffResult : Window
    {
        private List<KeyValuePair<string, SvnDiffKind>> DiffOutputs;

        string strSelectedItem;
        private SvnDiffInfo SvnDiffInfo;

        public DiffResult(List<KeyValuePair<string, SvnDiffKind>> diffOutputs, SvnDiffInfo _SvnDiffInfo,
            Dictionary<string, Dictionary<string, List<SvnLogEventArgs>>> _Histroy)
        {
            SvnDiffInfo = _SvnDiffInfo;
            DiffOutputs = diffOutputs;

            strSelectedItem = "";
            InitializeComponent();

            SetDisplayDiffInfo();

            DetailDiff.SetDiffHistory(_Histroy);
            UpdateLeftItem();
            UpdateRightItem();
        }

        private void UpdateLeftItem()
        {
            lstDiffFiles.Items.Clear();

            var sortedByExtension = DiffOutputs
                .GroupBy(
                    kvp => Path.GetExtension(kvp.Key)
                )
                .OrderBy(group => group.Key)
                .SelectMany(group => group);

            int csvcount = 0;

            foreach (var item in sortedByExtension)
            {
                ListViewItem ListView = new ListViewItem();
                ListView.Content = new { Item = item.Key };

                bool bCsvFile = item.Key.Contains(".csv");

                if (bCsvFile)
                {
                    switch (item.Value)
                    {
                        case SvnDiffKind.Added:
                        {
                            ListView.Foreground = Brushes.Blue;
                            break;
                        }
                        case SvnDiffKind.Deleted:
                        {
                            ListView.Foreground = Brushes.Red;
                            break;
                        }
                        case SvnDiffKind.Modified:
                        {
                            ListView.Foreground = Brushes.Green;
                            break;
                        }
                        case SvnDiffKind.Normal:
                        default:
                        {
                            ListView.Foreground = Brushes.Black;
                            break;
                        }
                    }

                    csvcount += 1;
                }
                else
                {
                    ListView.Foreground = Brushes.Gray;
                }

                lstDiffFiles.Items.Add(ListView);
            }

            if (lstDiffFiles.Items.Count > 0)
            {
                ListBoxItem? Item = (ListBoxItem)lstDiffFiles.Items[0];
                if (Item != null && Item.Content != null)
                {
                    lstDiffFiles.SelectedItem = Item;
                }
            }

            txtCount.Text = "--> 모든 파일 변경 갯수 : " + DiffOutputs.Count() + " / Csv 파일 변경 갯수 : " + csvcount;
        }

        private void lstDiffFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            lstDiffFiles.IsEnabled = false;
            UpdateRightItem();
            lstDiffFiles.IsEnabled = true;
        }

        private void UpdateRightItem()
        {
            var PrevstrSelectedItem = strSelectedItem;
            strSelectedItem = "";
            var item = lstDiffFiles.SelectedItem as ListViewItem;
            if (item != null)
            {
                dynamic content = item.Content;
                strSelectedItem = content.Item.ToString();
            }

            if (PrevstrSelectedItem == strSelectedItem)
            {
                MessageBox.Show("이미 선택된 아이템입니다.", "예외사항", MessageBoxButton.OK, MessageBoxImage.Error,
                    MessageBoxResult.OK, MessageBoxOptions.None);
                return;
            }

            if (strSelectedItem == "")
            {
                MessageBox.Show("선택된 아이템이 없습니다.", "예외사항", MessageBoxButton.OK, MessageBoxImage.Error,
                    MessageBoxResult.OK, MessageBoxOptions.None);
                return;
            }

            bool Ignore = strSelectedItem.Contains(".uasset");

            string a = Ignore
                ? "강제된 무시 확장자 입니다."
                : SvnHelper.GetFileString(SvnDiffInfo.ArepositoryURL + "/" + strSelectedItem,
                    SvnDiffInfo.previousRevision);
            string b = Ignore
                ? "강제된 무시 확장자 입니다."
                : SvnHelper.GetFileString(SvnDiffInfo.BrepositoryURL + "/" + strSelectedItem,
                    SvnDiffInfo.currentRevision);

            DetailDiff.UpdateContext(a, b, strSelectedItem, false);
        }

        private void SetDisplayDiffInfo()
        {
            txtInfo.Text = "--> ";

            if (SvnDiffInfo.ArepositoryURL == SvnDiffInfo.BrepositoryURL)
            {
                txtInfo.Inlines.Add(new Run(SvnDiffInfo.ArepositoryURL));
                txtInfo.Inlines.LastInline.Foreground = Brushes.Black;
                txtInfo.Inlines.LastInline.Background = Brushes.LightGray;

                txtInfo.Inlines.Add(" 경로에서 ");

                txtInfo.Inlines.Add(new Run(SvnDiffInfo.previousRevision.ToString()));
                txtInfo.Inlines.LastInline.Foreground = Brushes.Black;
                txtInfo.Inlines.LastInline.Background = Brushes.LightGray;


                txtInfo.Inlines.Add(" 리비전과 ");

                txtInfo.Inlines.Add(new Run(SvnDiffInfo.currentRevision.ToString()));
                txtInfo.Inlines.LastInline.Foreground = Brushes.Black;
                txtInfo.Inlines.LastInline.Background = Brushes.LightGray;

                txtInfo.Inlines.Add(" 리비전을 비교함. ");
            }
            else
            {
                txtInfo.Inlines.Add(new Run(SvnDiffInfo.ArepositoryURL + " (" + SvnDiffInfo.previousRevision + ")"));
                txtInfo.Inlines.LastInline.Foreground = Brushes.Black;
                txtInfo.Inlines.LastInline.Background = Brushes.LightGray;

                txtInfo.Inlines.Add(" 리비전과 ");

                txtInfo.Inlines.Add(new Run(SvnDiffInfo.BrepositoryURL + " (" + SvnDiffInfo.currentRevision + ")"));
                txtInfo.Inlines.LastInline.Foreground = Brushes.Black;
                txtInfo.Inlines.LastInline.Background = Brushes.LightGray;

                txtInfo.Inlines.Add(" 리비전을 비교함. ");
            }
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine($"DiffOutputs.Count = {DiffOutputs.Count}");
            if (0 >= DiffOutputs.Count) return;
            
            string absolutePath =
                Path.GetFullPath($"Export/{SvnDiffInfo.previousRevision}_{SvnDiffInfo.currentRevision}");
            MessageBoxResult Result = MessageBox.Show(
                $"아래 확인을 클릭 시 Diff 결과를 \n{absolutePath}\n위 경로에 CSV 파일 ({DiffOutputs.Count} 개)를 추출합니다.\nYes : 변경 사항만 추출 / No : 모든 정보 추출",
                "CSV EXPORT", MessageBoxButton.YesNoCancel);

            if (Result == MessageBoxResult.Cancel)
                return;

            SvnHelper.BackgroundProcess process = new SvnHelper.BackgroundProcess();
            process.Loading = new LoadingWindow();

            process.Func = delegate(LoadingWindow Loading)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Loading.OnUpdatedelegate.Invoke("Diff 결과 파싱 및 CSV 파일 추출..", 0, DiffOutputs.Count);
                });

                if (Directory.Exists(absolutePath))
                {
                    Directory.Delete(absolutePath, true);
                }

                Directory.CreateDirectory(absolutePath);
                Directory.CreateDirectory(absolutePath + "/Old_Table/");
                Directory.CreateDirectory(absolutePath + "/New_Table/");
                Console.WriteLine($"Directory created: {absolutePath}");

                int counter = 0;
                foreach (var diffOuput in DiffOutputs)
                {
                    bool bCsvFile = diffOuput.Key.Contains(".csv");
                    if (!bCsvFile)
                        continue;

                    string a = SvnHelper.GetFileString(SvnDiffInfo.ArepositoryURL + "/" + diffOuput.Key,
                        SvnDiffInfo.previousRevision);
                    string b = SvnHelper.GetFileString(SvnDiffInfo.BrepositoryURL + "/" + diffOuput.Key,
                        SvnDiffInfo.currentRevision);

                    var differ = new Differ();
                    var inlineBuilder = new SideBySideDiffBuilder(differ);
                    SideBySideDiffModel? result = inlineBuilder.BuildDiffModel(a, b);

                    if (result == null)
                        continue;

                    List<List<ExportHelper.CellInfo>> rawStructOld =
                        ExportHelper.ParseDiffModel(result, true, Result == MessageBoxResult.Yes);
                    List<List<ExportHelper.CellInfo>> rawStructNew =
                        ExportHelper.ParseDiffModel(result, false, Result == MessageBoxResult.Yes);
                    string ExportFile_Old = $"{absolutePath}/Old_Table/{diffOuput.Key}";
                    string ExportFile_New = $"{absolutePath}/New_Table/{diffOuput.Key}";
                    ExportHelper.WriteCsvFile(ExportFile_Old, rawStructOld);
                    ExportHelper.WriteCsvFile(ExportFile_New, rawStructNew);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Loading.OnUpdatedelegate.Invoke($"Diff 결과 파싱 및 CSV 파일 생성\n{diffOuput.Key} csv file", counter++,
                            DiffOutputs.Count);
                    });
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Loading.Close();

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = absolutePath,
                        UseShellExecute = true
                    });
                });
            };
            SvnHelper.StartWork(process);
            process.Loading.ShowDialog();
        }

        private void ExportGoogleBtn(object sender, RoutedEventArgs e)
        {
            Console.WriteLine($"DiffOutputs.Count = {DiffOutputs.Count}");
            if (0 >= DiffOutputs.Count) return;

            MessageBoxResult Result = MessageBox.Show(
                $"아래 확인을 클릭 시 Diff 결과 파일 ({DiffOutputs.Count} 개의 내용)을 구글 드라이브에 업로드합니다.",
                "Google Upload", MessageBoxButton.OKCancel);

            if (Result == MessageBoxResult.Cancel)
                return;

            GoogleInstance instance = new GoogleInstance();
            bool bCred = instance.DoCredentialAsync().Result;
            if (!bCred)
            {
                MessageBox.Show(
                    $"구글 업로드에 대한 권한이 없습니다.",
                    "Google Upload", MessageBoxButton.OK);
                return;
            }
            
            SvnHelper.BackgroundProcess process = new SvnHelper.BackgroundProcess();
            process.Loading = new LoadingWindow();

            process.Func = delegate(LoadingWindow Loading)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Loading.OnUpdatedelegate.Invoke("구글 드라이브 시트 생성 시도..", 0, DiffOutputs.Count);
                });

                string FileName = $"DiffResult_{SvnDiffInfo.previousRevision}_{SvnDiffInfo.currentRevision}";
                string CreatedSheetId = instance.InitCreateSheet(FileName).Result;
                
                if (CreatedSheetId == "")
                {
                    MessageBox.Show(
                        $"구글 시트 생성 실패.",
                        "Google Upload", MessageBoxButton.OK);
                    return;
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Loading.OnUpdatedelegate.Invoke($"{FileName} : 구글 드라이브 시트 생성 완료 {CreatedSheetId}", 1, DiffOutputs.Count);
                    });
                }
                
                instance.ClearRequest();
                
                // 무료 계정 1분당 60 요청 이상 시 에러, 최대한 요청을 통합해서 보냄
                int counter = 0;
                foreach (var diffOuput in DiffOutputs)
                {
                    instance.AddSheetRequest(diffOuput.Key);
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Loading.OnUpdatedelegate.Invoke($"{diffOuput.Key} : 시트 생성 요청 {CreatedSheetId}", counter++, DiffOutputs.Count * 2);
                    });
                }
                instance.ExecuteRequest().Wait();
                instance.ClearRequest();
                
                foreach (var diffOuput in DiffOutputs)
                {
                    bool bCsvFile = diffOuput.Key.Contains(".csv");
                    if (!bCsvFile)
                        continue;

                    string a = SvnHelper.GetFileString(SvnDiffInfo.ArepositoryURL + "/" + diffOuput.Key,
                        SvnDiffInfo.previousRevision);
                    string b = SvnHelper.GetFileString(SvnDiffInfo.BrepositoryURL + "/" + diffOuput.Key,
                        SvnDiffInfo.currentRevision);

                    var differ = new Differ();
                    var inlineBuilder = new SideBySideDiffBuilder(differ);
                    SideBySideDiffModel? result = inlineBuilder.BuildDiffModel(a, b);

                    if (result == null)
                        continue;

                    List<List<ExportHelper.CellInfo>> rawStructOld =
                        ExportHelper.ParseDiffModel(result, true, true);
                    List<List<ExportHelper.CellInfo>> rawStructNew =
                        ExportHelper.ParseDiffModel(result, false, true);

                    if (!instance.ValidRequestSize())
                    {
                        instance.ExecuteRequest().Wait();
                        instance.ClearRequest();
                    }
                    instance.AddSheetUpdateRequest(SvnDiffInfo, diffOuput.Key, rawStructOld, rawStructNew);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Loading.OnUpdatedelegate.Invoke($"{diffOuput.Key} : 시트 내용 동기화 요청 {CreatedSheetId}", counter++, DiffOutputs.Count * 2);
                    });
                }
                
                instance.ExecuteRequest().Wait();
                instance.ClearRequest();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Loading.Close();
                });
            };
            SvnHelper.StartWork(process);
            process.Loading.ShowDialog();
        }
    }
}