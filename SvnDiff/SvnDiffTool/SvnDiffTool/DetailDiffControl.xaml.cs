using DiffPlex.DiffBuilder;
using DiffPlex;
using DiffPlex.DiffBuilder.Model;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System;
using SharpSvn;
using System.Collections.ObjectModel;
using System.Windows.Shapes;
using Microsoft.VisualBasic;
using System.Text.Unicode;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace SvnDiffTool
{
    /// <summary>
    /// DetailDiffControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DetailDiffControl : UserControl
    {
        private string AContext;
        private string BContext;
        private SideBySideDiffModel? Result;
        private Dictionary<string, Dictionary<string, List<SvnLogEventArgs>>> DiffHistory;

        bool OnlyShowDiff;
        public DetailDiffControl()
        {
            InitializeComponent();

            AContext = "";
            BContext = "";
            Result = null;
            //leftTextBox.Document.PageWidth = 10000;
            //rightTextBox.Document.PageWidth = 10000;
            OnlyShowDiff = false;
            DiffOnlyBtn.Content = "변경사항만 출력";
        }
        public void SetDiffHistory(Dictionary<string, Dictionary<string, List<SvnLogEventArgs>>> _DiffHistory)
        {
            DiffHistory = _DiffHistory;
        }
        public void UpdateContext(string A, string B, string Filename, bool _OnlyDiff)
        {
            OnlyShowDiff = _OnlyDiff;
            DiffOnlyBtn.Content = OnlyShowDiff ? "전체 출력" : "변경사항만 출력";

            leftScroll.ScrollToHome();
            rightScroll.ScrollToHome();

            AContext = A; BContext = B;

            var differ = new Differ();
            var inlineBuilder = new SideBySideDiffBuilder(differ);
            Result = inlineBuilder.BuildDiffModel(AContext, BContext);

            int ACount = SetText(true, leftTextBox, leftInfo);
            int BCount = SetText(false, rightTextBox, rightInfo);

            int Max = ACount > BCount ? ACount : BCount;
            Title.Text = Filename + " 파일에서 " + Max + " 개의 변경 사항이 있습니다. \n";
            
            UpdateDiffHistory(Filename);
        }

        private void UpdateDiffHistory(string Filename)
        {
            HistoryList.Items.Clear();
            if (DiffHistory.TryGetValue(Filename, out var Historys))
            {
                foreach (var History in Historys)
                {
                    ListViewItem ListView = new ListViewItem();
                    string strMessage = "";

                    for (int i = 0; i < History.Value.Count; ++i)
                    {
                        int MaxMessage = 300;
                        string LogMessageLine = string.Format("{0}. 리비전 : {1} / 메시지 : {2}", i + 1, History.Value[i].Revision.ToString()
                                                    , History.Value[i].LogMessage.Replace("\r", "").Replace("\n", ""));
                        LogMessageLine = LogMessageLine.Length <= MaxMessage ? LogMessageLine : LogMessageLine.Substring(0, MaxMessage) + "..(생략)";
                        strMessage += LogMessageLine + "\n";
                    }
                    strMessage = strMessage.TrimEnd('\n');
                    ListView.Content = new { Author = History.Key, History = strMessage };
                    HistoryList.Items.Add(ListView);
                }
            }
            Int32 k = 0;
        }

        private int SetText(bool isOld, RichTextBox richTextBox, RichTextBox richTextBox_Info)
        {
            int ChangeCount = 0;
            richTextBox.Document.Blocks.Clear();
            richTextBox_Info.Document.Blocks.Clear();

            if (Result == null)
                return ChangeCount;

            List<DiffPiece> diffLines = isOld ? Result.OldText.Lines : Result.NewText.Lines;
            
            foreach (var line in diffLines)
            {
                if(line.Text == null)
                    continue;
                
                Paragraph paragraph = new Paragraph();
                Paragraph paragraph_info = new Paragraph();
                paragraph_info.TextAlignment = TextAlignment.Center;
                switch (line.Type)
                {
                    case ChangeType.Deleted:
                        ChangeCount++;
                        AddRun(paragraph_info.Inlines, "[삭제]", Brushes.LightPink);
                        AddRun(paragraph.Inlines, line.Text, Brushes.LightPink);
                        break;
                    case ChangeType.Inserted:
                        ChangeCount++;
                        AddRun(paragraph_info.Inlines, "[추가]", Brushes.LightSkyBlue);
                        AddRun(paragraph.Inlines, line.Text, Brushes.LightSkyBlue);
                        break;
                    case ChangeType.Modified:
                        ChangeCount++;
                        StringBuilder sb = new StringBuilder();
                        AddRun(paragraph_info.Inlines, "[변경]", Brushes.Yellow);
                        sb.Clear();
                        foreach (var subPiece in line.SubPieces)
                        {
                            if (subPiece.Type == ChangeType.Unchanged)
                            {
                                sb.Append(subPiece.Text); // 그냥 변경점없으면 저장
                                continue;
                            }
                            if (sb.Length > 0) AddRun(paragraph.Inlines, sb.ToString(), Brushes.Yellow);

                            // 변경 표시 
                            AddRun(paragraph.Inlines, subPiece.Text, Brushes.Orange);
                            sb.Clear();
                        }
                        if (sb.Length > 0) AddRun(paragraph.Inlines, sb.ToString(), Brushes.Yellow);
                        break;
                    case ChangeType.Unchanged:
                        if (OnlyShowDiff)
                        {
                            // 변경되지 않았지만 첫 줄 컬럼 정보는 출력하기 위해 예외처리
                            if (line.Position is not 1)
                            {
                                continue;
                            }
                        }
                        string LineNumber = "";
                        if(line.Position != null)
                            LineNumber = line.Position.ToString();
                        paragraph_info.Inlines.Add(LineNumber);
                        AddRun(paragraph.Inlines, line.Text, Brushes.White);
                        break;
                }
                richTextBox_Info.Document.Blocks.Add(paragraph_info);
                richTextBox.Document.Blocks.Add(paragraph);
            }
            return ChangeCount;
        }
        
        private int SetText(bool _isOld, TextBlock _TextBlock)
        {
            int ChangeCount = 0;
            _TextBlock.Inlines.Clear();

            if (Result == null)
                return ChangeCount;

            List<DiffPiece> diffLines = _isOld ? Result.OldText.Lines : Result.NewText.Lines;

            foreach (var line in diffLines)
            {
                StringBuilder sb = new StringBuilder();
                sb.Clear();
                switch (line.Type)
                {
                    case ChangeType.Deleted:
                        ChangeCount++;
                        sb.Append("[삭제] ").Append(line.Text);
                        AddRun(_TextBlock.Inlines, sb.ToString(), Brushes.LightPink);
                        break;
                    case ChangeType.Inserted:
                        ChangeCount++;
                        sb.Append("[추가] ").Append(line.Text);
                        AddRun(_TextBlock.Inlines, sb.ToString(), Brushes.LightSkyBlue);
                        break;
                    case ChangeType.Modified:
                        ChangeCount++;
                        sb.Append("[변경] ").Append(line.Text);
                        AddRun(_TextBlock.Inlines, sb.ToString(), Brushes.Yellow);
                        
                        foreach (var subPiece in line.SubPieces)
                        {
                            if(subPiece.Type == ChangeType.Unchanged)
                                continue;
                            
                            if(subPiece.Position == null)
                                continue;
                            
                            int startPosition = subPiece.Position.Value;
                            int length = subPiece.Text.Length;

                            // subPiece의 색상을 변경합니다.
                            for (int i = 0; i < length; i++)
                            {
                                if (_TextBlock.Inlines.LastInline is Run run)
                                {
                                    SolidColorBrush backgroundBrush = subPiece.Type switch
                                    {
                                        ChangeType.Inserted => Brushes.LightSkyBlue,
                                        ChangeType.Modified => Brushes.Orange,
                                        ChangeType.Deleted => Brushes.LightPink,
                                        ChangeType.Imaginary => Brushes.LightSeaGreen,
                                        _ => Brushes.Yellow
                                    };
                                    run.Background = backgroundBrush;
                                }
                            }
                        }
                        break;
                    case ChangeType.Unchanged:
                        if (OnlyShowDiff)
                            continue;
                        AddRun(_TextBlock.Inlines, "  " + line.Text, Brushes.White);
                        break;
                }
            }
            return ChangeCount;
        }

        private void AddRun(InlineCollection Inlines, string text, SolidColorBrush background)
        {
            Run run = new Run(text);
            run.Foreground = Brushes.Black;
            run.Background = background;
            Inlines.Add(run);
        }
        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (Result == null)
                return;

            OnlyShowDiff = !OnlyShowDiff;

            SetText(true, leftTextBox, leftInfo);
            SetText(false, rightTextBox, rightInfo);

            DiffOnlyBtn.Content = OnlyShowDiff ? "전체 출력" : "변경사항만 출력";
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender == rightScroll)
            {
                leftScroll.ScrollToVerticalOffset(rightScroll.VerticalOffset);
                leftScroll.ScrollToHorizontalOffset(rightScroll.HorizontalOffset);
            }
            else if (sender == leftScroll)
            {
                rightScroll.ScrollToVerticalOffset(leftScroll.VerticalOffset);
                rightScroll.ScrollToHorizontalOffset(leftScroll.HorizontalOffset);
            }
        }

        private void rightTextBox_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RichTextBox? ClickText = (RichTextBox)sender;

            if (ClickText == null)
                return;

            TextRange textRange = new TextRange(ClickText.Document.ContentStart, ClickText.Document.ContentEnd);
            string context = textRange.Text;
            try
            {
                Clipboard.SetText(context);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                MessageBox.Show("텍스트가 클립보드에 복사되었습니다. 다른 애플리케이션이 클립보드를 사용 중일 수 있습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show("텍스트가 클립보드에 복사되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
