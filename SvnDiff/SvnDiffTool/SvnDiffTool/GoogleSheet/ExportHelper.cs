using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using DiffPlex.DiffBuilder.Model;

namespace SvnDiffTool.GoogleSheet;

public class ExportHelper
{
    public struct CellInfo
    {
        public string Context;
        public SolidColorBrush Color;
    }

    public static void WriteCsvFile(string filePath, List<List<CellInfo>> csvInfo)
    {
        using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
        {
            foreach (var row in csvInfo)
            {
                // CSV 한 줄을 만듭니다.
                var line = string.Join(",", row.ConvertAll(cell => cell.Context));
                writer.WriteLine(line);
            }
        }
    }
    public static List<List<CellInfo>> ParseDiffModel(SideBySideDiffModel? result, bool isOld, bool OnlyShowDiff)
    {
        List<List<CellInfo>> csvInfo = new List<List<CellInfo>>();
        if (result == null)
            return csvInfo;

        List<DiffPiece> diffLines = isOld ? result.OldText.Lines : result.NewText.Lines;
        
        foreach (var line in diffLines)
        {
            if(line.Text == null)
                continue;
            
            List<CellInfo> rowCell = new List<CellInfo>(); 
            var cellValue = ParseCsvLine(line.Text);
            List<bool> diffList = ParseDiffLine(cellValue, line.SubPieces);

            for (int nIndex = 0; nIndex < cellValue.Count; nIndex++) // 셀 단위
            {
                var cell = new CellInfo();
                cell.Context = cellValue[nIndex];
                
                switch (line.Type)
                {
                    case ChangeType.Deleted:
                        cell.Color = Brushes.LightPink;
                        break;
                    case ChangeType.Inserted:
                        cell.Color = Brushes.LightSkyBlue;
                        break;
                    case ChangeType.Modified:
                        cell.Color = diffList[nIndex] ? Brushes.Orange : Brushes.Yellow;
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
                        cell.Color = Brushes.Cornsilk;
                        break;
                }
                rowCell.Add(cell);
            }

            if (rowCell.Count > 0)
            {
                csvInfo.Add(rowCell);
            }
        }
        return csvInfo;
    }
    
    private static List<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var cell = string.Empty;
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];

            if (ch == '"')
            {
                inQuotes = !inQuotes;
                cell += ch;
            }
            else if (ch == ',' && !inQuotes)
            {
                cells.Add(cell);
                cell = string.Empty;
            }
            else
            {
                cell += ch;
            }
        }

        if (!string.IsNullOrEmpty(cell))
        {
            cells.Add(cell);
        }

        return cells;
    }
    
    private static List<bool> ParseDiffLine(List<string> _target, List<DiffPiece> _subPieces)
    {
        var changeList = new List<bool>();

        Queue<string> queue = new Queue<string>(_target);
        
        string founded = "";
        bool bChanged = false;
        foreach (DiffPiece piece in _subPieces)
        {
            if (queue.Count == 0)
                break;
            founded += piece.Text;
            string peekaboo = queue.Peek();

            bChanged |= piece.Type != ChangeType.Unchanged;
            
            if (founded.Contains(peekaboo))
            {
                changeList.Add(bChanged);
                
                queue.Dequeue();
                founded = "";
                bChanged = false;
            }
        }

        while (queue.Count != 0)
        {
            changeList.Add(false);
            queue.Dequeue();
        }
        
        return changeList;
    }
}