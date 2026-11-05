using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace RocketLauncher
{
    /// <summary>"Starting Rocket..." window shown - idk if this works or not</summary>
    public sealed class SplashForm : Form
    {
        readonly Label status;
        readonly ProgressBar progress;
        volatile bool closingFromLauncher;

        /// <summary>True once the player closed the window themselves - same as above idk if this works or not</summary>
        public volatile bool UserCancelled;

        public SplashForm()
        {
            Text = "Rocket";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(400, 230);
            BackColor = Color.FromArgb(35, 37, 39);
            Font = new Font("Segoe UI", 10f);
            ShowInTaskbar = true;
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            FormClosing += (s, e) => { if (!closingFromLauncher) UserCancelled = true; };

            Icon appIcon = TryLoadIcon();
            if (appIcon != null) Icon = appIcon;

            var logo = new PictureBox
            {
                Size = new Size(96, 96),
                Location = new Point((ClientSize.Width - 96) / 2, 26),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Image = TryLoadLogo(),
            };

            status = new Label
            {
                Text = "Starting Rocket...",
                ForeColor = Color.White,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width - 40, 28),
                Location = new Point(20, 138),
            };

            progress = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Size = new Size(ClientSize.Width - 80, 14),
                Location = new Point(40, 178),
            };

            Controls.Add(logo);
            Controls.Add(status);
            Controls.Add(progress);
        }

        /// <summary>Thread-safe. text == null keeps the current text; percent &lt; 0 shows the moving bar.</summary>
        public void SetProgress(string text, int percent)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string, int>(SetProgress), text, percent); } catch (InvalidOperationException) { }
                return;
            }
            if (text != null) status.Text = text;
            if (percent < 0)
            {
                progress.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                progress.Style = ProgressBarStyle.Continuous;
                progress.Value = Math.Max(0, Math.Min(100, percent));
            }
        }

        public void CloseFromAnyThread()
        {
            closingFromLauncher = true;
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(Close)); } catch (InvalidOperationException) { }
                return;
            }
            Close();
        }

        static Image TryLoadLogo()
        {
            try
            {
                Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("RocketLauncher.Logo.png");
                return s == null ? null : Image.FromStream(s);
            }
            catch { return null; }
        }

        static Icon TryLoadIcon()
        {
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { return null; }
        }
    }
}
