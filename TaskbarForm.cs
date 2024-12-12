using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Linq;
using Timer = System.Windows.Forms.Timer;

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

        public TaskbarForm()
        {
            InitializeComponent();
            SetupTimer();
            RefreshApplications(null, null);
            this.MouseDown += TaskbarForm_MouseDown;
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
            flowPanel.AutoScroll = true;
            flowPanel.WrapContents = false;
            flowPanel.FlowDirection = FlowDirection.LeftToRight;
            flowPanel.BackColor = Color.FromArgb(20, 20, 20);
            flowPanel.MouseDown += TaskbarForm_MouseDown;
            flowPanel.Cursor = Cursors.SizeAll;
            
            this.Controls.Add(flowPanel);

            // Add signature label
            Label signatureLabel = new Label
            {
                Text = "Creator: Idris Jimoh",
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
            // Get current applications
            Process[] processes = Process.GetProcesses();
            var currentHandles = flowPanel.Controls.Cast<PictureBox>()
                .Select(pb => (IntPtr)pb.Tag)
                .ToList();

            // Find new windows to add
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

            // Remove closed windows
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
    }
}
