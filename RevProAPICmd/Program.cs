// See https://aka.ms/new-console-template for more information

using System.Globalization;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Text;


//revpro command line interface
namespace RevproAPICmd
{

    internal class Program
    {
        private static void Main(string[] args)
        {
            string userInputCmd = "";
            string userInputFileNamePath = "";

            if (args.Length == 0 || args.Length > 2 || args[0] == "-h" || args[0] == "--help")
            {
                PrintHelpMessage();
                return;
            }

            userInputCmd = args[0];

            if (args.Length == 2)
            {
                userInputFileNamePath = args[1];
            }

            switch (userInputCmd)
            {
                case "-split":
                    if (string.IsNullOrEmpty(userInputFileNamePath))
                    {
                        Console.WriteLine("Please provide a file path.");
                    }
                    else
                    {
                        Console.WriteLine($"Splitting CSV file {userInputFileNamePath}");
                        RevproAPI.SplitCsv(userInputFileNamePath, 10, false);
                    }
                    break;

                case "-repair":
                    if (string.IsNullOrEmpty(userInputFileNamePath))
                    {
                        Console.WriteLine("Please provide a file path.");
                    }
                    else
                    {
                        Console.WriteLine($"Checking for errors in CSV {userInputFileNamePath}");
                        RevproAPI.RepairCsvFile(userInputFileNamePath);
                    }
                    break;

                default:
                    if (DateTime.TryParseExact(userInputCmd, new[] { "MMddyyyy", "MM-dd-yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                    {
                        Console.WriteLine($"Running with date: {parsedDate}");
                        RunWithDate(parsedDate);
                    }
                    else
                    {
                        Console.WriteLine("Invalid command or date format. Use '-h' or '--help' for usage instructions.");
                    }
                    break;
            }

        }

        private static void RunWithDate(DateTime d)
        {
            //locate the settings file and open it
            string jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "revprosettings.json");
            if (!File.Exists(jsonFilePath))
            {
                Console.WriteLine($"File {jsonFilePath} not found!");
                return;
            }
            string jsonContent = File.ReadAllText(jsonFilePath);

            SettingsFileRootObject? optionsList = JsonConvert.DeserializeObject<SettingsFileRootObject>(jsonContent);

            if (optionsList?.RevproAPIOptionsList == null || optionsList.RevproAPIOptionsList.Count == 0)
            {
                Console.WriteLine("Error: No API options found in settings file.");
                return;
            }

            foreach (RevproAPI.RevproAPIOptions options in optionsList.RevproAPIOptionsList)
            {
                Console.WriteLine($"LogDir: {options.LogDir}");
                RevproAPI revproAPI = new(options);

                //****** example options object - Update this when you update any of the options stuff  ******
                // RevproAPI.RevproAPIOptions options = new()
                // {
                // LogDir = @"C:\Scripts\ScriptLogs\",
                // LogFile = "RevproScriptLogfile.txt",
                // RevproAPIClientName = "Default",
                // RevproAPIRole = "FULL REPORTS",
                // RevproAPIUser = "com_api",
                // RevproAPIPwd = "somepwd", 
                // RevproAuthURI = "https://company.revprooncloud.com/api/integration/v1/authenticate",
                // RevproReportListURI = "https://company.revprooncloud.com/api/integration/v1/reports/list",
                // RevproSignedUrlUri = "https://company.revprooncloud.com/api/integration/v2/reports/signedurl/",
                // RevproTokenName = "Revpro-Token",
                // RevproLayoutName = "Monthly_Finance_Revenue",
                // RevproReportName = "Accounting Report",
                // ReportDirectory = @"C:\Scripts\Reports\",
                // ZipFileDownloadDirectory = @"C:\Scripts\ZipReports\",
                // ReportFileNameExtension = "csv"
                // };

                // RevproAPI revproAPI = new(options);
                string token = revproAPI.GetAuthToken().Result;
                if (token != "")
                {
                    List<RevproAPI.RevproReportMetadata> revproReports = revproAPI.GetReportList(token, d).Result;

                    foreach (RevproAPI.RevproReportMetadata rrmetadata in revproReports)
                    {
                        string signedURL = revproAPI.GetSignedURL(token, rrmetadata.id).Result;
                        if (signedURL.Any())
                        {
                            string downloadedFilePath = revproAPI.DownloadFileFromSignedURL(signedURL, rrmetadata.file_name).Result;

                            if (downloadedFilePath != "")
                            {
                                revproAPI.UnzipFile(Path.Combine(options.ZipFileDownloadDirectory, rrmetadata.file_name), downloadedFilePath);
                            }
                        }

                    }
                }
                else
                {
                    Console.WriteLine($"Error while trying to get authorization from RevproApi - Auth token response is empty!");
                }

            }

            Console.WriteLine($"{Environment.NewLine}RevProAPICmd completed.");
            return;

        }

        private static void PrintHelpMessage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  RevProAPICmd -split <file_path>      Split the specified CSV file.");
            Console.WriteLine("  RevProAPICmd -repair <file_path>     Check and repair errors in the specified CSV file.");
            Console.WriteLine("  RevProAPICmd <date>                  Run with the specified date in 'MMddyyyy' or 'MM-dd-yyyy' format.");
            Console.WriteLine("  RevProAPICmd -h | --help             Display this help message.");
            return;
        }

    }

    //The JSON object in the settings file conforms to this on deserialization
    public class SettingsFileRootObject
    {
        public List<RevproAPI.RevproAPIOptions>? RevproAPIOptionsList { get; set; }
    }

    public class RevproAPI
    {
        private string _logDir;
        private string _logFile;
        private string _revproApiClientName;
        private string _revproApiRole;
        private string _revproUsr;
        private string _revproPwd;
        private string _revproAuthUri;
        private string _revproReportListUri;
        private string _revproSignedUrlUri;
        private string _revproTokenName;
        private string _revproLayoutName;
        private string _revproReportName;
        private string _zipFileDownloadDirectory;
        private string _reportDirectory;
        private string _reportFileNameExtension;

        public RevproAPI(RevproAPIOptions options)
        {
            _logDir = options.LogDir;
            _logFile = options.LogFile;
            _revproApiClientName = options.RevproAPIClientName;
            _revproApiRole = options.RevproAPIRole;
            _revproUsr = options.RevproAPIUser;
            _revproPwd = options.RevproAPIPwd;
            _revproAuthUri = options.RevproAuthURI;
            _revproReportListUri = options.RevproReportListURI;
            _revproSignedUrlUri = options.RevproSignedUrlUri;
            _revproTokenName = options.RevproTokenName;
            _revproLayoutName = options.RevproLayoutName;
            _revproReportName = options.RevproReportName;
            _zipFileDownloadDirectory = options.ZipFileDownloadDirectory;
            _reportDirectory = options.ReportDirectory;
            _reportFileNameExtension = options.ReportFileNameExtension;
        }

        //options class with default options
        public class RevproAPIOptions
        {
            public string LogDir { get; set; } = "";
            public string LogFile { get; set; } = "";
            public string RevproAPIClientName { get; set; } = "";
            public string RevproAPIRole { get; set; } = "";
            public string RevproAPIUser { get; set; } = "";
            public string RevproAPIPwd { get; set; } = "";
            public string RevproAuthURI { get; set; } = "";
            public string RevproReportListURI { get; set; } = "";
            public string RevproSignedUrlUri { get; set; } = "";
            public string RevproTokenName { get; set; } = "";
            public string RevproLayoutName { get; set; } = "";
            public string RevproReportName { get; set; } = "";
            public string ZipFileDownloadDirectory { get; set; } = "";
            public string ReportDirectory { get; set; } = "";
            public string ReportFileNameExtension { get; set; } = "";
        }

        #region utility classes
        public class RevproReportMetadata
        {
            public string? category { get; set; }
            public string? file_name { get; set; }
            public int? id { get; set; }
            public string? layout_name { get; set; }
            public string? rep_desc { get; set; }
            public string? rep_name { get; set; }
            public string? report_date { get; set; }
            public string? status { get; set; }
        }

        public class ApiReportListResponse
        {
            public string? Message { get; set; }
            public List<RevproReportMetadata>? Result { get; set; }
            public string? Status { get; set; }
        }

        public class APISignedURLResponse
        {
            public string? signed_url { get; set; }
            public bool success { get; set; } = false;
        }

        public class UrlResponse
        {
            [JsonProperty("signed_url")]
            public string? SignedUrl { get; set; }

            [JsonProperty("success")]
            public bool Success { get; set; }
        }

        public class ErrorResponse400
        {
            [JsonProperty("Message")]
            public string? Message { get; set; }

            [JsonProperty("Result")]
            public string? Result { get; set; }

            [JsonProperty("status")]
            public string? Status { get; set; }
        }

        public class ErrorResponse404
        {
            [JsonProperty("error")]
            public string? Error { get; set; }

            [JsonProperty("success")]
            public bool Success { get; set; }
        }

        #endregion

        //get a signed url from the revpro api. signed url is used for downloading a report. 
        //Reference: https://www.zuora.com/developer/api-references/revenue/operation/GET_ReportsURL/
        public async Task<string> GetSignedURL(string token, int? id)
        {
            var baseAddress = new Uri(_revproSignedUrlUri);
            using var client = new HttpClient { BaseAddress = baseAddress };
            APISignedURLResponse? apiresult = new();

            //add the token to the request headers
            if (!client.DefaultRequestHeaders.TryAddWithoutValidation("token", token))
            {
                string failMessage = $"Error: GetSignedURL - Unable to add token to client request headers.";
                Console.WriteLine(failMessage);
                WriteLog(Path.Combine(_logDir, _logFile), failMessage);
                throw new Exception(failMessage);
            }

            //send id and token to API and await response
            var response = await client.GetAsync($"{id}");

            //if success, deserialize result to metadata list
            if (response.IsSuccessStatusCode)
            {
                string responseData = await response.Content.ReadAsStringAsync();
                apiresult = JsonConvert.DeserializeObject<APISignedURLResponse>(responseData);

                string successMessage = $"Success: StatusCode: {response.StatusCode}, Reason: {response.ReasonPhrase}";
                string successInfo = $"Retrieved signed url for report_id: {id}. Signed URL = {responseData}";
                Console.WriteLine(successMessage);
                Console.WriteLine(successInfo);
                WriteLog(Path.Combine(_logDir, _logFile), successMessage);
                WriteLog(Path.Combine(_logDir, _logFile), successInfo);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    ErrorResponse404? errorResponse = JsonConvert.DeserializeObject<ErrorResponse404>(errorContent);
                    string errorMessage = $"Error in GetSignedURL: {errorResponse?.Error}. There was no report available at this location: {response.RequestMessage?.RequestUri}";
                    Console.WriteLine(errorMessage);
                    WriteLog(Path.Combine(_logDir, _logFile), errorMessage);
                    return "";

                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    ErrorResponse400? errorResponse = JsonConvert.DeserializeObject<ErrorResponse400>(errorContent);
                    string errorMessage = $"Error GetSignedURL: {errorResponse?.Message ?? string.Empty}, Status: {errorResponse?.Status}";
                    Console.WriteLine(errorMessage);
                    WriteLog(Path.Combine(_logDir, _logFile), errorMessage);
                    return "";

                }
                else
                {
                    var errorResponse = errorContent;
                    string errorMessage = $"Error in GetSignedURL: {errorResponse}.";
                    Console.WriteLine(errorMessage);
                    WriteLog(Path.Combine(_logDir, _logFile), errorMessage);
                    throw new Exception(errorMessage);
                }
            }

            return apiresult?.signed_url ?? "";

        }

        //Gets a list of available reports that are created on a specified date.
        //Reference: https://www.zuora.com/developer/api-references/revenue/operation/GET_ReportList/
        public async Task<List<RevproReportMetadata>> GetReportList(string mytoken, DateTime d)
        {
            List<RevproReportMetadata> revproReports = new();
            List<RevproReportMetadata> revproReportsFiltered = new();
            string createddate = d.ToString("dd-MMM-yyyy");
            string createddateparameter = $"?createddate={createddate}";
            var baseAddress = new Uri(_revproReportListUri);
            using var client = new HttpClient { BaseAddress = baseAddress };

            //add the token to the request headers
            if (!client.DefaultRequestHeaders.TryAddWithoutValidation("token", mytoken))
            {
                string failMessage = $"Error: GetReportList - Unable to add token to client request headers.";
                Console.WriteLine(failMessage);
                WriteLog(Path.Combine(_logDir, _logFile), failMessage);
                return revproReports;
            }

            //Get response
            var response = await client.GetAsync(createddateparameter);

            //if success, deserialize result to metadata list
            if (response.IsSuccessStatusCode)
            {
                string responseData = await response.Content.ReadAsStringAsync();

                ApiReportListResponse? apiResponse = JsonConvert.DeserializeObject<ApiReportListResponse>(responseData);
                revproReports = apiResponse?.Result ?? new List<RevproReportMetadata>();

                //filter the list down to the layout and report name in the options
                foreach (RevproReportMetadata revproReportMetadata in revproReports)
                {
                    string msg = $"Checking for - LayoutName:{this._revproLayoutName} and ReportName:{this._revproReportName} - ";
                    if (revproReportMetadata.layout_name == this._revproLayoutName && revproReportMetadata.rep_name == this._revproReportName)
                    {
                        revproReportsFiltered.Add(revproReportMetadata);
                        msg = $"{msg} Found!";
                    }
                    else
                    {
                        msg = $"{msg} NOT Found!";
                    }
                    Console.WriteLine(msg);
                    WriteLog(Path.Combine(_logDir, _logFile), msg);
                }

                string successMessage = $"Success: StatusCode: {response.StatusCode}, Reason: {response.ReasonPhrase}";
                string successInfo = $"Retrieved metadata for {revproReports.Count} reports. Found {revproReportsFiltered.Count} reports matching LayoutName:{this._revproLayoutName} and ReportName:{this._revproReportName}";
                Console.WriteLine(successMessage);
                Console.WriteLine(successInfo);
                WriteLog(Path.Combine(_logDir, _logFile), successMessage);
                WriteLog(Path.Combine(_logDir, _logFile), successInfo);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var errorResponse = JsonConvert.DeserializeObject<ErrorResponse404>(errorContent);
                    string errorMessage = $"Error: {errorResponse?.Error}";
                    Console.WriteLine(errorMessage);
                    WriteLog(Path.Combine(_logDir, _logFile), errorMessage);
                    throw new Exception(errorMessage);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var errorResponse = JsonConvert.DeserializeObject<ErrorResponse400>(errorContent);
                    string errorMessage = $"Error: {errorResponse?.Message}, Status: {errorResponse?.Status}";
                    Console.WriteLine(errorMessage);
                    WriteLog(Path.Combine(_logDir, _logFile), errorMessage);
                    throw new Exception(errorMessage);
                }
            }

            return revproReportsFiltered;
        }

        //Downloads a file from a signed url and puts it into given destination directory
        //returns the full name and path of the file to unzip to
        public async Task<string> DownloadFileFromSignedURL(string url, string file_name)
        {
            string unzipToFileFullPath = "";

            using var httpClient = new HttpClient();
            try
            {
                byte[] bytes = await httpClient.GetByteArrayAsync(url);

                string zipFilePath = Path.Combine(this._zipFileDownloadDirectory, file_name);
                //unzipToFileFullPath = Path.Combine(this._reportDirectory, Path.ChangeExtension(file_name, this._reportFileNameExtension));
                unzipToFileFullPath = Path.Combine(this._reportDirectory);

                Directory.CreateDirectory(Path.GetDirectoryName(zipFilePath) ?? Directory.GetCurrentDirectory());
                Directory.CreateDirectory(Path.GetDirectoryName(unzipToFileFullPath) ?? Directory.GetCurrentDirectory());

                await File.WriteAllBytesAsync(zipFilePath, bytes);

                string successMessage = "File downloaded successfully.";
                Console.WriteLine(successMessage);
                WriteLog(Path.Combine(_logDir, _logFile), successMessage);
            }
            catch (Exception ex)
            {
                string errorMessage = $"An error occurred while downloading the file: {ex.Message}";
                Console.WriteLine(errorMessage);
                WriteLog(Path.Combine(_logDir, _logFile), errorMessage);
                return "";
            }

            return unzipToFileFullPath;

        }

        //unzip zip file located at zipfilepathandname, and save to filename
        public bool UnzipFile(string zipfilepathandname, string filename)
        {
            try
            {
                if (File.Exists(zipfilepathandname))
                {
                    ZipFile.ExtractToDirectory(zipfilepathandname, filename);

                }

                string successMessage = "File unzipped successfully.";
                Console.WriteLine(successMessage);
                WriteLog(Path.Combine(_logDir, _logFile), successMessage);
                return true;
            }
            catch (Exception ex)
            {
                string errorMessage = $"An error occurred while unzipping the file: {ex.Message}";
                Console.WriteLine(errorMessage);
                WriteLog(Path.Combine(_logDir, _logFile), errorMessage);
                return false;
            }
        }

        //authenticate with RevPro API and get auth token
        //Reference: https://www.zuora.com/developer/api-references/revenue/operation/POST_Authenticate/
        public async Task<string> GetAuthToken()
        {
            using var client = new HttpClient();
            string revprotoken = "";

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_revproAuthUri),
                Headers =
                {
                    { "clientname", _revproApiClientName },
                    { "role", _revproApiRole },
                    // Basic auth
                    { "Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_revproUsr}:{_revproPwd}"))}" }
                }
            };

            var response = await client.SendAsync(request);


            if (response.IsSuccessStatusCode)
            {
                // Print key
                Console.WriteLine($"Success response from Revpro API Auth. Response headers - ");
                WriteLog(Path.Combine(_logDir, _logFile), $"Success response from Revpro API Auth. Response headers - ");

                foreach (var header in response.Headers)
                {
                    string key = header.Key;
                    IEnumerable<string> values = header.Value;

                    // Print key:value pairs
                    foreach (string value in values)
                    {
                        Console.WriteLine($"{key}:{value}");
                        WriteLog(Path.Combine(_logDir, _logFile), $"{key}:{value}");
                    }

                    if (key == _revproTokenName)
                    {
                        revprotoken = values.First();
                    }

                }

                return revprotoken;

            }

            else
            {
                string failMessage = $"Failed: StatusCode: {response.StatusCode}, Reason: {response.ReasonPhrase}";
                string possibleSolution = $"If fail reason is ambiguous, possible solution - Api URI's in the settings file are missing or misspelled.";
                Console.WriteLine(failMessage);
                Console.WriteLine(possibleSolution);
                WriteLog(Path.Combine(_logDir, _logFile), failMessage);
                WriteLog(Path.Combine(_logDir, _logFile), possibleSolution);
            }

            return "";
        }

        public static void WriteLog(string logFilePath, string message)
        {
            try
            {
                string logMessage = $"{DateTime.Now} - {message}{Environment.NewLine}";
                File.AppendAllText(logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        //split large csv files into smaller ones
        public static void SplitCsv(string csvfile, int splitcount, bool deleteaftersplit)
        {
            Console.WriteLine($"Splitting csv file into {splitcount} smaller files...");
            if (!File.Exists(csvfile))
            {
                Console.WriteLine($"File {csvfile} does not exist.");
                return;
            }

            // Get the header.
            string header;
            using (StreamReader sr = new StreamReader(csvfile))
            {
                header = sr.ReadLine();
            }

            if (string.IsNullOrEmpty(header))
            {
                Console.WriteLine("CSV file is empty or does not have a header.");
                return;
            }

            // Calculate rows per split, avoiding division by zero.
            int totalLines = File.ReadLines(csvfile).Count() - 1; // minus one to exclude header.

            if (totalLines <= 0)
            {
                Console.WriteLine("CSV file has only a header row (no data rows). Nothing to split.");
                return;
            }

            // Don't create more split files than there are data rows
            int effectiveSplitCount = Math.Min(splitcount, totalLines);
            if (effectiveSplitCount < splitcount)
            {
                Console.WriteLine($"File has only {totalLines} data row(s). Creating {effectiveSplitCount} split file(s) instead of {splitcount}.");
            }

            int linesPerSplit = totalLines / effectiveSplitCount + (totalLines % effectiveSplitCount == 0 ? 0 : 1);

            // Handle null from Path.GetDirectoryName (e.g., root-level paths)
            string outputDirectory = Path.GetDirectoryName(csvfile) ?? Directory.GetCurrentDirectory();

            using (StreamReader sr = new StreamReader(csvfile))
            {
                // Skip header
                sr.ReadLine();

                for (int splitNum = 1; splitNum <= effectiveSplitCount; splitNum++)
                {
                    // Define the split file name
                    string splitFile = Path.Combine(
                        outputDirectory,
                        $"{Path.GetFileNameWithoutExtension(csvfile)}_splitPart{splitNum.ToString("D2")}.csv");

                    using (StreamWriter sw = new StreamWriter(splitFile))
                    {
                        // Write header to the split file
                        sw.WriteLine(header);

                        for (int lineNum = 0; lineNum < linesPerSplit; lineNum++)
                        {
                            if (sr.Peek() < 0) // Check if end of file is reached.
                                break;

                            sw.WriteLine(sr.ReadLine());
                        }
                    }
                    Console.WriteLine($"Created file {splitNum} of {effectiveSplitCount} - {Path.GetFileNameWithoutExtension(csvfile)}_splitPart{splitNum.ToString("D2")}.csv");
                }
            }

            if (deleteaftersplit)
            {
                Console.WriteLine($"Deleting original csv file.");
                File.Delete(csvfile);
            }
        }

        //attempt to repair broken encoding in csv file
        //Uses a streaming single-pass approach: reads bytes, detects invalid UTF-8 sequences,
        //skips them, and writes everything else to the output file.
        public static void RepairCsvFile(string csvfile)
        {
            Console.WriteLine($"Checking {Path.GetFileName(csvfile)} for UTF-8 encoding errors...");

            string repairedFile = csvfile.Replace(".csv", "_repaired.csv");
            int invalidByteCount = 0;

            using (FileStream fsRead = new FileStream(csvfile, FileMode.Open, FileAccess.Read))
            using (FileStream fsWrite = new FileStream(repairedFile, FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[4096];
                int bytesRead;

                // We may have a partial multi-byte sequence at the end of a buffer.
                // Track leftover bytes from the previous read.
                byte[] leftover = new byte[4];
                int leftoverCount = 0;

                while ((bytesRead = fsRead.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // Combine leftover bytes from previous iteration with current buffer
                    byte[] work;
                    int workLen;
                    if (leftoverCount > 0)
                    {
                        workLen = leftoverCount + bytesRead;
                        work = new byte[workLen];
                        Array.Copy(leftover, 0, work, 0, leftoverCount);
                        Array.Copy(buffer, 0, work, leftoverCount, bytesRead);
                        leftoverCount = 0;
                    }
                    else
                    {
                        work = buffer;
                        workLen = bytesRead;
                    }

                    int i = 0;
                    while (i < workLen)
                    {
                        byte b = work[i];

                        // Determine expected sequence length from the lead byte
                        int seqLen;
                        if (b <= 0x7F)
                        {
                            // 0xxxxxxx — ASCII (1 byte)
                            seqLen = 1;
                        }
                        else if (b >= 0xC2 && b <= 0xDF)
                        {
                            // 110xxxxx — 2-byte sequence (C0/C1 are overlong, invalid)
                            seqLen = 2;
                        }
                        else if (b >= 0xE0 && b <= 0xEF)
                        {
                            // 1110xxxx — 3-byte sequence
                            seqLen = 3;
                        }
                        else if (b >= 0xF0 && b <= 0xF4)
                        {
                            // 11110xxx — 4-byte sequence (F5+ are invalid)
                            seqLen = 4;
                        }
                        else
                        {
                            // Invalid lead byte (bare continuation byte 0x80-0xBF, or 0xC0-0xC1, or 0xF5+)
                            invalidByteCount++;
                            i++;
                            continue;
                        }

                        // Check if we have enough bytes remaining in the work buffer
                        if (i + seqLen > workLen)
                        {
                            // Possibly a valid sequence split across buffer boundaries.
                            // Save remaining bytes as leftover for the next iteration.
                            // But only if there IS a next iteration (more data to read).
                            if (bytesRead == buffer.Length)
                            {
                                // More data likely coming — save as leftover
                                leftoverCount = workLen - i;
                                Array.Copy(work, i, leftover, 0, leftoverCount);
                                break;
                            }
                            else
                            {
                                // End of file — this is a truncated sequence, skip the lead byte
                                invalidByteCount++;
                                i++;
                                continue;
                            }
                        }

                        // Validate continuation bytes (must be 10xxxxxx)
                        bool valid = true;
                        for (int j = 1; j < seqLen; j++)
                        {
                            if ((work[i + j] & 0xC0) != 0x80)
                            {
                                valid = false;
                                break;
                            }
                        }

                        // Additional checks for overlong encodings and invalid code points
                        if (valid && seqLen == 3)
                        {
                            // E0 requires second byte >= A0 (prevents overlong 2-byte)
                            if (b == 0xE0 && work[i + 1] < 0xA0) valid = false;
                            // ED requires second byte <= 9F (prevents surrogates U+D800-U+DFFF)
                            if (b == 0xED && work[i + 1] > 0x9F) valid = false;
                        }
                        else if (valid && seqLen == 4)
                        {
                            // F0 requires second byte >= 90 (prevents overlong 3-byte)
                            if (b == 0xF0 && work[i + 1] < 0x90) valid = false;
                            // F4 requires second byte <= 8F (prevents code points > U+10FFFF)
                            if (b == 0xF4 && work[i + 1] > 0x8F) valid = false;
                        }

                        if (valid)
                        {
                            // Write valid sequence to output
                            fsWrite.Write(work, i, seqLen);
                            i += seqLen;
                        }
                        else
                        {
                            // Invalid sequence — skip the lead byte only.
                            // Continuation bytes will be evaluated on their own in subsequent iterations.
                            invalidByteCount++;
                            i++;
                        }
                    }
                }

                // Flush any remaining leftover bytes that didn't form a complete sequence
                if (leftoverCount > 0)
                {
                    // These are trailing bytes at end-of-file that couldn't complete a sequence
                    invalidByteCount += leftoverCount;
                    leftoverCount = 0;
                }
            }

            if (invalidByteCount > 0)
            {
                Console.WriteLine($"Found and removed {invalidByteCount} invalid bytes.");
                Console.WriteLine($"Created repaired file {repairedFile}");
            }
            else
            {
                Console.WriteLine($"No encoding errors found. Repaired file written to {repairedFile}");
            }
        }

    }

}



