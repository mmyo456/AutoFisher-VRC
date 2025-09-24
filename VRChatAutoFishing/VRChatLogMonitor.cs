using System;
using System.IO;
using System.Threading;

namespace VRChatAutoFishing
{
    public class VRChatLogMonitor
    {
        public event Action OnDataSaved;
        public event Action OnFishPickup;

        private FileSystemWatcher _watcher;
        private string _currentLogPath;
        private long _filePosition;
        private readonly object _lockObject = new object();
        private Thread _monitorThread;
        private bool _isMonitoring;
        private bool _isStopping; // 添加停止标志

        public VRChatLogMonitor()
        {
            _filePosition = 0;
            _isMonitoring = false;
            _isStopping = false;
        }

        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            string logDir = GetVRChatLogDirectory();
            if (!Directory.Exists(logDir))
            {
                Console.WriteLine("VRChat日志目录不存在");
                return;
            }

            _watcher = new FileSystemWatcher(logDir, "output_log_*.txt");
            _watcher.Created += OnLogFileCreated;
            _watcher.Changed += OnLogFileChanged;
            _watcher.EnableRaisingEvents = true;

            UpdateLogFile();

            _isMonitoring = true;
            _isStopping = false;
            _monitorThread = new Thread(MonitorLoop);
            _monitorThread.IsBackground = true;
            _monitorThread.Start();
        }

        public void StopMonitoring()
        {
            _isStopping = true;
            _isMonitoring = false;

            _watcher?.Dispose();
            _watcher = null;

            if (_monitorThread != null && _monitorThread.IsAlive)
            {
                if (!_monitorThread.Join(1000)) // 等待1秒线程结束
                {
                    try
                    {
                        _monitorThread.Abort();
                    }
                    catch
                    {
                        // 忽略中止异常
                    }
                }
            }
        }

        private void MonitorLoop()
        {
            while (_isMonitoring && !_isStopping)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (_isStopping) break;

                    if (UpdateLogFile())
                        continue;

                    string content = ReadNewContent();
                    if (!string.IsNullOrEmpty(content) && !_isStopping)
                    {
                        if (content.Contains("SAVED DATA"))
                        {
                            OnDataSaved?.Invoke();
                        }

                        if (content.Contains("Fish Pickup attached to rod Toggles(True)"))
                        {
                            OnFishPickup?.Invoke();
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_isStopping)
                    {
                        Console.WriteLine($"日志监视错误: {ex.Message}");
                    }
                }
            }
        }

        // 其他方法保持不变...
        private string GetVRChatLogDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.GetFullPath(Path.Combine(appData, @"..\LocalLow\VRChat\VRChat"));
        }

        private bool UpdateLogFile()
        {
            string newLog = FindLatestLog();
            if (newLog != _currentLogPath)
            {
                Console.WriteLine($"检测到新日志文件: {newLog}");
                _currentLogPath = newLog;
                _filePosition = 0;
                return true;
            }
            return false;
        }

        private string FindLatestLog()
        {
            string logDir = GetVRChatLogDirectory();
            if (!Directory.Exists(logDir))
                return null;

            var logFiles = Directory.GetFiles(logDir, "output_log_*.txt");
            if (logFiles.Length == 0)
                return null;

            string latestFile = null;
            DateTime latestTime = DateTime.MinValue;

            foreach (string file in logFiles)
            {
                DateTime writeTime = File.GetLastWriteTime(file);
                if (writeTime > latestTime)
                {
                    latestTime = writeTime;
                    latestFile = file;
                }
            }

            return latestFile;
        }

        public string ReadNewContent()
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(_currentLogPath) || !File.Exists(_currentLogPath))
                    return string.Empty;

                try
                {
                    using (var stream = new FileStream(_currentLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (_filePosition > stream.Length)
                            _filePosition = 0;

                        stream.Seek(_filePosition, SeekOrigin.Begin);

                        using (var reader = new StreamReader(stream))
                        {
                            string content = reader.ReadToEnd();
                            _filePosition = stream.Position;
                            return content;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!_isStopping)
                    {
                        Console.WriteLine($"读取日志失败: {ex.Message}");
                    }
                    return string.Empty;
                }
            }
        }

        private void OnLogFileCreated(object sender, FileSystemEventArgs e)
        {
            if (!_isStopping)
            {
                UpdateLogFile();
            }
        }

        private void OnLogFileChanged(object sender, FileSystemEventArgs e)
        {
            // 文件变化处理
        }
    }
}