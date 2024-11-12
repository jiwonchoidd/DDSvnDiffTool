using SharpSvn;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading;
using System.Windows;
using System.Windows.Shapes;

namespace SvnDiffTool
{
    public struct SvnDiffInfo
    {
        public string ArepositoryURL;
        public string BrepositoryURL;
        public long previousRevision;
        public long currentRevision;
    }
    public struct ChangeInfo
    {
        public ChangeInfo()
        {
            strLine = "";
            ChangeContext = new List<string>();
        }
        public string strLine;
        public List<string> ChangeContext;
        public void SetLine(string _line) { strLine = _line;  }
        public void AddChange(string _Change) { ChangeContext.Add(_Change); }
    }
    public class SvnDiffParser
    {
        public void ParseUnitfiedDiff(string _unifiedDiff, out Dictionary<string, List<ChangeInfo>> _outchanges)
        {
            _outchanges = new Dictionary<string, List<ChangeInfo>>();
            _outchanges.Clear();

            string[] diffSections = _unifiedDiff.Split(new string[] { "Index: " }, StringSplitOptions.None);

            string currentFile = "";
            ChangeInfo? currentChange = null;
            string[] lines = _unifiedDiff.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                if(line.StartsWith("Index:"))
                {
                    string[] parts = line.Split(':');
                    if (parts.Length >= 2)
                    {
                        currentFile = parts[1].Trim();
                    }
                    if (!_outchanges.ContainsKey(currentFile))
                    {
                        _outchanges.Add(currentFile, new List<ChangeInfo>());
                    }
                }
                else if (line.StartsWith("@@"))
                {
                    ChangeInfo Change = new ChangeInfo();
                    Change.SetLine(line);
                    currentChange = Change;
                    _outchanges[currentFile].Add(Change);
                }
                else if (line.StartsWith("---") || line.StartsWith("+++"))
                {
                    // 파일 생성 유무
                }
                else if (line.StartsWith("+") || line.StartsWith("-"))
                {
                    // 변경된 라인 확인
                    if(currentChange != null)
                    {
                        currentChange.Value.AddChange(line);
                    }
                }
            }
        }
    }

    public delegate void OnCompleteDelegate(bool _Success);

    static class SvnHelper
    {
        // If you want default value, edit this
        static public string DefaultSVNUrl = "";
        public static bool CheckRepositoryUrl(string _Url)
        {
            if (_Url.Length <= 0)
                return false;
            
            try
            {
                using (SvnClient client = new SvnClient())
                {
                    SvnInfoEventArgs info;
                    bool success = client.GetInfo(new Uri(_Url), out info);

                    return success && info != null;
                }
            }
            catch (SvnRepositoryIOException ex)
            {
                Console.WriteLine("SvnRepositoryIOException occurred: " + ex.Message);
                return false;
            }
        }
        static private void client_Notify(object sender, SvnNotifyEventArgs e)
        {
            switch (e.CommandType)
            {
                case SvnCommandType.CheckOut:
                case SvnCommandType.Update:
                case SvnCommandType.Add:
                case SvnCommandType.Commit:
                    Console.WriteLine(e.FullPath + ", Rev. " + e.Revision + "\t : " + e.Action);
                    break;
                default:
                    break;
            }
        }
        public static Collection<SvnListEventArgs> GetRepoList(Uri _Uri)
        {
            Collection<SvnListEventArgs> SvnList = new Collection<SvnListEventArgs>();

            using (SvnClient client = new SvnClient())
            {
                SvnUriTarget repo = new SvnUriTarget(_Uri);

                try
                {
                    bool v = client.GetList(repo, out SvnList);
                }
                catch (SvnFileSystemException ex)
                {
                    MessageBox.Show("SvnFileSystemException 발생: " + ex.Message, "에러", MessageBoxButton.OK, MessageBoxImage.Error);
                    Console.WriteLine("SvnFileSystemException 발생: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("예상치 못한 예외 발생: " + ex.Message);
                }
            }

            return SvnList;
        }

        public static long GetRepoLogList(Uri _Uri, out Collection<SvnLogEventArgs> logList, long _StartRevision = -1)
        {
            logList = new Collection<SvnLogEventArgs>();
            long initialRevision = -1;

            using (SvnClient client = new SvnClient())
            {
                SvnLogArgs logArgs = new SvnLogArgs();
                logArgs.Limit = 100;
                logArgs.StrictNodeHistory = true;
                logArgs.Start = new SvnRevision(SvnRevisionType.Head);

                // _StartRevision이 지정된 경우 시작 리비전을 설정합니다.
                if (_StartRevision != -1)
                {
                    logArgs.Start = new SvnRevision(_StartRevision);
                }

                client.GetLog(_Uri, logArgs, out logList);

                if (logList.Count > 0)
                {
                    initialRevision = logList[logList.Count - 1].Revision;
                }
            }

            return initialRevision;
        }
        public static string GetDiff(string _AUrl, string _BUrl, long _ARevision, long _BRevision)
        {
            string strResult ="";
            using (SvnClient client = new SvnClient())
            {
                using (MemoryStream result = new MemoryStream())
                {
                    try
                    {
                        SvnDiffArgs svnDiffArgs = new SvnDiffArgs();
                        svnDiffArgs.Depth = SvnDepth.Infinity;
                        svnDiffArgs.UseGitFormat = false;
                        Uri AUrl = new Uri(_AUrl);
                        Uri BUrl = new Uri(_BUrl);

                        if (client.Diff(new SvnUriTarget(AUrl, _ARevision), new SvnUriTarget(BUrl, _BRevision), svnDiffArgs, result))
                        {
                            result.Position = 0;
                            using (StreamReader strReader = new StreamReader(result))
                            {
                                strResult = strReader.ReadToEnd();
                            }
                        }
                    }
                    catch (SvnException svnEx)
                    {
                        Console.WriteLine("SVN Exception: " + svnEx.Message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }
            }
            
            return strResult;
        }

        public static List<KeyValuePair<string, SvnDiffKind>> GetDiffSummary(string _AUrl, string _BUrl, long _ARevision, long _BRevision)
        {
            List<KeyValuePair<string, SvnDiffKind>> DiffFile = new List<KeyValuePair<string, SvnDiffKind>>();
            using (SvnClient client = new SvnClient())
            {
                try
                {
                    SvnDiffSummaryArgs svnDiffArgs = new SvnDiffSummaryArgs();
                    //svnDiffArgs.Depth = SvnDepth.Infinity;
                    //svnDiffArgs.IgnoreAncestry = false;
                    Uri AUrl = new Uri(_AUrl);
                    Uri BUrl = new Uri(_BUrl);

                    Collection<SvnDiffSummaryEventArgs> list;
                    if (client.GetDiffSummary(new SvnUriTarget(AUrl, _ARevision), new SvnUriTarget(BUrl, _BRevision), svnDiffArgs, out list))
                    {
                        foreach (SvnDiffSummaryEventArgs e in list)
                        {
                            if (e.NodeKind == SvnNodeKind.File)
                            {
                                DiffFile.Add(new KeyValuePair<string, SvnDiffKind>(e.Path, e.DiffKind));
                            }
                        }
                    }
                }
                catch (SvnException svnEx)
                {
                    Console.WriteLine("SVN Exception: " + svnEx.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }

            return DiffFile;
        }

        public static string GetFileString(string _TargetFileUrl, long _Revision)
        {
            string content = "";
            try
            {
                using (SvnClient client = new SvnClient())
                {
                    MemoryStream stream = new MemoryStream();
                    client.Write(new SvnUriTarget(new Uri(_TargetFileUrl)), stream, new SvnWriteArgs { Revision = new SvnRevision(_Revision) });
                    stream.Position = 0;
                    StreamReader reader = new StreamReader(stream);
                    content = reader.ReadToEnd();
                }
            }
            catch (SharpSvn.SvnFileSystemException ex)
            {
                // 파일 시스템 예외 처리
                Console.WriteLine("File System Exception: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("System Exception: " + ex.Message);
            }
            return content;
        }
        public static bool GetLog(Uri _OrginUrl, SvnRevision _startRevision, SvnRevision _endRevision, out Collection<SvnLogEventArgs> _LogItems)
        {
            using (SvnClient client = new SvnClient())
            {
                SvnLogArgs logArgs = new SvnLogArgs
                {
                    RetrieveMergedRevisions = true,
                    RetrieveAllProperties = true,
                    RetrieveChangedPaths = true,
                    StrictNodeHistory = false,
                    Start = _startRevision,
                    End = _endRevision,
                };
                if (client.GetLog(_OrginUrl, logArgs, out _LogItems))
                {
                    return true;
                }
            }
            return false;
        }

        public static void GetFileHistoryRecursive(string Filename, Uri _OrginUri, SvnRevision _startRevision, SvnRevision _endRevision, Dictionary<string, Dictionary<string, List<SvnLogEventArgs>>> _History)
        {
            Collection<SvnLogEventArgs> LogItems;

            if (GetLog(_OrginUri, _startRevision, _endRevision, out LogItems))
            {
                foreach (SvnLogEventArgs LogItem in LogItems)
                {
                    if (LogItem.MergeLogNestingLevel != 0 && LogItem.ChangedPaths != null)
                    {
                        foreach (var subChangePath in LogItem.ChangedPaths)
                        {
                            Uri subsourceUri = new Uri(DefaultSVNUrl + subChangePath.Path);

                            if (false == CheckRepositoryUrl(subsourceUri.ToString()))
                                continue;

                            string[] OrginUrlArray = _OrginUri.ToString().Split('/');
                            string[] SubUrlArray = subsourceUri.ToString().Split('/');
                            
                            if (OrginUrlArray.Length > 2 && SubUrlArray.Length > 2)
                            {
                                string[] lhs = new string[2];
                                string[] rhs = new string[2];

                                lhs[0] = OrginUrlArray[OrginUrlArray.Length - 2];
                                lhs[1] = OrginUrlArray[OrginUrlArray.Length - 1];

                                rhs[0] = SubUrlArray[SubUrlArray.Length - 2];
                                rhs[1] = SubUrlArray[SubUrlArray.Length - 1];

                                OrginUrlArray = lhs;
                                SubUrlArray = rhs;
                            }

                            if (OrginUrlArray.Length != SubUrlArray.Length)
                                continue;

                            int DiffCount = 0;
                            for (int i = 0; i < OrginUrlArray.Length; ++i)
                            {
                                if (OrginUrlArray[i] != SubUrlArray[i])
                                {
                                    DiffCount++;
                                }
                            }

                            if (DiffCount > 0)
                                continue;

                            GetFileHistoryRecursive(Filename, subsourceUri, _startRevision, _endRevision, _History);
                        }
                    }
                    else
                    {
                        if (!_History.TryGetValue(Filename, out Dictionary<string, List<SvnLogEventArgs>>? fileLogs))
                        {
                            fileLogs = new Dictionary<string, List<SvnLogEventArgs>>();
                            _History[Filename] = fileLogs;
                        }

                        if (!_History[Filename].TryGetValue(LogItem.Author, out List<SvnLogEventArgs>? HistoryItem))
                        {
                            HistoryItem = new List<SvnLogEventArgs>();
                            _History[Filename][LogItem.Author] = HistoryItem;
                        }

                        Predicate<SvnLogEventArgs> condition = s => s.Revision == LogItem.Revision;
                        if (false == HistoryItem.Exists(condition))
                        {
                            HistoryItem.Add(LogItem);
                        }
                    }
                }
            }
        }
        public static void GetFileHistory(List<KeyValuePair<string, SvnDiffKind>> _FileList, string _reposUrl, SvnRevision _startRevision, SvnRevision _endRevision,
            Dictionary<string, Dictionary<string, List<SvnLogEventArgs>>> _History, LoadingWindow LoadingBar)
        {
            using (SvnClient client = new SvnClient())
            {
                //client.Authentication.DefaultCredentials = new System.Net.NetworkCredential("", "");

                int Count = 0;
                foreach (var Filename in _FileList)
                {
                    Uri sourceUri = new Uri(_reposUrl + "/" + Filename.Key);

                    if (false == CheckRepositoryUrl(sourceUri.ToString()))
                        continue;

                    GetFileHistoryRecursive(Filename.Key, sourceUri, _startRevision, _endRevision, _History);
                    Count++;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        LoadingBar.OnUpdatedelegate.Invoke(string.Format("{0} 파일 로그 추적 중.", Filename.Key), Count, _FileList.Count);
                    });
                }
                Console.WriteLine(Count);
            }
        }

        public static void GetFileWorkers(string _repositoryUrl, long _startRevision, long _endRevision,
            Dictionary<string, Dictionary<string, List<SvnLogEventArgs>>> _History)
        {
            using (SvnClient client = new SvnClient())
            {
                //client.Authentication.DefaultCredentials = new System.Net.NetworkCredential("", "");

                Collection<SvnLogEventArgs> logItems;

                SvnLogArgs logArgs = new SvnLogArgs
                {
                    //RetrieveMergedRevisions = true,
                    //RetrieveAllProperties = true,
                    //RetrieveChangedPaths = true,
                    StrictNodeHistory = false,
                    Start = _startRevision,
                    End = _endRevision
                };

                client.GetLog(new Uri(_repositoryUrl), logArgs, out logItems);

                ProcessLogItems(logItems, _History);
            }
        }
       
        static void ProcessLogItems(IEnumerable<SvnLogEventArgs> logItems, Dictionary<string, Dictionary<string, List<SvnLogEventArgs>>> _History)
        {
            foreach (var logItem in logItems)
            {
                string Author = logItem.Author;

                foreach (var changePath in logItem.ChangedPaths)
                {
                    if (changePath.NodeKind != SvnNodeKind.File)
                        continue;

                    //특정 SVN 환경에서 허용하지 않는 프로퍼티여서 사용 불가
                    //SvnPropertyValue properties;
                    //bool v = client.GetProperty(new Uri(DefaultSVNUrl + changePath.Path), "svn:mergeinfo", out properties);

                    string? Path = System.IO.Path.GetDirectoryName(changePath.Path);
                    if (Path == null)
                        continue;
                    Path = Path.Replace('\\', '/');
                    
                    string filename = System.IO.Path.GetFileName(changePath.Path);

                    //int MaxMessage = 80;
                    //string LogMessageLine = logItem.LogMessage.Replace("\r", "").Replace("\n", "");
                    //LogMessageLine = LogMessageLine.Length <= MaxMessage ? LogMessageLine : LogMessageLine.Substring(0, MaxMessage);

                    if (!_History.TryGetValue(filename, out Dictionary<string, List<SvnLogEventArgs>> fileLogs))
                    {
                        fileLogs = new Dictionary<string, List<SvnLogEventArgs>>();
                        _History[filename] = fileLogs;
                    }

                    if (!fileLogs.TryGetValue(logItem.Author, out List<SvnLogEventArgs> history))
                    {
                        history = new List<SvnLogEventArgs>();
                        fileLogs[Author] = history;
                    }

                    history.Add(logItem);
                }
            }
        }

        public static void GetBlame(long _TargetRev, string _TargetReposUrl,  out Collection<SvnBlameEventArgs> _BlameList)
        {
            _BlameList = new Collection<SvnBlameEventArgs>();

            using (SvnClient client = new SvnClient())
            {
                Uri sourceUri = new Uri(_TargetReposUrl);

                if (false == CheckRepositoryUrl(sourceUri.ToString()))
                    return;
                try
                {
                    SvnBlameArgs args = new SvnBlameArgs()
                    {
                        IgnoreMimeType = false,
                        RetrieveMergedRevisions = true,
                        Start = SvnRevision.Zero,
                        End = _TargetRev,
                    };
                    if (client.GetBlame(SvnTarget.FromUri(sourceUri), args, out _BlameList))
                    {
                    }
                }
                catch (SharpSvn.SvnClientBinaryFileException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        static void TrackOriginalSourceAndRevision(string OrginSourceUrl, long OriginRevision, Dictionary<string, Dictionary<string, List<string>>> diffFileAndAuthor)
        {
            using (SvnClient client = new SvnClient())
            {
                SvnLogArgs logArgs = new SvnLogArgs
                {
                    Limit = 1,
                    Start = new SvnRevision(OriginRevision)
                };

                client.GetLog(new Uri(OrginSourceUrl), logArgs, out var logItems);

                foreach (var logItem in logItems)
                {
                    string originAuthor = logItem.Author;
                    string originRevision = logItem.Revision.ToString();

                    // Add the original author and revision to the dictionary
                    string filename = System.IO.Path.GetFileName(OrginSourceUrl);
                    if (!diffFileAndAuthor.TryGetValue(filename, out Dictionary<string, List<string>> fileLogs))
                    {
                        fileLogs = new Dictionary<string, List<string>>();
                        diffFileAndAuthor[filename] = fileLogs;
                    }

                    if (!fileLogs.TryGetValue(originAuthor, out List<string> history))
                    {
                        history = new List<string>();
                        fileLogs[originAuthor] = history;
                    }

                    history.Add(originRevision);

                    if (logItem.ChangedPaths != null)
                    {
                        foreach (var changePath in logItem.ChangedPaths)
                        {
                            if (changePath.Action == SvnChangeAction.Add || changePath.Action == SvnChangeAction.Replace)
                            {
                                string newSource = changePath.Path;
                                long newRevision = changePath.CopyFromRevision;

                                TrackOriginalSourceAndRevision(newSource, newRevision, diffFileAndAuthor);
                            }
                        }
                    }
                }
            }
        }

        public struct BackgroundProcess
        {
            public Action<LoadingWindow> Func;
            public LoadingWindow Loading;
        }
        public static void StartWork(BackgroundProcess BackgoundProcess)
        {
            Thread DownloadThread = new Thread(new ParameterizedThreadStart(Work));
            DownloadThread.SetApartmentState(ApartmentState.STA);
            DownloadThread.Start(BackgoundProcess);
        }

        private static void Work(object? _Obj)
        {
            try
            {
                BackgroundProcess backgroundProcess = (BackgroundProcess)_Obj;
                backgroundProcess.Func?.Invoke(backgroundProcess.Loading);
            }
            catch (InvalidCastException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

}
