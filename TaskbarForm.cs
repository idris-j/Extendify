using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Linq;
using Timer = System.Windows.Forms.Timer;
using System.IO;
using System.Threading;

namespace Extendify
{
    public class TaskbarForm : Form
    {
        private FlowLayoutPanel flowPanel;
        private Timer refreshTimer;

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private Button recordButton;
        private Button pauseButton;
        private Button stopButton;
        private Button muteButton;
        private Label timerLabel;
        private Timer recordingTimer;
        private bool isRecording = false;
        private bool isPaused = false;
        private bool isMuted = false;
        private TimeSpan recordingDuration = TimeSpan.Zero;
        private ScreenRecorder screenRecorder;
        private ContextMenuStrip contextMenu;
        private bool isMicMuted = false;
        private Button micMuteButton;

        public TaskbarForm()
        {
            InitializeComponent();
            InitializeRecordingControls();
            SetupTimer();
            RefreshApplications(null, null);
            this.MouseDown += TaskbarForm_MouseDown;
            InitializeContextMenu();
        }

        private void TaskbarForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Opacity = 0.85;
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.Padding = new Padding(4);

            Screen screen = Screen.PrimaryScreen;
            this.Width = 600;
            this.Height = 45;
            this.Location = new Point(
                (screen.WorkingArea.Width - this.Width) / 2,
                screen.WorkingArea.Bottom - this.Height
            );

            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 10, 10));

            flowPanel = new FlowLayoutPanel();
            flowPanel.Dock = DockStyle.Fill;
            flowPanel.AutoScroll = false;
            flowPanel.WrapContents = false;
            flowPanel.FlowDirection = FlowDirection.LeftToRight;
            flowPanel.BackColor = Color.FromArgb(20, 20, 20);
            flowPanel.MouseDown += TaskbarForm_MouseDown;
            flowPanel.Cursor = Cursors.SizeAll;

            contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Exit", null, (s, e) => Application.Exit());
            flowPanel.ContextMenuStrip = contextMenu;
            
            this.Controls.Add(flowPanel);

            Label signatureLabel = new Label
            {
                Text = "Idris",
                Font = new Font("Segoe Script", 8, FontStyle.Italic),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            signatureLabel.Location = new Point(this.Width - signatureLabel.PreferredWidth - 10, 5);
            signatureLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.Controls.Add(signatureLabel);
            signatureLabel.BringToFront();
        }

        private void SetupTimer()
        {
            refreshTimer = new Timer();
            refreshTimer.Interval = 3000;
            refreshTimer.Tick += RefreshApplications;
            refreshTimer.Start();
        }

        private void RefreshApplications(object sender, EventArgs e)
        {
            Process[] processes = Process.GetProcesses();
            var currentHandles = flowPanel.Controls.Cast<PictureBox>()
                .Select(pb => (IntPtr)pb.Tag)
                .ToList();

            foreach (Process p in processes)
            {
                if (!string.IsNullOrEmpty(p.MainWindowTitle) && 
                    p.MainWindowHandle != IntPtr.Zero &&
                    !IsSystemProcess(p) &&
                    !currentHandles.Contains(p.MainWindowHandle))
                {
                    try
                    {
                        var handle = p.MainWindowHandle;
                        Icon appIcon = null;
                        
                        try {
                            appIcon = Icon.ExtractAssociatedIcon(p.MainModule.FileName);
                        }
                        catch {
                            appIcon = SystemIcons.Application;
                        }
                        
                        PictureBox iconBox = CreateIconBox(appIcon, p.MainWindowTitle, handle);
                        flowPanel.Controls.Add(iconBox);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            var activeHandles = processes
                .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && 
                           p.MainWindowHandle != IntPtr.Zero && 
                           !IsSystemProcess(p))
                .Select(p => p.MainWindowHandle)
                .ToList();

            var controlsToRemove = flowPanel.Controls.Cast<PictureBox>()
                .Where(pb => !activeHandles.Contains((IntPtr)pb.Tag))
                .ToList();

            foreach (var control in controlsToRemove)
            {
                flowPanel.Controls.Remove(control);
                control.Dispose();
            }
        }

        private bool IsSystemProcess(Process process)
        {
            string[] systemProcesses = new string[] 
            {
                "ApplicationFrameHost",
                "WindowsInternal",
                "ShellExperienceHost",
                "SearchHost",
                "TextInputHost",
                "StartMenuExperienceHost",
                "SystemSettings",
                "WidgetService",
                "WindowsShellExperience",
                "LockApp"
            };

            try
            {
                if (process.ProcessName.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase))
                    return false;

                return systemProcesses.Any(sp => 
                    process.ProcessName.Contains(sp, StringComparison.OrdinalIgnoreCase) ||
                    process.MainWindowTitle.Contains(sp, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private PictureBox CreateIconBox(Icon appIcon, string title, IntPtr handle)
        {
            PictureBox iconBox = new PictureBox
            {
                Image = appIcon.ToBitmap(),
                Width = 32,
                Height = 32,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Margin = new Padding(5),
                Cursor = Cursors.Hand,
                Tag = handle
            };

            ToolTip tooltip = new ToolTip();
            tooltip.SetToolTip(iconBox, title);

            iconBox.Click += (s, ev) => ActivateWindow(handle);

            return iconBox;
        }

        private void ActivateWindow(IntPtr handle)
        {
            if (NativeMethods.IsIconic(handle))
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
            }
            NativeMethods.BringWindowToTop(handle);
            NativeMethods.SetForegroundWindow(handle);
            
            if (!NativeMethods.IsWindowVisible(handle))
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SW_SHOW);
            }
        }

        private void InitializeRecordingControls()
        {
            Panel recordingPanel = new Panel
            {
                Height = 35,
                Width = 250,
                BackColor = Color.FromArgb(30, 30, 30),
                Dock = DockStyle.Right
            };

            FlowLayoutPanel buttonFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(5),
                BackColor = Color.FromArgb(30, 30, 30)
            };

            void StyleButton(Button btn)
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.BackColor = Color.FromArgb(30, 30, 30);
                btn.Size = new Size(32, 32);
                btn.Font = new Font("Segoe UI", 12);
                btn.Cursor = Cursors.Hand;

                btn.MouseEnter += (s, e) => {
                    btn.BackColor = Color.FromArgb(45, 45, 45);
                };
                btn.MouseLeave += (s, e) => {
                    btn.BackColor = Color.FromArgb(30, 30, 30);
                };
            }

            recordButton = new Button
            {
                Text = "⏺",
                ForeColor = Color.White
            };
            StyleButton(recordButton);
            recordButton.Click += StartRecording;

            pauseButton = new Button
            {
                Text = "⏸",
                ForeColor = Color.White,
                Enabled = false
            };
            StyleButton(pauseButton);
            pauseButton.Click += PauseRecording;

            stopButton = new Button
            {
                Text = "⏹",
                ForeColor = Color.White,
                Enabled = false
            };
            StyleButton(stopButton);
            stopButton.Click += StopRecording;

            micMuteButton = new Button
            {
                Text = "🎤",
                ForeColor = Color.White
            };
            StyleButton(micMuteButton);
            micMuteButton.Click += ToggleMicrophone;

            muteButton = new Button
            {
                Text = "🔊",
                ForeColor = Color.White
            };
            StyleButton(muteButton);
            muteButton.Click += ToggleMute;

            timerLabel = new Label
            {
                Text = "00:00:00",
                ForeColor = Color.White,
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                Padding = new Padding(5, 8, 5, 0)
            };

            buttonFlow.Controls.AddRange(new Control[] { 
                recordButton, 
                pauseButton, 
                stopButton, 
                timerLabel 
            });

            recordingPanel.Controls.Add(buttonFlow);
            this.Controls.Add(recordingPanel);

            recordingTimer = new Timer
            {
                Interval = 1000
            };
            recordingTimer.Tick += UpdateRecordingTime;

            screenRecorder = new ScreenRecorder();
        }

        private void ToggleMicrophone(object sender, EventArgs e)
        {
            isMicMuted = !isMicMuted;
            micMuteButton.Text = isMicMuted ? "🎤⃠" : "🎤";
            micMuteButton.ForeColor = isMicMuted ? Color.Red : Color.White;
            screenRecorder.ToggleMicrophone(isMicMuted);
        }

        private bool CheckDependencies()
        {
            string[] requiredDlls = new string[] 
            {
                "NAudio.Wasapi.dll",
                "NAudio.WinMM.dll"
            };

            string applicationPath = AppDomain.CurrentDomain.BaseDirectory;
            foreach (string dll in requiredDlls)
            {
                if (!File.Exists(Path.Combine(applicationPath, dll)))
                {
                    MessageBox.Show($"Required dependency {dll} is missing. Please reinstall the application.", 
                                  "Missing Dependencies", 
                                  MessageBoxButtons.OK, 
                                  MessageBoxIcon.Error);
                    return false;
                }
            }
            return true;
        }

        private void StartRecording(object sender, EventArgs e)
        {
            try 
            {
                if (!CheckDependencies())
                {
                    return;
                }

                if (!isRecording)
                {
                    string recordingsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Extensify",
                        "Recordings"
                    );
                    Directory.CreateDirectory(recordingsPath);

                    isRecording = true;
                    recordButton.Enabled = false;
                    pauseButton.Enabled = true;
                    stopButton.Enabled = true;
                    recordingTimer.Start();

                    Screen primaryScreen = Screen.PrimaryScreen;
                    int screenWidth = primaryScreen.Bounds.Width;
                    int screenHeight = primaryScreen.Bounds.Height;
                    screenRecorder.StartRecording(recordingsPath, screenWidth, screenHeight);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start recording: {ex.Message}", 
                              "Recording Error", 
                              MessageBoxButtons.OK, 
                              MessageBoxIcon.Error);
                StopRecording(null, null);
            }
        }

        private void PauseRecording(object sender, EventArgs e)
        {
            isPaused = !isPaused;
            pauseButton.Text = isPaused ? "▶" : "⏸";
            if (isPaused)
            {
                recordingTimer.Stop();
                screenRecorder.PauseRecording();
            }
            else
            {
                recordingTimer.Start();
                screenRecorder.ResumeRecording();
            }
        }

        private void StopRecording(object sender, EventArgs e)
        {
            isRecording = false;
            isPaused = false;
            recordButton.Enabled = true;
            pauseButton.Enabled = false;
            stopButton.Enabled = false;
            recordingTimer.Stop();
            recordingDuration = TimeSpan.Zero;
            timerLabel.Text = "00:00:00";

            screenRecorder.StopRecording();
        }

        private void ToggleMute(object sender, EventArgs e)
        {
            isMuted = !isMuted;
            muteButton.Text = isMuted ? "🔇" : "🔊";
            screenRecorder.ToggleMute();
        }

        private void UpdateRecordingTime(object sender, EventArgs e)
        {
            recordingDuration = recordingDuration.Add(TimeSpan.FromSeconds(1));
            timerLabel.Text = recordingDuration.ToString(@"hh\:mm\:ss");
        }

        private void InitializeContextMenu()
        {
            contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Exit", null, (s, e) => Application.Exit());
        }
    }
}
