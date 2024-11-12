using DiffPlex.DiffBuilder;
using DiffPlex;
using System;
using System.Collections.Generic;
using System.Linq;
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
using DiffPlex.DiffBuilder.Model;

namespace SvnDiffTool
{
    /// <summary>
    /// DetailDiffResult.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DetailDiffResult : Window
    {
        private string AContext;
        private string BContext;
        private SideBySideDiffModel Result;

        public DetailDiffResult(string A, string B, string Filename)
        {
            InitializeComponent();

            Title.Text = Filename;
            AContext = A; BContext = B;

            leftTextBox.Document.PageWidth = 10000;
            rightTextBox.Document.PageWidth = 10000;

            var Adiffer = new Differ();
            var AinlineBuilder = new SideBySideDiffBuilder(Adiffer);
            Result = AinlineBuilder.BuildDiffModel(AContext, BContext);
            SetText(true, leftTextBox);
            SetText(false, rightTextBox);
        }
        private void SetText(bool Old, RichTextBox RichTextBox)
        {
            List<DiffPiece> DiffLine = Old ? Result.OldText.Lines : Result.NewText.Lines;
            
            foreach (var line in DiffLine)
            {
                List<Run> runList = new List<Run>();

                switch (line.Type)
                {
                    case ChangeType.Deleted:
                        {
                            Run run = new Run();
                            run.Foreground = Brushes.Black;
                            run.Background = Brushes.LightPink;
                            run.Text = "[삭제] ";
                            run.Text += line.Text;
                            runList.Add(run);
                            break;
                        }
                    case ChangeType.Inserted:
                        {
                            Run run = new Run();
                            run.Foreground = Brushes.Black;
                            run.Background = Brushes.LightSkyBlue;
                            run.Text = "[추가] ";
                            run.Text += line.Text;
                            runList.Add(run);
                            break;
                        }
                    case ChangeType.Modified:
                        {
                            foreach (var subPiece in line.SubPieces)
                            {
                                SolidColorBrush BackColor  = Brushes.Yellow;

                                switch (subPiece.Type)
                                {
                                    case ChangeType.Inserted:
                                    case ChangeType.Modified:
                                    case ChangeType.Deleted:
                                        BackColor = Brushes.Orange;
                                        break;
                                }

                                runList.Add(new Run(subPiece.Text) { Foreground = Brushes.Black, Background = BackColor });
                            }
                            Run run = new Run();
                            run.Foreground = Brushes.Black;
                            run.Background = Brushes.Yellow;
                            run.Text = "[추가] ";

                            runList.Insert(0, run);
                            break;
                        }
                    case ChangeType.Unchanged:
                        {
                            Run run = new Run();
                            run.Foreground = Brushes.Black;
                            run.Text = "  ";
                            run.Text += line.Text;
                            runList.Add(run);
                            break;
                        }
                }

                Paragraph paragraph = new Paragraph();
                foreach (var DoRun in runList)
                {
                    paragraph.Inlines.Add(DoRun);
                }
                RichTextBox.Document.Blocks.Add(paragraph);
            }
        }
    
        private void OnNextDiff(object sender, RoutedEventArgs e)
        {

        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if(sender == rightScroll)
            {
                leftScroll.ScrollToVerticalOffset(rightScroll.VerticalOffset);
                leftScroll.ScrollToHorizontalOffset(rightScroll.HorizontalOffset);
            }
            else if(sender == leftScroll)
            {
                rightScroll.ScrollToVerticalOffset(leftScroll.VerticalOffset);
                rightScroll.ScrollToHorizontalOffset(leftScroll.HorizontalOffset);
            }
        }
    }
}
