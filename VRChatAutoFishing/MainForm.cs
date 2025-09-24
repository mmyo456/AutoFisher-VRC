using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Timers;
using System.Windows.Forms;

namespace VRChatAutoFishing
{
    public partial class MainForm : Form
    {
        private bool _isRunning = false;
        private bool _isProtected = false;
        private string _currentAction = "�ȴ�";
        private DateTime _lastCycleEnd;
        private DateTime _lastCastTime;
        private System.Timers.Timer _timeoutTimer;
        private System.Timers.Timer _statusDisplayTimer;
        private System.Timers.Timer _reelBackTimer;
        private OSCClient _oscClient;
        private VRChatLogMonitor _logMonitor;
        private bool _firstCast = true;
        private Thread _fishingThread;
        private bool _isReeling = false;
        private bool _isClosing = false;
        private ManualResetEvent _stopEvent = new ManualResetEvent(false);

        // �̰߳�ȫ�Ĳ����洢
        private double _castTime = 1.7;
        private const double TIMEOUT_MINUTES = 3.0;

        // ����ͳ����ر���
        private int _fishCount = 0;
        private bool _showingFishCount = false;
        private DateTime _lastStatusSwitchTime = DateTime.Now;

        // �ո�״̬����
        private int _savedDataCount = 0;
        private DateTime _firstSavedDataTime;

        // �����׸���ر���
        private double _actualCastTime = 0;
        private double _reelBackTime = 0;

        public MainForm()
        {
            InitializeComponent();
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            _oscClient = new OSCClient("127.0.0.1", 9000);
            _logMonitor = new VRChatLogMonitor();
            _logMonitor.OnDataSaved += FishOnHook;
            _logMonitor.OnFishPickup += OnFishPickupDetected;

            _timeoutTimer = new System.Timers.Timer();
            _timeoutTimer.Elapsed += HandleTimeout;
            _timeoutTimer.AutoReset = false;

            _statusDisplayTimer = new System.Timers.Timer();
            _statusDisplayTimer.Interval = 100;
            _statusDisplayTimer.Elapsed += UpdateStatusDisplay;
            _statusDisplayTimer.AutoReset = true;

            _reelBackTimer = new System.Timers.Timer();
            _reelBackTimer.AutoReset = false;
            _reelBackTimer.Elapsed += PerformReelBack;

            _lastCycleEnd = DateTime.Now;
            _lastCastTime = DateTime.MinValue;

            trackBarCastTime.Minimum = 0;
            trackBarCastTime.Maximum = 17;
            trackBarCastTime.Value = 17;

            UpdateCastTimeLabel();
            UpdateParameters();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            _logMonitor.StartMonitoring();
            _statusDisplayTimer.Start();
            SendClick(false);
        }

        private void btnToggle_Click(object sender, EventArgs e)
        {
            if (_isClosing) return;

            _isRunning = !_isRunning;
            btnToggle.Text = _isRunning ? "ֹͣ" : "��ʼ";

            if (_isRunning)
            {
                _fishCount = 0;
                UpdateParameters();

                _firstCast = true;
                _currentAction = "��ʼ�׸�";
                UpdateStatus();

                _stopEvent.Reset();
                _fishingThread = new Thread(PerformFishingLoop);
                _fishingThread.IsBackground = true;
                _fishingThread.Start();
            }
            else
            {
                EmergencyRelease();
            }
        }

        private void UpdateStatusDisplay(object sender, ElapsedEventArgs e)
        {
            if (_isClosing) return;

            if (_isRunning && _currentAction == "�ȴ����Ϲ�")
            {
                double elapsedSeconds = (DateTime.Now - _lastStatusSwitchTime).TotalSeconds;

                if (_showingFishCount)
                {
                    if (elapsedSeconds >= 2.0)
                    {
                        _showingFishCount = false;
                        _lastStatusSwitchTime = DateTime.Now;
                        UpdateStatusText("�ȴ����Ϲ�");
                    }
                }
                else
                {
                    if (elapsedSeconds >= 5.0)
                    {
                        _showingFishCount = true;
                        _lastStatusSwitchTime = DateTime.Now;
                        UpdateStatusText($"�ѵ�����:{_fishCount}");
                    }
                }
            }
        }

        private void UpdateStatusText(string text)
        {
            if (lblStatus.InvokeRequired)
            {
                if (!_isClosing)
                {
                    try
                    {
                        lblStatus.Invoke(new Action<string>(UpdateStatusText), text);
                    }
                    catch (ObjectDisposedException)
                    {
                        // �����쳣
                    }
                }
                return;
            }

            if (!_isClosing)
            {
                lblStatus.Text = $"[{text}]";
            }
        }

        private void PerformFishingLoop()
        {
            try
            {
                while (_isRunning && !_isClosing)
                {
                    PerformCast();

                    if (_isClosing || !_isRunning)
                        break;

                    DateTime waitStart = DateTime.Now;
                    while (_isRunning && !_isClosing &&
                           (DateTime.Now - waitStart).TotalSeconds < TIMEOUT_MINUTES * 60)
                    {
                        if (_stopEvent.WaitOne(100))
                        {
                            return;
                        }

                        if (!_isRunning || _isClosing)
                            break;
                    }
                }
            }
            catch (ThreadAbortException)
            {
                Thread.ResetAbort();
            }
            catch (Exception ex)
            {
                if (!_isClosing)
                {
                    MessageBox.Show($"�����̴߳���: {ex.Message}", "����",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            if (_isClosing) return;
            ShowHelpDialog();
        }

        private void ShowHelpDialog()
        {
            HelpForm helpForm = new HelpForm();
            helpForm.ShowDialog();
        }

        private void EmergencyRelease()
        {
            _isReeling = false;
            SendClick(false);
            _currentAction = "��ֹͣ";
            _showingFishCount = false;
            UpdateStatusText(_currentAction);

            if (_timeoutTimer.Enabled)
                _timeoutTimer.Stop();

            if (_reelBackTimer.Enabled)
                _reelBackTimer.Stop();

            _stopEvent.Set();
        }

        private void UpdateStatus()
        {
            if (_showingFishCount && _currentAction == "�ȴ����Ϲ�")
                return;

            UpdateStatusText(_currentAction);
        }

        private void SendClick(bool press)
        {
            if (!_isClosing)
            {
                _oscClient.SendUseRight(press ? 1 : 0);
            }
        }

        private void UpdateParameters()
        {
            if (InvokeRequired)
            {
                if (!_isClosing)
                {
                    try
                    {
                        Invoke(new Action(UpdateParameters));
                    }
                    catch (ObjectDisposedException)
                    {
                        // �����쳣
                    }
                }
                return;
            }

            _castTime = trackBarCastTime.Value / 10.0;
        }

        private double GetCastTime()
        {
            return _castTime;
        }

        private void UpdateCastTimeLabel()
        {
            if (lblCastValue.InvokeRequired)
            {
                if (!_isClosing)
                {
                    try
                    {
                        lblCastValue.Invoke(new Action(UpdateCastTimeLabel));
                    }
                    catch (ObjectDisposedException)
                    {
                        // �����쳣
                    }
                }
                return;
            }

            if (!_isClosing)
            {
                lblCastValue.Text = $"{GetCastTime():0.0}��";
            }
        }

        private void StartTimeoutTimer()
        {
            if (_timeoutTimer.Enabled)
                _timeoutTimer.Stop();

            _timeoutTimer.Interval = TIMEOUT_MINUTES * 60 * 1000;
            _timeoutTimer.Start();
        }

        private void HandleTimeout(object sender, ElapsedEventArgs e)
        {
            if (_isRunning && !_isClosing && _currentAction == "�ȴ����Ϲ�")
            {
                _currentAction = "��ʱ�ո�";
                _showingFishCount = false;
                UpdateStatusText(_currentAction);
                ForceReel();
            }
        }

        private void ForceReel()
        {
            if (_isProtected || _isClosing) return;

            try
            {
                _isProtected = true;
                PerformReel();
                if (!_isClosing)
                {
                    PerformCast();
                }
            }
            finally
            {
                _isProtected = false;
            }
        }

        private void PerformReel()
        {
            if (_isClosing) return;

            _currentAction = "�ո���";
            _showingFishCount = false;
            UpdateStatusText(_currentAction);
            _isReeling = true;
            SendClick(true);

            // �����ո�״̬
            _savedDataCount = 0;
            _firstSavedDataTime = DateTime.MinValue;

            DateTime startTime = DateTime.Now;
            bool secondSavedDataDetected = false;

            while (_isReeling && !_isClosing && (DateTime.Now - startTime).TotalSeconds < 30)
            {
                string content = _logMonitor.ReadNewContent();
                if (content.Contains("SAVED DATA"))
                {
                    if (_savedDataCount == 0)
                    {
                        _savedDataCount = 1;
                        _firstSavedDataTime = DateTime.Now;
                        Console.WriteLine("��⵽��һ��SAVED DATA");
                    }
                    else if (_savedDataCount == 1)
                    {
                        double interval = (DateTime.Now - _firstSavedDataTime).TotalSeconds;
                        if (interval >= 1.0)
                        {
                            _savedDataCount = 2;
                            secondSavedDataDetected = true;
                            Console.WriteLine($"��⵽�ڶ���SAVED DATA�����: {interval:F1}��");
                            break;
                        }
                    }
                }

                if (_savedDataCount == 1 && (DateTime.Now - _firstSavedDataTime).TotalSeconds > 10)
                {
                    Console.WriteLine("��һ��SAVED DATA��10����δ��⵽�ڶ��Σ���Ϊ��ʱ");
                    break;
                }

                if (_stopEvent.WaitOne(100))
                {
                    break;
                }
            }

            _isReeling = false;
            SendClick(false);

            if (!_isClosing)
            {
                if (secondSavedDataDetected)
                {
                    _currentAction = "�ո����";
                    _fishCount++;
                }
                else if (_savedDataCount == 1)
                {
                    _currentAction = "�ո˳�ʱ(����)";
                }
                else
                {
                    _currentAction = "�ո˳�ʱ";
                }
                _showingFishCount = false;
                UpdateStatusText(_currentAction);
            }
        }

        private void PerformReelBack(object sender, ElapsedEventArgs e)
        {
            if (_isClosing || !_isRunning) return;

            try
            {
                Console.WriteLine($"ִ�л�������������ʱ��: {_reelBackTime}��");

                SendClick(true);

                DateTime reelStart = DateTime.Now;
                while (!_isClosing && _isRunning &&
                       (DateTime.Now - reelStart).TotalSeconds < _reelBackTime)
                {
                    if (_stopEvent.WaitOne(100))
                    {
                        SendClick(false);
                        return;
                    }
                }

                SendClick(false);

                Console.WriteLine("�����������");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"������������: {ex.Message}");
            }
        }

        private void OnFishPickupDetected()
        {
            if (_isRunning && !_isProtected && !_isClosing && (DateTime.Now - _lastCycleEnd).TotalSeconds >= 2)
            {
                FishOnHook();
            }
        }

        private void PerformCast()
        {
            if (_isClosing) return;

            if (!_firstCast)
            {
                _currentAction = "׼����";
                _showingFishCount = false;
                UpdateStatusText(_currentAction);

                if (_stopEvent.WaitOne(500))
                {
                    return;
                }
            }
            else
            {
                _firstCast = false;
            }

            _currentAction = "���������";
            _showingFishCount = false;
            UpdateStatusText(_currentAction);

            double castDuration = GetCastTime();

            if (castDuration < 0.2)
            {
                _actualCastTime = 0.2;

                if (castDuration < 0.1)
                {
                    _reelBackTime = 0.5;
                }
                else
                {
                    _reelBackTime = 0.3;
                }

                Console.WriteLine($"�����׸�: ��{_actualCastTime}��, {_reelBackTime}������{_reelBackTime}��");

                SendClick(true);

                DateTime castStart = DateTime.Now;
                while (!_isClosing && (DateTime.Now - castStart).TotalSeconds < _actualCastTime)
                {
                    if (_stopEvent.WaitOne(100))
                    {
                        SendClick(false);
                        return;
                    }
                }

                SendClick(false);

                if (!_isClosing)
                {
                    _currentAction = "�ȴ����Ϲ�";
                    _showingFishCount = false;
                    _lastStatusSwitchTime = DateTime.Now;
                    _lastCastTime = DateTime.Now;
                    UpdateStatusText(_currentAction);

                    _reelBackTimer.Interval = 1000;
                    _reelBackTimer.Start();

                    StartTimeoutTimerAfterReelBack();
                }
            }
            else
            {
                SendClick(true);

                DateTime castStart = DateTime.Now;
                while (!_isClosing && (DateTime.Now - castStart).TotalSeconds < castDuration)
                {
                    if (_stopEvent.WaitOne(100))
                    {
                        SendClick(false);
                        return;
                    }
                }

                SendClick(false);

                if (!_isClosing)
                {
                    _currentAction = "�ȴ����Ϲ�";
                    _showingFishCount = false;
                    _lastStatusSwitchTime = DateTime.Now;
                    _lastCastTime = DateTime.Now;
                    UpdateStatusText(_currentAction);
                    StartTimeoutTimer();
                }
            }
        }

        private void StartTimeoutTimerAfterReelBack()
        {
            double totalWaitTime = 1000 + (_reelBackTime * 1000) + 100;

            System.Timers.Timer delayTimer = new System.Timers.Timer(totalWaitTime);
            delayTimer.AutoReset = false;
            delayTimer.Elapsed += (s, e) => {
                if (!_isClosing && _isRunning)
                {
                    StartTimeoutTimer();
                }
                delayTimer.Dispose();
            };
            delayTimer.Start();
        }

        private void FishOnHook()
        {
            if ((DateTime.Now - _lastCastTime).TotalSeconds < 3.0)
            {
                Console.WriteLine("�����׸ͺ�3���ڵ�SAVED DATA�¼�");
                return;
            }

            if (!_isRunning || _isProtected || _isClosing || (DateTime.Now - _lastCycleEnd).TotalSeconds < 2)
                return;

            try
            {
                _isProtected = true;
                _lastCycleEnd = DateTime.Now;
                PerformReel();
                if (!_isClosing)
                {
                    PerformCast();
                }
            }
            finally
            {
                _isProtected = false;
                _lastCycleEnd = DateTime.Now;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _isClosing = true;

            _statusDisplayTimer?.Stop();
            _statusDisplayTimer?.Dispose();

            _reelBackTimer?.Stop();
            _reelBackTimer?.Dispose();

            EmergencyRelease();

            _timeoutTimer?.Stop();
            _timeoutTimer?.Dispose();

            _logMonitor?.StopMonitoring();

            if (_fishingThread != null && _fishingThread.IsAlive)
            {
                _stopEvent.Set();

                if (!_fishingThread.Join(2000))
                {
                    try
                    {
                        _fishingThread.Abort();
                    }
                    catch
                    {
                        // ������ֹ�쳣
                    }
                }
            }

            _oscClient?.Dispose();
            _stopEvent?.Close();
        }

        private void trackBarCastTime_Scroll(object sender, EventArgs e)
        {
            if (_isClosing) return;
            UpdateParameters();
            UpdateCastTimeLabel();
        }

        // Windows Form Designer generated code
        private TrackBar trackBarCastTime;
        private Label lblCastValue;
        private Button btnToggle;
        private Button btnHelp;
        private Label lblStatus;
        private Label label1;

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            trackBarCastTime = new TrackBar();
            lblCastValue = new Label();
            btnToggle = new Button();
            btnHelp = new Button();
            lblStatus = new Label();
            label1 = new Label();
            ((System.ComponentModel.ISupportInitialize)trackBarCastTime).BeginInit();
            SuspendLayout();
            // 
            // trackBarCastTime
            // 
            trackBarCastTime.Location = new Point(80, 12);
            trackBarCastTime.Maximum = 17;
            trackBarCastTime.Minimum = 0;
            trackBarCastTime.Name = "trackBarCastTime";
            trackBarCastTime.Size = new Size(130, 45);
            trackBarCastTime.TabIndex = 1;
            trackBarCastTime.Value = 17;
            trackBarCastTime.Scroll += trackBarCastTime_Scroll;
            // 
            // lblCastValue
            // 
            lblCastValue.Location = new Point(215, 15);
            lblCastValue.Name = "lblCastValue";
            lblCastValue.Size = new Size(40, 20);
            lblCastValue.TabIndex = 2;
            lblCastValue.Text = "1.7��";
            // 
            // btnToggle
            // 
            btnToggle.Location = new Point(95, 58);
            btnToggle.Name = "btnToggle";
            btnToggle.Size = new Size(70, 30);
            btnToggle.TabIndex = 4;
            btnToggle.Text = "��ʼ";
            btnToggle.Click += btnToggle_Click;
            // 
            // btnHelp
            // 
            btnHelp.Location = new Point(15, 58);
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new Size(70, 30);
            btnHelp.TabIndex = 3;
            btnHelp.Text = "˵��";
            btnHelp.Click += btnHelp_Click;
            // 
            // lblStatus
            // 
            lblStatus.Location = new Point(170, 65);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(80, 20);
            lblStatus.TabIndex = 5;
            lblStatus.Text = "[״̬]";
            // 
            // label1
            // 
            label1.Location = new Point(15, 15);
            label1.Name = "label1";
            label1.Size = new Size(60, 20);
            label1.TabIndex = 0;
            label1.Text = "����ʱ��:";
            // 
            // MainForm
            // 
            BackgroundImageLayout = ImageLayout.None;
            ClientSize = new Size(260, 100);
            Controls.Add(label1);
            Controls.Add(trackBarCastTime);
            Controls.Add(lblCastValue);
            Controls.Add(btnHelp);
            Controls.Add(btnToggle);
            Controls.Add(lblStatus);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "�Զ�����v1.5.0";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            ((System.ComponentModel.ISupportInitialize)trackBarCastTime).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}