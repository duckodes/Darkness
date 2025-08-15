using System;
using System.Net.Http;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Net;
using System.Threading;
using System.Security.Policy;

public class DarknessApp : Form
{
    const int PADDING = 10;

    private readonly WebView2 webView;
    private readonly TextBox urlBox;
    private readonly Button backButton;
    private readonly Button forwardButton;
    private readonly Button showWindowButton;

    [DllImport("user32.dll")]
    static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
    const uint WDA_NONE = 0x00000000;
    const uint WDA_MONITOR = 0x00000001;
    const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    public DarknessApp(string url)
    {
        this.Text = "Darkness";
        this.Width = 1000;
        this.Height = 700;
        this.ShowInTaskbar = false;
        this.Resize += (s, e) =>
        {
            Resized(PADDING);
        };
        this.Load += (s, e) =>
        {
            SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE);
#if DEBUG
            InitWebView2DefaultCore(url);
#elif TRACE
            InitWebView2(url);
#endif
        };

        #region ShowWindowButton
        showWindowButton = new Button
        {
            FlatStyle = FlatStyle.Flat,
            Width = 5
        };
        showWindowButton.FlatAppearance.BorderSize = 0;
        showWindowButton.Click += (s, e) =>
        {
            if (this.ShowInTaskbar)
            {
                this.ShowInTaskbar = false;
                SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE);
            }
            else
            {
                this.ShowInTaskbar = true;
                SetWindowDisplayAffinity(this.Handle, WDA_NONE);
            }
        };
        #endregion

        #region BackButton
        backButton = new Button
        {
            Font = new Font("Segoe UI", 12),
            FlatStyle = FlatStyle.Flat,
            Text = "↩",
            AutoSize = true
        };
        backButton.FlatAppearance.BorderSize = 0;
        backButton.Click += (s, e) =>
        {
            if (webView.CoreWebView2.CanGoBack)
                webView.CoreWebView2.GoBack();
        };
        #endregion

        #region forwardButton
        forwardButton = new Button
        {
            Font = new Font("Segoe UI", 12),
            FlatStyle = FlatStyle.Flat,
            Text = "↪",
            AutoSize = true
        };
        forwardButton.FlatAppearance.BorderSize = 0;
        forwardButton.Click += (s, e) =>
        {
            if (webView.CoreWebView2.CanGoForward)
                webView.CoreWebView2.GoForward();
        };
        #endregion

        #region UrlBox
        urlBox = new CenteredTextBox
        {
            Font = new Font("Segoe UI", 12),
            BorderStyle = BorderStyle.None,
            ForeColor = Color.White,
            BackColor = Color.FromArgb((255 << 24) | (30 << 16) | (30 << 8) | 30),
            Multiline = false,
            WordWrap = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        urlBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                Search();
            }
        };
        urlBox.GotFocus += (s, e) =>
        {
            urlBox.SelectAll();
        };

        urlBox.LostFocus += (s, e) =>
        {
            urlBox.SelectionStart = 0;
            urlBox.SelectionLength = 0;
            urlBox.ScrollToCaret();
        };
        #endregion

        FlowLayoutPanel navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            ForeColor = Color.White,
            BackColor = Color.FromArgb((255 << 24) | (50 << 16) | (50 << 8) | 50),
            AutoSize = true
        };

        navigation.Controls.Add(showWindowButton);
        navigation.Controls.Add(backButton);
        navigation.Controls.Add(forwardButton);
        navigation.Controls.Add(urlBox);

        webView = new WebView2
        {
            Dock = DockStyle.Fill
        };

        this.Controls.Add(webView);
        this.Controls.Add(navigation);

        webView.NavigationCompleted += (s, e) =>
        {
            urlBox.Text = $"{webView.Source}";
            this.Text = webView.CoreWebView2.DocumentTitle;

            Resized(PADDING);
            UpdateIcon();
        };
    }
    private async void InitWebView2(string url)
    {
        string pathName = "Microsoft.WebView2.FixedVersionRuntime.139.0.3405.86.x64";
        string cabPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pathName + ".cab");
        string extractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pathName);
        string downloadUrl = $"https://drive.usercontent.google.com/download?id=1TSTJy7RamjL7SpqJuY1BG-VwiiVQjX4D&export=download&authuser=0&confirm=t&uuid=cea07cdf-6138-4624-9328-3264c74e6431&at=AN8xHoqjehMYU2xHwgF5KZLl_lK8:1755242592435";

        Form progressForm = null;
        Label labelStatus = null;
        ProgressBar progressBar = null;

        void ShowProgressForm(string initialText, ProgressBarStyle style)
        {
            progressForm = new Form
            {
                Width = 400,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                Text = "初始化 WebView2",
                ControlBox = false,
                TopMost = true
            };

            labelStatus = new Label
            {
                Text = initialText,
                AutoSize = true,
                Font = new Font("Segoe UI", 12),
                Location = new Point(20, 20)
            };

            progressBar = new ProgressBar
            {
                Style = style,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Width = 340,
                Location = new Point(20, 60),
            };

            progressForm.Controls.Add(labelStatus);
            progressForm.Controls.Add(progressBar);
            progressForm.Show();
            progressForm.Refresh();
        }

        if (!File.Exists(cabPath))
        {
            ShowProgressForm("正在下載 WebView2 Runtime...", ProgressBarStyle.Blocks);

            using (WebClient client = new WebClient())
            {
                client.DownloadProgressChanged += (s, e) =>
                {
                    if (progressForm.IsHandleCreated)
                    {
                        progressForm.Invoke(new Action(() =>
                        {
                            progressBar.Value = e.ProgressPercentage;
                            labelStatus.Text = $"下載中... {e.ProgressPercentage}%";
                        }));
                    }
                };

                client.DownloadFileCompleted += (s, e) =>
                {
                    if (progressForm.IsHandleCreated)
                    {
                        progressForm.Invoke(new Action(() =>
                        {
                            if (e.Error != null)
                            {
                                MessageBox.Show($"下載失敗：{e.Error.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                progressForm.Close();
                                return;
                            }

                            labelStatus.Text = "下載完成，正在解壓縮...";
                            progressBar.Style = ProgressBarStyle.Marquee;
                        }));
                    }
                };

                try
                {
                    client.DownloadFileAsync(new Uri(downloadUrl), cabPath);

                    while (client.IsBusy)
                    {
                        Application.DoEvents();
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"下載失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    progressForm?.Close();
                    return;
                }
            }
        }

        string coreDllPath = Path.Combine($"{extractPath}\\{pathName}", "msedge.dll");

        if (!File.Exists(coreDllPath))
        {
            if (progressForm == null)
            {
                ShowProgressForm("正在解壓縮 WebView2 Runtime...", ProgressBarStyle.Marquee);
            }
            else
            {
                labelStatus.Text = "正在解壓縮 WebView2 Runtime...";
                progressBar.Style = ProgressBarStyle.Marquee;
            }

            Directory.CreateDirectory(extractPath);

            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "expand.exe"),
                    Arguments = $"\"{cabPath}\" -F:* \"{extractPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            try
            {
                process.Start();
                process.WaitForExit();


                labelStatus?.Invoke(new Action(() => labelStatus.Text = "初始化 WebView2 中..."));
                progressBar?.Invoke(new Action(() => progressBar.Style = ProgressBarStyle.Marquee));

                progressForm?.Close();

                try
                {
                    var env = await CoreWebView2Environment.CreateAsync(pathName + "\\" + pathName);
                    await webView.EnsureCoreWebView2Async(env);
                    webView.CoreWebView2.Navigate(url);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"WebView2 初始化失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解壓縮失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                progressForm?.Close();
                return;
            }
        }
        else
        {
            labelStatus?.Invoke(new Action(() => labelStatus.Text = "初始化 WebView2 中..."));
            progressBar?.Invoke(new Action(() => progressBar.Style = ProgressBarStyle.Marquee));

            try
            {
                var env = await CoreWebView2Environment.CreateAsync(pathName + "\\" + pathName);
                await webView.EnsureCoreWebView2Async(env);
                webView.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 初始化失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            progressForm?.Close();
        }
    }
    private async void InitWebView2DefaultCore(string url)
    {
        webView.CoreWebView2InitializationCompleted += (s, e) =>
        {
            webView.CoreWebView2.Navigate(url);
        };
        await webView.EnsureCoreWebView2Async(null);
    }

    private void Resized(int padding)
    {
        int buttonWidth = backButton.Width + forwardButton.Width;
        int totalWidth = this.ClientSize.Width;

        urlBox.Width = totalWidth - buttonWidth - (padding * 3);
        backButton.Left = urlBox.Right + padding;
        forwardButton.Left = urlBox.Right + padding;
    }
    private void Search()
    {
        if (!string.IsNullOrWhiteSpace(urlBox.Text))
        {
            string inputUrl = urlBox.Text;
            if (!inputUrl.StartsWith("http"))
                inputUrl = "https://" + inputUrl;

            webView.CoreWebView2.Navigate(inputUrl);
        }
    }
    private async void UpdateIcon()
    {
        var faviconUri = webView.CoreWebView2.FaviconUri;

        if (!string.IsNullOrEmpty(faviconUri))
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var bytes = await client.GetByteArrayAsync(faviconUri);
                    using (var ms = new MemoryStream(bytes))
                    using (var image = Image.FromStream(ms))
                    {
                        // 將圖片轉為 .ico 格式並儲存到記憶體
                        using (var iconStream = new MemoryStream())
                        {
                            SaveImageAsIcon(image, iconStream, 32);
                            iconStream.Seek(0, SeekOrigin.Begin);
                            this.Icon = new Icon(iconStream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"載入 favicon 失敗: {ex.Message}");
            }
            return;
        }
        SetIconFromPng("res\\faviconV2.png");
    }
    public class CenteredTextBox : TextBox
    {
        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            this.Multiline = true;
            this.TextAlign = HorizontalAlignment.Left;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.SetPadding();
        }

        private void SetPadding()
        {
            int padding = (this.Height - this.Font.Height) / 2;
            this.Padding = new Padding(0, padding, 0, 0);
        }
    }
    public static void SaveImageAsIcon(Image image, Stream outputStream, int iconSize = 32)
    {
        using (var resized = new Bitmap(image, new Size(iconSize, iconSize)))
        using (var ms = new MemoryStream())
        {
            resized.Save(ms, ImageFormat.Png);
            var pngBytes = ms.ToArray();

            using (var bw = new BinaryWriter(outputStream, System.Text.Encoding.UTF8, true))
            {
                bw.Write((short)0);   // Reserved
                bw.Write((short)1);   // Type: 1 = icon
                bw.Write((short)1);   // Number of images

                bw.Write((byte)iconSize); // Width
                bw.Write((byte)iconSize); // Height
                bw.Write((byte)0);        // No color palette
                bw.Write((byte)0);        // Reserved
                bw.Write((short)1);       // Color planes
                bw.Write((short)32);      // Bits per pixel
                bw.Write(pngBytes.Length); // Image data size
                bw.Write(22);             // Offset to image data

                bw.Write(pngBytes);
            }
        }
    }
    private void SetIconFromPng(string relativePath)
    {
        string fullPath = Path.Combine(Application.StartupPath, relativePath);

        if (File.Exists(fullPath))
        {
            using (Bitmap bitmap = new Bitmap(fullPath))
            {
                IntPtr hIcon = bitmap.GetHicon();
                Icon icon = Icon.FromHandle(hIcon);
                this.Icon = icon;
            }
        }
        else
        {
            MessageBox.Show("找不到圖示檔案：" + fullPath);
        }
    }

    [STAThread]
    private static void Main(string[] args)
    {
        string url = args.Length > 0 ? args[0] : "https://www.google.com";
        Application.EnableVisualStyles();
        Application.Run(new DarknessApp(url));
    }
}