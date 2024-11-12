using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;

namespace SvnDiffTool.GoogleSheet
{
    public class GoogleInstance
    {
        private SheetsService? SheetsService;
        private DriveService? DriveService;

        private string SperedSheedid = "";
        private List<Request> SavedUpdateRequests = new();
        private Dictionary<string, SheetProperties> SheetResult = new(); //key title, value, SheetProperties

        public void ClearRequest()
        {
            SavedUpdateRequests.Clear();
        }
        private const long MaxSizeBytes = 134217728; // 128 MB in bytes
        public bool ValidRequestSize()
        {
            try
            {
                string json = JsonSerializer.Serialize(SavedUpdateRequests);
                long sizeInBytes = json.Length * sizeof(char);

                Console.WriteLine($"Size of requests in bytes: {sizeInBytes}");

                // 크기 제한의 25%를 초과하면 false 반환 : 무료 계정
                if (sizeInBytes > MaxSizeBytes * 0.25)
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking request size: {ex.Message}");
                return false;
            }
        }
        public async Task<bool> ExecuteRequest()
        {
            if (!IsServiceReady())
            {
                Console.WriteLine("Services are not initialized.");
                throw new InvalidOperationException("Services is not initialized.");
            }

            if (SavedUpdateRequests.Count > 0)
            {
                BatchUpdateSpreadsheetRequest batchUpdateRequest = new BatchUpdateSpreadsheetRequest();
                batchUpdateRequest.Requests = SavedUpdateRequests;
                    
                var response = await SheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, SperedSheedid).ExecuteAsync();

                // 시트 업데이트일 경우 저장
                foreach (var res in response.Replies)
                {
                    if (res.AddSheet != null)
                    {
                        if (res.AddSheet.Properties != null)
                        {
                            SheetResult.Add(res.AddSheet.Properties.Title, res.AddSheet.Properties);
                        }
                    }
                }
                ClearRequest();
                return true;
            }
            
            ClearRequest();
            return false;
        }
        // bool success = googleInstance.DoCredentialAsync().Result;
        public async Task<bool> DoCredentialAsync()
        {
            const string credentialPath = "token.json";
            const string credentialsFilePath = "credentials.json";

            if (!File.Exists(credentialsFilePath))
            {
                // Google에서 발급받는 인증 Json 파일 
                Console.WriteLine("Credentials file not found.");
                return false;
            }

            UserCredential Cred;
            try
            {
                using (var stream = new FileStream(credentialsFilePath, FileMode.Open, FileAccess.Read))
                {
                    Cred = GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        new[] { SheetsService.Scope.Spreadsheets, DriveService.Scope.Drive },
                        "user",
                        CancellationToken.None,
                        new FileDataStore(credentialPath, true)).Result;
                }

                if (Cred.Token == null)
                {
                    return false;
                }

                MessageBox.Show("인증 완료", "google drive", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.None);
                SheetsService = new SheetsService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = Cred,
                    ApplicationName = "SvnDiffTool",
                });

                DriveService = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = Cred,
                    ApplicationName = "SvnDiffTool",
                });

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during authentication: {ex.Message}");
                return false;
            }
        }

        private bool IsServiceReady()
        {
            return SperedSheedid == "" || SheetsService != null || DriveService != null;
        }

        public async Task<string> InitCreateSheet(string filename)
        {
            if (!IsServiceReady())
            {
                Console.WriteLine("Services are not initialized.");
                throw new InvalidOperationException("Services is not initialized.");
            }

            try
            {
                var existingFiles = DriveService.Files.List().Execute().Files
                    .Where(f => f.Name == filename && f.MimeType == "application/vnd.google-apps.spreadsheet")
                    .ToList();

                foreach (var file in existingFiles)
                {
                    DriveService.Files.Delete(file.Id).Execute();
                    Console.WriteLine($"기존 파일 삭제 with ID: {file.Id}");
                }

                var reqCreateFile = DriveService.Files.Create(new Google.Apis.Drive.v3.Data.File
                {
                    Name = filename,
                    MimeType = "application/vnd.google-apps.spreadsheet"
                });

                var Createfile = reqCreateFile.Execute();
                SperedSheedid = Createfile.Id;

                Console.WriteLine($"파일 생성 with ID: {SperedSheedid}");
                return SperedSheedid;
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                Console.WriteLine($"An error occurred: {ex.Message}");
                return null;
            }
        }

        public void AddSheetRequest(string title)
        {
            if (!IsServiceReady())
            {
                Console.WriteLine("Services are not initialized.");
                throw new InvalidOperationException("Services are not initialized.");
            }

            AddSheetRequest_Internal(SperedSheedid, title);
        }
        
        public void AddSheetUpdateRequest(SvnDiffInfo svnInfo , string title,
            List<List<ExportHelper.CellInfo>> dataA, List<List<ExportHelper.CellInfo>> dataB)
        {
            if (!IsServiceReady())
            {
                Console.WriteLine("Services are not initialized.");
                throw new InvalidOperationException("Services are not initialized.");
            }

            if (SheetResult.ContainsKey(title))
            {
                AddSheetUpdateRequest_Internal(svnInfo, SheetResult[title], dataA, dataB);
            }
            else
            {
                MessageBox.Show("오류", "예외사항", MessageBoxButton.OK, MessageBoxImage.Error,
                    MessageBoxResult.OK, MessageBoxOptions.None);
            }
        }

        private async Task RenameSheetAsync(string spreadsheetId, int? sheetId, string newTitle)
        {
            var updateRequest = new Request
            {
                UpdateSheetProperties = new UpdateSheetPropertiesRequest
                {
                    Properties = new SheetProperties
                    {
                        SheetId = sheetId,
                        Title = newTitle
                    },
                    Fields = "title"
                }
            };

            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request> { updateRequest }
            };

            await SheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId).ExecuteAsync();
            Console.WriteLine("Sheet name has been changed.");
        }

        private void AddSheetRequest_Internal(string spreadsheetId, string title)
        {
            var addRequest = new Request
            {
                AddSheet = new AddSheetRequest
                {
                    Properties = new SheetProperties
                    {
                        Title = title
                    }
                }
            };

            SavedUpdateRequests.Add(addRequest);
            Console.WriteLine("Add SavedUpdateRequests Completed..");
        }

        private void AddSheetUpdateRequest_Internal(SvnDiffInfo svnissuck, SheetProperties sheetProperties,
            List<List<ExportHelper.CellInfo>> dataA, List<List<ExportHelper.CellInfo>> dataB)
        {
            if (!IsServiceReady())
            {
                Console.WriteLine("Services are not initialized.");
                throw new InvalidOperationException("Services are not initialized.");
            }

            int numRows = (dataA.Count + dataB.Count) + 2;
            int numCols = new[] { dataA, dataB }
                .SelectMany(data => data ?? Enumerable.Empty<List<ExportHelper.CellInfo>>())
                .Select(row => row?.Count ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            
            int currentColumns = sheetProperties.GridProperties.ColumnCount ?? 0;
            int currentRows = sheetProperties.GridProperties.RowCount ?? 0;
            
            var reqList = new List<Request>();
            if (numRows > currentRows)
            {
                reqList.Add(AppendDimensionUpdateRequest(sheetProperties.SheetId, "ROWS", numRows - currentRows));
            }

            if (numCols > currentColumns)
            {
                reqList.Add(AppendDimensionUpdateRequest(sheetProperties.SheetId, "COLUMNS", numCols - currentColumns));
            }
            
            var updateRowData = new List<RowData>();
            updateRowData.AddRange(StringToRowData($"Path : {svnissuck.ArepositoryURL} Revision : {svnissuck.previousRevision}"));
            updateRowData.AddRange(CellInfoToRowData(dataA));
            updateRowData.AddRange(StringToRowData($"Path : {svnissuck.BrepositoryURL} Revision : {svnissuck.currentRevision}"));
            updateRowData.AddRange(CellInfoToRowData(dataB));
            
            if (updateRowData.Count > 0)
            {
                for (int rowIndex = 0; rowIndex < updateRowData.Count; rowIndex++)
                {
                    var updateCellsRequest = new Request
                    {
                        UpdateCells = new UpdateCellsRequest
                        {
                            Rows = new List<RowData> { updateRowData[rowIndex] },
                            Fields = "userEnteredValue,userEnteredFormat",
                            Start = new GridCoordinate
                            {
                                SheetId = sheetProperties.SheetId,
                                RowIndex = rowIndex,
                                ColumnIndex = 0
                            }
                        }
                    };

                    reqList.Add(updateCellsRequest);
                }
            }

            SavedUpdateRequests.AddRange(reqList);
            Console.WriteLine("Add SavedUpdateRequests Completed..");
        }

        private Request AppendDimensionUpdateRequest(int? sheetId, string dimension, int appendLength)
        {
            return new Request
            {
                AppendDimension = new AppendDimensionRequest
                {
                    Dimension = dimension,
                    Length = appendLength,
                    SheetId = sheetId
                }
            };
        }

        private List<RowData> CellInfoToRowData(List<List<ExportHelper.CellInfo>> cellInfo)
        {
            var pRowDatas = new List<RowData>();

            for (int rowIndex = 0; rowIndex < cellInfo.Count; rowIndex++)
            {
                var row = cellInfo[rowIndex];
                var rowData = new RowData
                {
                    Values = row.Select(cell => new CellData
                    {
                        UserEnteredValue = new ExtendedValue
                        {
                            StringValue = cell.Context
                        },
                        UserEnteredFormat = new CellFormat
                        {
                            BackgroundColor = new Color
                            {
                                Red = cell.Color.Color.R / 255f,
                                Green = cell.Color.Color.G / 255f,
                                Blue = cell.Color.Color.B / 255f
                            },
                            TextFormat = new TextFormat
                            {
                                ForegroundColor = new Color { Red = 0.0f, Green = 0.0f, Blue = 0.0f },
                                FontSize = 10,
                                Bold = false
                            }
                        }
                    }).ToList()
                };
                pRowDatas.Add(rowData);
            }
            return pRowDatas;
        }
        private List<RowData> StringToRowData(string _strContext)
        {
            var pRowDatas = new List<RowData>();
            var rowData = new RowData
            {
                Values = new[]
                {
                    new CellData
                    {
                        UserEnteredValue = new ExtendedValue
                        {
                            StringValue = _strContext
                        },
                        UserEnteredFormat = new CellFormat
                        {
                            TextFormat = new TextFormat
                            {
                                ForegroundColor = new Color { Red = 0.0f, Green = 0.0f, Blue = 0.0f },
                                FontSize = 10,
                                Bold = true
                            }
                        }
                    }
                }
            };
            pRowDatas.Add(rowData);
            
            return pRowDatas;
        }
    }
}