using System;
using System.Collections.Generic;
using System.Text;
using WinSCP;
using System.Configuration;
using System.IO;

namespace DataFeed
{
    class WinSCPClient
    {
        IList<string> SuccessFiles = null;

        System.Text.StringBuilder build = new StringBuilder();

        int tryCount =0;

        //是否还有剩余文件
        bool isRemnant = false;

        public WinSCPClient()
        {
            SuccessFiles = new List<string>();

            build.AppendLine("Starting at"+System.DateTime.Now.DayOfWeek.ToString().Substring(0,3)+" "+System.DateTime.Now.ToString("MM/dd/yyyy,hh:mm:ss.ff tt")); // 05/13/2015,03:30:06.44 PM

            InitilzationConfig();
            
            isConnected = false;
        }

        #region

        private Session sessionFTP;
        public Session SessionFTP
        {
            get { return sessionFTP; }
            set { sessionFTP = value; }
        }

        private string sftpServerIP;
        public string SFTPServerIP
        {
            get
            {
                return sftpServerIP;
            }
        }

        private string sFtpUserID;
        public string SFtpUserID
        {
            get
            {
               return sFtpUserID;
            }
        }

        private string sFtpPassword;
        public string SFtpPassword
        {
            get
            {
                return sFtpPassword;
            }
        }

        private string  sFtpSeverPort;
        public string SFtpSeverPort
        {
            get
            {
                return sFtpSeverPort;
            }
            
        }

        private string sFTPHostKey;
        public string SFTPHostKey
        {
            get { return sFTPHostKey; }
        }

        private string sftpLocalPath;
        public string SFTPLocalPath
        {
            get
            {
                return sftpLocalPath;
            }
            
        }

        private string sftpRemotePath;
        public string SFTPRemotePath
        {
          get { return sftpRemotePath; }
        }

        private string backUpFolder;
        public string BackUpFolder
        {
            get { return backUpFolder; }
        }

        private string stringLogPath;
        public string StringLogPath
        {
            get { return stringLogPath ; }
        }

        private Boolean isConnected;
        public bool IsConnected
        {
            get
            {
                return isConnected;
            }
        }

        private int intelvalHour;
        public int IntelvalHour
        {
            get { return intelvalHour; }
            set { intelvalHour = value; }
        }

        private int intelvalMinute;
        public int IntelvalMinute
        {
            get { return intelvalMinute; }
            set { intelvalMinute = value; }
        }

        private int intelvalSecond;
        public int IntelvalSecond
        {
            get { return intelvalSecond; }
            set { intelvalSecond = value; }
        }
           

        #endregion

        public void InitilzationConfig()
        {
            try
            {
                sftpServerIP = ConfigurationManager.AppSettings["sftpServerIP"];
                sFtpUserID = ConfigurationManager.AppSettings["sftpUserID"];
                sFtpPassword = ConfigurationManager.AppSettings["sftpPassword"];
                sFtpSeverPort = ConfigurationManager.AppSettings["sftpPort"];
                sFTPHostKey = ConfigurationManager.AppSettings["sftpHostKey"];
                sftpRemotePath = ConfigurationManager.AppSettings["sToPath"];
                sftpLocalPath = ConfigurationManager.AppSettings["sFromPath"];
                stringLogPath = ConfigurationManager.AppSettings["sftpLogPath"];
                backUpFolder = ConfigurationManager.AppSettings["sftpBackup"];


                intelvalHour = Convert.ToInt32(ConfigurationManager.AppSettings["Shour"]);
                intelvalMinute = Convert.ToInt32(ConfigurationManager.AppSettings["Sminute"]);
                intelvalSecond = Convert.ToInt32(ConfigurationManager.AppSettings["Ssecod"]);


            }
            catch(ConfigurationException ex)
            {
                build.AppendLine("Load  App.Config  Fail :" + ex.Message );
            }
        }

        /// <summary>
        /// SFTP协议上传文件
        /// </summary>
        public void SFTPUploadFiles() {

            CheckLocalRemainFiles();

            if (isRemnant)
            {

                try
                {
                    if (!IsConnected)
                        Connect();

                    TransferOptions transferOptionsSFTP = new TransferOptions();
                    transferOptionsSFTP.TransferMode = TransferMode.Binary;
                    transferOptionsSFTP.FilePermissions = null;
                    transferOptionsSFTP.PreserveTimestamp = false;
                    transferOptionsSFTP.ResumeSupport.State = TransferResumeSupportState.Off;

                    //当文件传输时发生
                    SessionFTP.FileTransferred += FileTransCatch;

                    //同步开始传输文件
                    TransferOperationResult remoteResult = SessionFTP.PutFiles(SFTPLocalPath, SFTPRemotePath, false, transferOptionsSFTP);

                    if (remoteResult.IsSuccess && SuccessFiles.Count > 0)
                    {
                        BackupOnlySuccessFiles();
                        ExecuteToUploadAgain();
                    }
                    else { 
                        ExecuteToUploadAgain();
                    }

                    //throw exception if any error.
                    remoteResult.Check();

                    //disconnect if not any error.
                    Disconnect();

                }
                catch (Exception ex)
                {
                    //如果有任何传输问题导致传输失败，则每5分钟后重新上传文件，直到上传完成
                    System.Threading.Thread.Sleep(new TimeSpan(IntelvalHour,IntelvalMinute,IntelvalSecond));

                    if (tryCount <= 5)
                    {
                        tryCount++;
                        build.AppendLine("File transferring occurs problem:" + ex.Message + ",System is trying to reupload" + tryCount + "...");
                        SFTPUploadFiles();
                    }

                }

                foreach (string strlog in sessionFTP.Output)
                {
                    build.AppendLine(strlog);
                }

                build.AppendLine("End at " + System.DateTime.Now.DayOfWeek.ToString().Substring(0, 3) + " " + System.DateTime.Now.ToString("MM/dd/yyyy,hh:mm:ss.ff tt"));

                WriteLog(build.ToString(), StringLogPath);

                Environment.Exit(0);
               
            }

        }

        //尽最大努力上传完所有文件
        private void ExecuteToUploadAgain() {

            CheckLocalRemainFiles();

            if (isRemnant)
            {
                if (tryCount <= 5)
                {
                    tryCount++;
                    SFTPUploadFiles();
                }
            }
        }

        /// <summary>
        /// 只备份成功上传的文件
        /// </summary>
        private void BackupOnlySuccessFiles()
        {
            try
            {
                if (false == Directory.Exists(BackUpFolder))
                {
                    try
                    {
                        DirectoryInfo direInfo = new DirectoryInfo(BackUpFolder);
                        direInfo.Create();
                    }
                    catch(DirectoryNotFoundException ex)
                    {
                        throw ex;
                    }

                }

                foreach (string fileName in SuccessFiles)
                {
                    FileInfo fi = new FileInfo(fileName);

                    File.Copy(fileName, BackUpFolder + "\\" + fi.Name, true);

                    File.Delete(fileName);

                    //if (File.Exists(fileName))
                    //{
                    //    File.Move(fileName, BackUpFolder + "\\" + fi.Name);
                    //}

                    fi = null;
                }
            }
            catch(Exception ex)
            {
                build.AppendLine("Backup fail:" + ex.Message);
            }
        }

        /// <summary>
        /// 打开会话链接
        /// </summary>
        private void Connect()
        {
            try
            {
                //配置参数
                SessionOptions sessionOptionsSFTP = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = SFTPServerIP,
                    UserName = SFtpUserID,
                    Password = SFtpPassword,
                    PortNumber = int.Parse(SFtpSeverPort),
                    SshHostKeyFingerprint = SFTPHostKey
                };

                //sessionFTP.SessionLogPath

                sessionFTP = new Session();
                sessionFTP.Open(sessionOptionsSFTP);

                isConnected = true;
            }
            catch (Exception ex)
            {
                isConnected = false;
                build.AppendLine("Could not connect to remote server:"+ex.Message);
            }
        }

        /// <summary>
        /// 断开会话连接
        /// </summary>
        public void Disconnect() {

            if (IsConnected)
            {
                if (sessionFTP.Opened)
                {
                    sessionFTP.Dispose();

                    isConnected = false;
                }
            }
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="filecontent"></param>
        /// <param name="currentpath"></param>
        private void WriteLog(string filecontent, string currentpath)
        {
            string stringStrDate = System.DateTime.Now.ToString("yyyyMMdd");
            string stringLogFileName = "send" + stringStrDate + ".log";

            if (false == Directory.Exists(currentpath))
            {
                try
                {
                    DirectoryInfo direInfo = new DirectoryInfo(currentpath);
                    direInfo.Create();
                }
                catch
                { }

            }

            string finalPath = Path.Combine(currentpath, stringLogFileName);

            if (false == File.Exists(finalPath))
            {
                try
                {
                    using (File.Create(finalPath)) { };
                }
                catch
                { }

            }

            byte[] byteadd = Encoding.UTF8.GetBytes(filecontent);

            FileStream fs = null;
            try
            {

                using (fs = File.Open(finalPath, FileMode.Append, FileAccess.Write))
                {
                    if (fs.CanWrite)
                    {
                        fs.Write(byteadd, 0, filecontent.Length);
                    }
                }

                if (fs != null)
                {
                    fs.Close();
                    fs.Dispose();
                }

            }
            catch
            {
                fs.Close();
                fs.Dispose();
            }
            finally
            {
                if (fs != null)
                {
                    fs.Close();
                    fs.Dispose();
                }
            }
        }

        /// <summary>
        /// 捕获文件传输过程中发生的事
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileTransCatch(object sender, TransferEventArgs e)
        {
            if (e.Error == null)
            {

                build.AppendLine("Upload of "+e.FileName+" succeeded");
                SuccessFiles.Add(e.FileName);
            }
            else
            {
                build.AppendLine("Upload of " + e.FileName + " failed:" + e.Error);
            }

            if (e.Chmod != null)
            {
                if (e.Chmod.Error == null)
                {
                    build.AppendLine("Permisions of "+e.Chmod.FileName+" set to "+e.Chmod.FilePermissions);
                }
                else
                {
                    build.AppendLine("Setting permissions of "+e.Chmod.FileName +" failed: "+ e.Chmod.Error);
                }
            }
            else
            {
                 build.AppendLine("Permissions of "+e.Destination+" kept with their defaults");
            }

            if (e.Touch != null)
            {
                if (e.Touch.Error == null)
                {
                    build.AppendLine("Timestamp of "+e.Touch.FileName+" set to "+e.Touch.LastWriteTime);
                }
                else
                {
                    build.AppendLine("Setting timestamp of "+e.Touch.FileName+" failed: "+ e.Touch.Error);
                }
            }
            else
            {
                // This should never happen during "local to remote" synchronization
                build.AppendLine("Timestamp of " + e.Destination + " kept with its default (current time)");
            }

        }

        /// <summary>
        /// 检查本地目录是否还有未上传完的文件
        /// </summary>
        /// <returns></returns>
        private void CheckLocalRemainFiles()
        {
            try
            {
                string[] PrepareUploadFiles = Directory.GetFiles(SFTPLocalPath);
                if (PrepareUploadFiles.Length > 0)
                {
                    isRemnant = true;
                }
            }
            catch
            {
                isRemnant=false;
            }
        }

    }
}
