using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Net;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using System.Reflection;
using Microsoft.Win32;

[assembly: AssemblyTitle("DevSpace Quick Tunnel Tray")]
[assembly: AssemblyProduct("DevSpace Quick Tunnel Tray")]
[assembly: AssemblyDescription("Portable Windows tray launcher for DevSpace over Cloudflare Tunnel")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

namespace DevSpaceQuickTunnelTray
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null && args.Length == 2 &&
                string.Equals(args[0], "--service-action", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = CloudflaredServiceManager.Execute(args[1]);
                return;
            }

            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\DevSpaceQuickTunnelTray", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "DevSpace 临时隧道托盘程序已经在运行。请查看任务栏右下角。",
                        "DevSpace Quick Tunnel",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TunnelApplicationContext());
            }
        }
    }

    internal static class CloudflaredServiceManager
    {
        private const string ServiceName = "cloudflared";

        public static string QueryStatus()
        {
            try
            {
                using (var controller = new ServiceController(ServiceName))
                {
                    return TranslateStatus(controller.Status);
                }
            }
            catch (InvalidOperationException)
            {
                return "未安装或不可读取";
            }
            catch (Exception exception)
            {
                return "查询失败：" + exception.Message;
            }
        }

        public static bool CanUseService(out string error)
        {
            error = string.Empty;
            try
            {
                using (var controller = new ServiceController(ServiceName))
                {
                    var ignored = controller.Status;
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                error = "未安装 cloudflared Windows Service，或当前用户无法读取该服务。";
                return false;
            }
            catch (Exception exception)
            {
                error = "无法读取 cloudflared Windows Service：" + exception.Message;
                return false;
            }
        }

        public static bool IsServiceRunning(out string error)
        {
            error = string.Empty;
            try
            {
                using (var controller = new ServiceController(ServiceName))
                {
                    controller.Refresh();
                    if (controller.Status == ServiceControllerStatus.Running)
                    {
                        return true;
                    }
                    error = "cloudflared Windows Service 当前状态不是 Running：" + TranslateStatus(controller.Status) + "。";
                    return false;
                }
            }
            catch (InvalidOperationException)
            {
                error = "未安装 cloudflared Windows Service，或当前用户无法读取该服务。";
                return false;
            }
            catch (Exception exception)
            {
                error = "无法读取 cloudflared Windows Service：" + exception.Message;
                return false;
            }
        }

        public static int Execute(string action)
        {
            try
            {
                using (var controller = new ServiceController(ServiceName))
                {
                    controller.Refresh();
                    if (controller.Status == ServiceControllerStatus.StartPending)
                    {
                        controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                        controller.Refresh();
                    }
                    else if (controller.Status == ServiceControllerStatus.StopPending)
                    {
                        controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                        controller.Refresh();
                    }

                    if (string.Equals(action, "start", StringComparison.OrdinalIgnoreCase))
                    {
                        if (controller.Status != ServiceControllerStatus.Running)
                        {
                            controller.Start();
                            controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                        }
                    }
                    else if (string.Equals(action, "stop", StringComparison.OrdinalIgnoreCase))
                    {
                        if (controller.Status != ServiceControllerStatus.Stopped)
                        {
                            controller.Stop();
                            controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                        }
                    }
                    else if (string.Equals(action, "restart", StringComparison.OrdinalIgnoreCase))
                    {
                        if (controller.Status != ServiceControllerStatus.Stopped)
                        {
                            controller.Stop();
                            controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                        }
                        controller.Start();
                        controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                    }
                    else
                    {
                        return 2;
                    }
                }
                return 0;
            }
            catch
            {
                return 1;
            }
        }

        private static string TranslateStatus(ServiceControllerStatus status)
        {
            switch (status)
            {
                case ServiceControllerStatus.Running: return "运行中";
                case ServiceControllerStatus.Stopped: return "已停止";
                case ServiceControllerStatus.StartPending: return "正在启动";
                case ServiceControllerStatus.StopPending: return "正在停止";
                case ServiceControllerStatus.Paused: return "已暂停";
                case ServiceControllerStatus.PausePending: return "正在暂停";
                case ServiceControllerStatus.ContinuePending: return "正在恢复";
                default: return status.ToString();
            }
        }
    }

    internal sealed class AppSettings
    {
        public int SchemaVersion { get; set; }
        public string WorkspaceRoot { get; set; }
        public string ToolMode { get; set; }
        public int LocalPort { get; set; }
        public string TunnelMode { get; set; }
        public string FixedHostname { get; set; }
        public string NamedTunnelIdOrName { get; set; }
        public string CredentialsFilePath { get; set; }
        public string CloudflaredConfigPath { get; set; }
        public bool AutoStart { get; set; }

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                SchemaVersion = 1,
                WorkspaceRoot = string.Empty,
                ToolMode = "minimal",
                LocalPort = 7676,
                TunnelMode = string.Empty,
                FixedHostname = string.Empty,
                NamedTunnelIdOrName = string.Empty,
                CredentialsFilePath = string.Empty,
                CloudflaredConfigPath = string.Empty,
                AutoStart = false
            };
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                SchemaVersion = SchemaVersion,
                WorkspaceRoot = WorkspaceRoot,
                ToolMode = ToolMode,
                LocalPort = LocalPort,
                TunnelMode = TunnelMode,
                FixedHostname = FixedHostname,
                NamedTunnelIdOrName = NamedTunnelIdOrName,
                CredentialsFilePath = CredentialsFilePath,
                CloudflaredConfigPath = CloudflaredConfigPath,
                AutoStart = AutoStart
            };
        }

        public bool IsNamedTunnel
        {
            get { return string.Equals(TunnelMode, "Named", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsServiceTunnel
        {
            get { return string.Equals(TunnelMode, "Service", StringComparison.OrdinalIgnoreCase); }
        }

        public bool HasFixedHostname
        {
            get { return IsNamedTunnel || IsServiceTunnel; }
        }
    }

    internal sealed class TunnelApplicationContext : ApplicationContext
    {
        private static readonly Regex TunnelUrlRegex = new Regex(
            @"https://[a-z0-9-]+\.trycloudflare\.com",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly object stateLock = new object();
        private readonly NotifyIcon trayIcon;
        private readonly System.Windows.Forms.Timer uiTimer;
        private Process tunnelProcess;
        private Process devSpaceProcess;
        private ChildProcessJob processJob;
        private StatusForm statusForm;
        private System.Windows.Forms.Timer backendHealthTimer;
        private string tunnelUrl = string.Empty;
        private string statusText = "正在启动…";
        private string detailText = "";
        private string backendStatus = "等待启动";
        private string ownerToken = string.Empty;
        private string cloudflareServiceStatus = "未检查";
        private DateTime lastCloudflareServiceCheckUtc = DateTime.MinValue;
        private bool exiting;
        private bool restarting;
        private bool balloonShownForCurrentUrl;
        private bool namedTunnelStartRequested;
        private bool serviceOperationInProgress;
        private volatile bool backendRestartRequested;
        private DateTime backendStartedUtc = DateTime.MinValue;
        private AppSettings settings;

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "settings.json");
            }
        }

        private static string BackendLogPath
        {
            get
            {
                return Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "DevSpaceQuickTunnelTray.log");
            }
        }

        public TunnelApplicationContext()
        {
            string settingsError;
            settings = LoadSettings(out settingsError);
            ApplyAutoStart(settings.AutoStart);

            var menu = new ContextMenuStrip();
            menu.Items.Add("显示地址", null, delegate { ShowStatusWindow(); });
            menu.Items.Add("复制公网地址", null, delegate { CopyTunnelUrl(); });
            menu.Items.Add("复制 MCP 地址", null, delegate { CopyMcpUrl(); });
            menu.Items.Add("复制 DevSpace 更新命令", null, delegate { CopyDevSpaceCommand(); });
            menu.Items.Add("复制 Owner password", null, delegate { CopyOwnerToken(); });
            menu.Items.Add(new ToolStripSeparator());
            var cloudflareMenu = new ToolStripMenuItem("Cloudflare 服务");
            cloudflareMenu.DropDownItems.Add("查看状态", null, delegate { ShowCloudflareServiceStatus(); });
            cloudflareMenu.DropDownItems.Add("启动服务", null, delegate { StartCloudflareService(); });
            cloudflareMenu.DropDownItems.Add("停止服务", null, delegate { StopCloudflareService(); });
            cloudflareMenu.DropDownItems.Add("重启服务", null, delegate { RestartCloudflareService(); });
            menu.Items.Add(cloudflareMenu);
            menu.Items.Add("重新启动全部", null, delegate { RestartAll(); });
            menu.Items.Add("退出托盘程序", null, delegate { ConfirmExit(); });

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("设置...", null, delegate { ShowSettingsDialog(true); });

            trayIcon = new NotifyIcon();
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.Text = "DevSpace Quick Tunnel";
            trayIcon.ContextMenuStrip = menu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += delegate { ShowStatusWindow(); };

            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 300;
            uiTimer.Tick += delegate { RefreshUi(); };
            uiTimer.Start();

            backendHealthTimer = new System.Windows.Forms.Timer();
            backendHealthTimer.Interval = 10000;
            backendHealthTimer.Tick += delegate { CheckBackendHealth(); };
            backendHealthTimer.Start();

            if (!string.IsNullOrEmpty(settingsError))
            {
                MessageBox.Show(
                    settingsError + "\r\n请检查设置后再启动。",
                    "DevSpace Quick Tunnel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                if (!ShowSettingsDialog(false))
                {
                    SetState("设置无效，尚未启动", settingsError, string.Empty);
                    ShowStatusWindow();
                    return;
                }
            }

            StartTunnel();
            ShowStatusWindow();
        }

        private string CloudflaredPath
        {
            get
            {
                var besideApplication = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "cloudflared.exe");
                if (File.Exists(besideApplication))
                {
                    return besideApplication;
                }

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "cloudflared",
                    "cloudflared.exe");
            }
        }

        private static void ApplyAutoStart(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (key != null)
                    {
                        if (enabled)
                        {
                            key.SetValue(
                                "DevSpaceQuickTunnelTray",
                                "\"" + Application.ExecutablePath + "\"",
                                RegistryValueKind.String);
                        }
                        else
                        {
                            key.DeleteValue("DevSpaceQuickTunnelTray", false);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                AppendBackendLog("AUTOSTART ERROR " + exception.Message);
            }
        }

        private static AppSettings LoadSettings(out string error)
        {
            error = string.Empty;
            if (!File.Exists(SettingsPath))
            {
                error = "首次运行：请先完成工作区和 Cloudflare Tunnel 配置。所有必填条件满足前不会启动。";
                return AppSettings.CreateDefault();
            }

            try
            {
                var serializer = new JavaScriptSerializer();
                var value = serializer.Deserialize<AppSettings>(
                    File.ReadAllText(SettingsPath, Encoding.UTF8));
                string validationError = "设置内容为空。";
                if (value == null || !ValidateSettings(value, out validationError))
                {
                    error = "settings.json 无效：" + validationError;
                    return value ?? AppSettings.CreateDefault();
                }
                return value;
            }
            catch (Exception exception)
            {
                error = "无法读取 settings.json：" + exception.Message;
                return AppSettings.CreateDefault();
            }
        }

        private static void SaveSettings(AppSettings value)
        {
            string validationError;
            if (!ValidateSettings(value, out validationError))
            {
                throw new InvalidDataException(validationError);
            }

            var serializer = new JavaScriptSerializer();
            WriteJsonText(SettingsPath, serializer.Serialize(value));
            RestrictPrivateFile(SettingsPath);
        }

        internal static bool ValidateSettings(AppSettings value, out string error)
        {
            error = string.Empty;
            if (value == null)
            {
                error = "设置不能为空。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(value.WorkspaceRoot) ||
                !Directory.Exists(value.WorkspaceRoot))
            {
                error = "工作区根目录不存在。";
                return false;
            }
            if (value.LocalPort < 1 || value.LocalPort > 65535)
            {
                error = "本地端口必须在 1 到 65535 之间。";
                return false;
            }
            if (!string.Equals(value.ToolMode, "minimal", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(value.ToolMode, "full", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(value.ToolMode, "codex", StringComparison.OrdinalIgnoreCase))
            {
                error = "Tool mode 必须是 minimal、full 或 codex。";
                return false;
            }
            if (!string.Equals(value.TunnelMode, "Quick", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(value.TunnelMode, "Named", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(value.TunnelMode, "Service", StringComparison.OrdinalIgnoreCase))
            {
                error = "请选择 Tunnel 模式：Quick、Named 或 Service。程序不会默认选择。";
                return false;
            }
            if (ContainsUnsafeArgumentText(value.WorkspaceRoot) ||
                ContainsUnsafeArgumentText(value.NamedTunnelIdOrName) ||
                ContainsUnsafeArgumentText(value.CredentialsFilePath) ||
                ContainsUnsafeArgumentText(value.CloudflaredConfigPath))
            {
                error = "设置中不能包含引号、换行符或 NUL 字符。";
                return false;
            }
            if (value.HasFixedHostname)
            {
                if (string.IsNullOrWhiteSpace(value.FixedHostname) ||
                    !Regex.IsMatch(value.FixedHostname.Trim(),
                        @"^(?=.{1,253}$)([a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,63}$"))
                {
                    error = "固定 Tunnel 需要合法的 hostname（不含 https://）。";
                    return false;
                }
            }
            if (value.IsNamedTunnel)
            {
                if (string.IsNullOrWhiteSpace(value.NamedTunnelIdOrName))
                {
                    error = "Named Tunnel UUID/名称不能为空。";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(value.CredentialsFilePath) ||
                    !File.Exists(value.CredentialsFilePath))
                {
                    error = "credentials-file 不存在。";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(value.CloudflaredConfigPath) ||
                    !File.Exists(value.CloudflaredConfigPath))
                {
                    error = "cloudflared config 文件不存在。";
                    return false;
                }
            }
            if (value.IsServiceTunnel)
            {
                string serviceError;
                if (!CloudflaredServiceManager.CanUseService(out serviceError))
                {
                    error = serviceError;
                    return false;
                }
            }
            return true;
        }

        private static bool ContainsUnsafeArgumentText(string value)
        {
            return value != null &&
                (value.IndexOf('\0') >= 0 || value.IndexOf('\r') >= 0 ||
                 value.IndexOf('\n') >= 0 || value.IndexOf('"') >= 0);
        }

        private bool ShowSettingsDialog(bool restartAfterSave)
        {
            using (var form = new SettingsForm(settings.Clone()))
            {
                if (form.ShowDialog() != DialogResult.OK)
                {
                    return false;
                }
                try
                {
                    SaveSettings(form.Value);
                    settings = form.Value.Clone();
                    ApplyAutoStart(settings.AutoStart);
                    if (restartAfterSave)
                    {
                        RestartTunnel();
                    }
                    return true;
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        "无法保存设置：" + exception.Message,
                        "DevSpace Quick Tunnel",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return false;
                }
            }
        }

        private static void WriteJsonText(string path, string json)
        {
            var temporaryPath = path + ".tmp";
            File.WriteAllText(
                temporaryPath,
                json + Environment.NewLine,
                new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null, true);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }

        private bool ValidateStartupPrerequisites(out string error)
        {
            if (!ValidateSettings(settings, out error))
            {
                return false;
            }

            if (!settings.IsServiceTunnel && !File.Exists(CloudflaredPath))
            {
                error = "未找到 cloudflared.exe。请先运行 setup-runtime.ps1，所有运行时条件满足后程序才会启动。";
                return false;
            }

            var bundledNodePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "runtime",
                "node-v22.22.3-win-x64",
                "node.exe");
            var systemNodePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "nodejs",
                "node.exe");
            var bundledCliPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "runtime",
                "devspace",
                "node_modules",
                "@waishnav",
                "devspace",
                "dist",
                "cli.js");
            var globalCliPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "npm",
                "node_modules",
                "@waishnav",
                "devspace",
                "dist",
                "cli.js");

            if ((!File.Exists(bundledNodePath) || !File.Exists(bundledCliPath)) &&
                (!File.Exists(systemNodePath) || !File.Exists(globalCliPath)))
            {
                error = "DevSpace 运行时不完整：Node.js 与 DevSpace CLI 必须同时可用。请先运行 setup-runtime.ps1。";
                return false;
            }

            if (settings.IsServiceTunnel)
            {
                string serviceError;
                if (!CloudflaredServiceManager.IsServiceRunning(out serviceError))
                {
                    error = serviceError + " Service 模式不会先启动 DevSpace；请先启动 cloudflared 服务。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void StartTunnel()
        {
            string startupError;
            if (!ValidateStartupPrerequisites(out startupError))
            {
                SetState("启动已阻止", startupError, string.Empty);
                return;
            }

            namedTunnelStartRequested = false;
            if (settings.HasFixedHostname)
            {
                if (settings.IsServiceTunnel)
                {
                    RefreshCloudflareServiceStatus(true);
                }
                try
                {
                    if (processJob == null)
                    {
                        processJob = new ChildProcessJob();
                    }
                }
                catch (Exception exception)
                {
                    SetState("无法创建子进程保护", exception.Message, string.Empty);
                    return;
                }

                var publicBaseUrl = "https://" + settings.FixedHostname.Trim().TrimEnd('/');
                var detail = settings.IsServiceTunnel
                    ? "Cloudflare Windows 服务（由系统独立运行）"
                    : "Named Tunnel: " + settings.NamedTunnelIdOrName;
                SetState("正在启动 DevSpace", detail, publicBaseUrl);
                ConfigureAndStartDevSpace(publicBaseUrl);
                return;
            }

            StartCloudflared(
                "tunnel --url " + QuoteArgument("http://127.0.0.1:" + settings.LocalPort) +
                " --no-autoupdate");
        }

        private void StartNamedTunnel()
        {
            StartCloudflared(
                "tunnel --config " + QuoteArgument(settings.CloudflaredConfigPath) +
                " --credentials-file " + QuoteArgument(settings.CredentialsFilePath) +
                " --no-autoupdate run " + QuoteArgument(settings.NamedTunnelIdOrName));
        }

        private static string QuoteArgument(string value)
        {
            if (ContainsUnsafeArgumentText(value))
            {
                throw new InvalidDataException("命令行参数包含不安全字符。");
            }
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private void StartCloudflared(string arguments)
        {
            try
            {
                if (processJob == null)
                {
                    processJob = new ChildProcessJob();
                }
            }
            catch (Exception exception)
            {
                SetState("无法创建子进程保护", exception.Message, string.Empty);
                return;
            }

            lock (stateLock)
            {
                if (!settings.IsNamedTunnel)
                {
                    tunnelUrl = string.Empty;
                    backendStatus = "等待临时域名";
                }
                statusText = settings.IsNamedTunnel
                    ? "正在启动 Named Tunnel…"
                    : "正在启动临时隧道…";
                detailText = "目标：http://127.0.0.1:" + settings.LocalPort;
                balloonShownForCurrentUrl = false;
            }

            var executable = CloudflaredPath;
            if (!File.Exists(executable))
            {
                SetState(
                    "未找到 cloudflared.exe",
                    "预期位置：" + executable,
                    string.Empty);
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo();
                startInfo.FileName = executable;
                startInfo.Arguments = arguments;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;

                var process = new Process();
                process.StartInfo = startInfo;
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += OnCloudflaredOutput;
                process.ErrorDataReceived += OnCloudflaredOutput;
                process.Exited += OnCloudflaredExited;

                if (!process.Start())
                {
                    SetState("启动失败", "cloudflared 没有创建进程。", string.Empty);
                    process.Dispose();
                    return;
                }

                tunnelProcess = process;
                processJob.Add(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception exception)
            {
                SetState("启动失败", exception.Message, string.Empty);
            }
        }

        private void OnCloudflaredOutput(object sender, DataReceivedEventArgs eventArgs)
        {
            var line = eventArgs.Data;
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            var match = settings.IsNamedTunnel
                ? Match.Empty
                : TunnelUrlRegex.Match(line);
            if (match.Success)
            {
                var discoveredUrl = match.Value.TrimEnd('/');
                lock (stateLock)
                {
                    if (string.Equals(tunnelUrl, discoveredUrl, StringComparison.OrdinalIgnoreCase) &&
                        devSpaceProcess != null && !devSpaceProcess.HasExited)
                    {
                        return;
                    }
                }

                SetState(
                    "临时隧道已就绪",
                    "DevSpace 本地目标：http://127.0.0.1:" + settings.LocalPort,
                    discoveredUrl);
                ConfigureAndStartDevSpace(discoveredUrl);
                return;
            }

            if (settings.IsNamedTunnel &&
                line.IndexOf("Registered tunnel connection", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SetState(
                    "Named Tunnel 已连接",
                    "固定地址：https://" + settings.FixedHostname.Trim(),
                    "https://" + settings.FixedHostname.Trim());
            }

            if (line.IndexOf("ERR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                lock (stateLock)
                {
                    detailText = line;
                }
            }
        }

        private void OnCloudflaredExited(object sender, EventArgs eventArgs)
        {
            if (exiting || restarting)
            {
                return;
            }

            var exitCodeText = "";
            try
            {
                exitCodeText = "退出代码：" + ((Process)sender).ExitCode;
            }
            catch
            {
                exitCodeText = "cloudflared 已退出。";
            }

            SetState(
                settings.IsNamedTunnel ? "Named Tunnel 已停止" : "临时隧道已停止",
                exitCodeText,
                settings.IsNamedTunnel ? "https://" + settings.FixedHostname.Trim() : string.Empty);
        }

        private void SetState(string status, string detail, string url)
        {
            lock (stateLock)
            {
                statusText = status;
                detailText = detail;
                tunnelUrl = url ?? string.Empty;
            }
        }

        private void ConfigureAndStartDevSpace(string publicBaseUrl)
        {
            try
            {
                lock (stateLock)
                {
                    backendStatus = "正在写入 DevSpace 配置…";
                }

                var configDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".devspace");
                Directory.CreateDirectory(configDirectory);

                var serializer = new JavaScriptSerializer();
                var configPath = Path.Combine(configDirectory, "config.json");
                var authPath = Path.Combine(configDirectory, "auth.json");

                var config = LoadJsonObject(serializer, configPath);
                config["host"] = "127.0.0.1";
                config["port"] = settings.LocalPort;
                config["allowedRoots"] = new object[]
                {
                    settings.WorkspaceRoot
                };
                config["publicBaseUrl"] = publicBaseUrl;
                config["subagents"] = new Dictionary<string, object>
                {
                    { "enabled", false },
                    {
                        "providers",
                        new object[]
                        {
                            new Dictionary<string, object>
                            {
                                { "id", "codex" },
                                { "enabled", true }
                            }
                        }
                    }
                };

                var auth = LoadJsonObject(serializer, authPath);
                object existingToken;
                var token = auth.TryGetValue("ownerToken", out existingToken)
                    ? Convert.ToString(existingToken)
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(token) || token.Length < 16)
                {
                    token = GenerateOwnerToken();
                    auth["ownerToken"] = token;
                }

                WriteJson(serializer, configPath, config);
                WriteJson(serializer, authPath, auth);
                RestrictAuthFile(authPath);

                lock (stateLock)
                {
                    ownerToken = token;
                    backendStatus = "配置已写入，正在启动后端…";
                }

                StartDevSpaceBackend(publicBaseUrl);
            }
            catch (Exception exception)
            {
                lock (stateLock)
                {
                    backendStatus = "配置失败";
                    detailText = exception.Message;
                }
            }
        }

        private static Dictionary<string, object> LoadJsonObject(
            JavaScriptSerializer serializer,
            string path)
        {
            if (!File.Exists(path))
            {
                return new Dictionary<string, object>();
            }

            var text = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Dictionary<string, object>();
            }

            var value = serializer.Deserialize<Dictionary<string, object>>(text);
            if (value == null)
            {
                throw new InvalidDataException("无法读取配置文件：" + path);
            }
            return value;
        }

        private static void WriteJson(
            JavaScriptSerializer serializer,
            string path,
            Dictionary<string, object> value)
        {
            var temporaryPath = path + ".tmp";
            File.WriteAllText(
                temporaryPath,
                serializer.Serialize(value) + Environment.NewLine,
                new UTF8Encoding(false));
            File.Copy(temporaryPath, path, true);
            File.Delete(temporaryPath);
        }

        private static string GenerateOwnerToken()
        {
            var bytes = new byte[32];
            using (var random = new RNGCryptoServiceProvider())
            {
                random.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static void RestrictAuthFile(string path)
        {
            RestrictPrivateFile(path);
        }

        private static void RestrictPrivateFile(string path)
        {
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser == null)
            {
                throw new InvalidOperationException("无法识别当前 Windows 用户。 ");
            }

            var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false);
            security.SetOwner(currentUser);
            security.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            File.SetAccessControl(path, security);
        }

        private void StartDevSpaceBackend(string publicBaseUrl)
        {
            StopOwnedDevSpace();

            var bundledNodePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "runtime",
                "node-v22.22.3-win-x64",
                "node.exe");
            var systemNodePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "nodejs",
                "node.exe");
            var bundledCliPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "runtime",
                "devspace",
                "node_modules",
                "@waishnav",
                "devspace",
                "dist",
                "cli.js");
            var globalCliPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "npm",
                "node_modules",
                "@waishnav",
                "devspace",
                "dist",
                "cli.js");
            var useBundledRuntime = File.Exists(bundledNodePath) && File.Exists(bundledCliPath);
            var nodePath = useBundledRuntime ? bundledNodePath : systemNodePath;
            var cliPath = useBundledRuntime ? bundledCliPath : globalCliPath;

            if (!File.Exists(nodePath))
            {
                throw new FileNotFoundException(
                    "未找到 Node.js。首次使用轻量 Release 时，请先运行 setup-runtime.ps1 安装固定版本运行时。",
                    nodePath);
            }
            if (!File.Exists(cliPath))
            {
                throw new FileNotFoundException(
                    "未找到 DevSpace CLI。首次使用轻量 Release 时，请先运行 setup-runtime.ps1 安装固定版本运行时。",
                    cliPath);
            }

            var startInfo = new ProcessStartInfo();
            startInfo.FileName = nodePath;
            startInfo.Arguments = "\"" + cliPath + "\" serve";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.EnvironmentVariables["DEVSPACE_SUBAGENTS"] = "0";
            // Select the exact DevSpace tool surface requested in settings.
            // ChatGPT can use codex mode when full file lifecycle operations
            // (including delete and move) are required.
            startInfo.EnvironmentVariables["DEVSPACE_TOOL_MODE"] = settings.ToolMode.ToLowerInvariant();
            startInfo.EnvironmentVariables["DEVSPACE_WIDGETS"] = "off";
            startInfo.EnvironmentVariables["DEVSPACE_PUBLIC_BASE_URL"] = publicBaseUrl;
            startInfo.EnvironmentVariables["DEVSPACE_TRUST_PROXY"] = "1";

            // DevSpace's standard shell tool requires Bash. Put Git Bash ahead
            // of Windows' WSL bash shim so local commands do not inherit WSL's
            // proxy/NAT warnings or path translation behavior.
            var gitBashDirectory = FindGitBashDirectory();
            if (!string.IsNullOrEmpty(gitBashDirectory))
            {
                startInfo.EnvironmentVariables["PATH"] =
                    gitBashDirectory + ";" +
                    Path.GetDirectoryName(nodePath) + ";" +
                    startInfo.EnvironmentVariables["PATH"];
            }

            var process = new Process();
            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += OnDevSpaceOutput;
            process.ErrorDataReceived += OnDevSpaceOutput;
            process.Exited += OnDevSpaceExited;

            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("DevSpace 后端没有创建进程。");
            }

            devSpaceProcess = process;
            backendStartedUtc = DateTime.UtcNow;
            processJob.Add(process);
            lock (stateLock)
            {
                backendStatus = "正在启动，PID " + process.Id;
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        private static string FindGitBashDirectory()
        {
            var installRoots = new List<string>();
            foreach (var registryPath in new[]
            {
                @"HKEY_LOCAL_MACHINE\SOFTWARE\GitForWindows",
                @"HKEY_CURRENT_USER\SOFTWARE\GitForWindows"
            })
            {
                var value = Registry.GetValue(registryPath, "InstallPath", null) as string;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    installRoots.Add(value);
                }
            }

            installRoots.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git"));
            installRoots.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git"));

            foreach (var root in installRoots)
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }
                var bin = Path.Combine(root, "bin");
                if (File.Exists(Path.Combine(bin, "bash.exe")))
                {
                    return bin;
                }
            }

            var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var directory in pathValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = directory.Trim().Trim('"');
                if (File.Exists(Path.Combine(candidate, "bash.exe")) &&
                    File.Exists(Path.Combine(candidate, "git.exe")))
                {
                    return candidate;
                }
            }
            return string.Empty;
        }

        private void OnDevSpaceOutput(object sender, DataReceivedEventArgs eventArgs)
        {
            var line = eventArgs.Data;
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            AppendBackendLog("DEVSPACE " + line);

            lock (stateLock)
            {
                if (line.IndexOf("devspace listening", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    backendStatus = "运行中，监听 127.0.0.1:" + settings.LocalPort;
                }
                else if (line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         line.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    backendStatus = "后端报告错误";
                    detailText = line;
                }
            }
        }

        private void OnDevSpaceExited(object sender, EventArgs eventArgs)
        {
            var process = sender as Process;
            var exitCode = "unknown";
            try
            {
                if (process != null)
                {
                    // The stop/restart path can dispose the Process before the
                    // asynchronous Exited callback runs. ExitCode is best-effort
                    // diagnostic data and must never terminate the tray.
                    exitCode = process.ExitCode.ToString();
                }
            }
            catch (ObjectDisposedException)
            {
                exitCode = "disposed";
            }
            catch (InvalidOperationException)
            {
                exitCode = "disposed";
            }
            AppendBackendLog("DEVSPACE EXIT code=" + exitCode);

            if (exiting || restarting)
            {
                return;
            }

            lock (stateLock)
            {
                backendStatus = exitCode == "0" && IsPortListening(settings.LocalPort)
                    ? "运行中，监听 127.0.0.1:" + settings.LocalPort
                    : "DevSpace 后端已停止（退出码 " + exitCode + "）";
            }
            backendRestartRequested = true;
        }

        private void CheckBackendHealth()
        {
            if (exiting || restarting || backendRestartRequested)
            {
                return;
            }

            var process = devSpaceProcess;
            if (process == null)
            {
                backendRestartRequested = true;
                return;
            }

            try
            {
                if (process.HasExited)
                {
                    backendRestartRequested = true;
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                backendRestartRequested = true;
                return;
            }
            catch (InvalidOperationException)
            {
                backendRestartRequested = true;
                return;
            }

            if (DateTime.UtcNow - backendStartedUtc < TimeSpan.FromSeconds(5))
            {
                return;
            }

            if (!IsBackendHealthy(settings.LocalPort))
            {
                RestartDevSpaceBackend("/healthz 检查失败");
            }
        }

        private static bool IsBackendHealthy(int port)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(
                    "http://127.0.0.1:" + port + "/healthz");
                request.Method = "GET";
                request.Proxy = null;
                request.Timeout = 1500;
                request.ReadWriteTimeout = 1500;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }
        }

        private void RestartDevSpaceBackend(string reason)
        {
            if (exiting || restarting)
            {
                return;
            }

            var publicBaseUrl = CurrentTunnelUrl();
            if (string.IsNullOrWhiteSpace(publicBaseUrl))
            {
                return;
            }

            restarting = true;
            backendRestartRequested = false;
            AppendBackendLog("DEVSPACE AUTO-RECOVERY reason=" + reason);
            try
            {
                StopOwnedDevSpace();
                ConfigureAndStartDevSpace(publicBaseUrl);
            }
            finally
            {
                restarting = false;
            }
        }

        private static bool IsPortListening(int port)
        {
            try
            {
                foreach (var endpoint in IPGlobalProperties
                    .GetIPGlobalProperties()
                    .GetActiveTcpListeners())
                {
                    if (endpoint.Port == port)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // The status check is advisory; startup logs remain authoritative.
            }
            return false;
        }

        private static void AppendBackendLog(string message)
        {
            try
            {
                const long maxLogBytes = 5L * 1024L * 1024L;
                if (File.Exists(BackendLogPath) &&
                    new FileInfo(BackendLogPath).Length >= maxLogBytes)
                {
                    var backupPath = BackendLogPath + ".1";
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                    File.Move(BackendLogPath, backupPath);
                }
                File.AppendAllText(
                    BackendLogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch
            {
                // Diagnostics must never bring down the tray application.
            }
        }

        private void RefreshUi()
        {
            if (backendRestartRequested && !exiting && !restarting)
            {
                RestartDevSpaceBackend("后端进程退出");
            }

            RefreshCloudflareServiceStatus(false);
            if (settings.IsNamedTunnel &&
                !namedTunnelStartRequested &&
                tunnelProcess == null &&
                devSpaceProcess != null &&
                !devSpaceProcess.HasExited &&
                IsPortListening(settings.LocalPort))
            {
                namedTunnelStartRequested = true;
                try
                {
                    StartNamedTunnel();
                }
                catch (Exception exception)
                {
                    SetState("Named Tunnel 启动失败", exception.Message,
                        "https://" + settings.FixedHostname.Trim());
                }
            }

            string status;
            string detail;
            string url;
            string backend;
            string token;
            lock (stateLock)
            {
                status = statusText;
                detail = detailText;
                url = tunnelUrl;
                backend = backendStatus;
                token = ownerToken;
            }

            var devSpaceReady = backend.StartsWith("运行中", StringComparison.OrdinalIgnoreCase);
            var serviceReady = !settings.IsServiceTunnel ||
                string.Equals(cloudflareServiceStatus, "运行中", StringComparison.OrdinalIgnoreCase);
            var connectionReady = url.Length > 0 && devSpaceReady && serviceReady;

            trayIcon.Text = connectionReady
                ? "DevSpace Quick Tunnel - 已连接"
                : "DevSpace Quick Tunnel - " + Shorten(status, 28);

            if (statusForm != null && !statusForm.IsDisposed)
            {
                statusForm.UpdateState(
                    status,
                    detail,
                    url,
                    backend,
                    token,
                    settings.IsServiceTunnel
                        ? cloudflareServiceStatus
                        : "由托盘进程管理");
            }

            if (connectionReady &&
                !balloonShownForCurrentUrl &&
                (!settings.IsNamedTunnel ||
                 (tunnelProcess != null && !tunnelProcess.HasExited)))
            {
                balloonShownForCurrentUrl = true;
                trayIcon.ShowBalloonTip(
                    5000,
                    settings.IsServiceTunnel
                        ? "DevSpace 固定地址已就绪"
                        : (settings.IsNamedTunnel ? "Named Tunnel 已连接" : "临时隧道已就绪"),
                    "MCP 地址：" + url + "/mcp",
                    ToolTipIcon.Info);
            }
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }
            return value.Substring(0, maxLength);
        }

        private void ShowStatusWindow()
        {
            if (statusForm == null || statusForm.IsDisposed)
            {
                statusForm = new StatusForm(
                    CopyMcpUrl,
                    CopyDevSpaceCommand,
                    CopyOwnerToken,
                    RestartAll,
                    HideStatusWindow);
            }

            RefreshUi();
            statusForm.Show();
            if (statusForm.WindowState == FormWindowState.Minimized)
            {
                statusForm.WindowState = FormWindowState.Normal;
            }
            statusForm.Activate();
        }

        private void HideStatusWindow()
        {
            if (statusForm != null && !statusForm.IsDisposed)
            {
                statusForm.Hide();
            }
        }

        private string CurrentTunnelUrl()
        {
            lock (stateLock)
            {
                return tunnelUrl;
            }
        }

        private void CopyTunnelUrl()
        {
            CopyWhenReady(CurrentTunnelUrl(), "临时域名");
        }

        private void CopyMcpUrl()
        {
            var url = CurrentTunnelUrl();
            CopyWhenReady(url.Length > 0 ? url + "/mcp" : "", "MCP 地址");
        }

        private void CopyDevSpaceCommand()
        {
            var url = CurrentTunnelUrl();
            CopyWhenReady(
                url.Length > 0
                    ? "devspace.cmd config set publicBaseUrl \"" + url + "\""
                    : "",
                "DevSpace 更新命令");
        }

        private void CopyOwnerToken()
        {
            string token;
            lock (stateLock)
            {
                token = ownerToken;
            }
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(
                    "Owner password 尚未生成，请稍后重试。",
                    "DevSpace Quick Tunnel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                Clipboard.SetText(token);
                trayIcon.ShowBalloonTip(
                    2000,
                    "已复制 Owner password",
                    "敏感值已复制，未在通知中显示。",
                    ToolTipIcon.Info);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "无法写入剪贴板：" + exception.Message,
                    "DevSpace Quick Tunnel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void CopyWhenReady(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                MessageBox.Show(
                    "隧道地址还没有生成，请稍等几秒后重试。",
                    "DevSpace Quick Tunnel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                Clipboard.SetText(value);
                trayIcon.ShowBalloonTip(2000, "已复制" + label, value, ToolTipIcon.Info);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "无法写入剪贴板：" + exception.Message,
                    "DevSpace Quick Tunnel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void RefreshCloudflareServiceStatus(bool force)
        {
            if (!settings.IsServiceTunnel)
            {
                cloudflareServiceStatus = "当前不是 Service 模式";
                return;
            }

            if (!force && DateTime.UtcNow - lastCloudflareServiceCheckUtc < TimeSpan.FromSeconds(3))
            {
                return;
            }

            cloudflareServiceStatus = CloudflaredServiceManager.QueryStatus();
            lastCloudflareServiceCheckUtc = DateTime.UtcNow;
        }

        private bool EnsureServiceMode()
        {
            if (settings.IsServiceTunnel)
            {
                return true;
            }

            MessageBox.Show(
                "Cloudflare Windows 服务管理只用于 Service 模式。\r\n当前模式：" + settings.TunnelMode,
                "DevSpace Quick Tunnel",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return false;
        }

        private void ShowCloudflareServiceStatus()
        {
            if (!EnsureServiceMode())
            {
                return;
            }

            RefreshCloudflareServiceStatus(true);
            MessageBox.Show(
                "Cloudflare 服务：" + cloudflareServiceStatus +
                "\r\n固定域名：https://" + settings.FixedHostname.Trim() +
                "\r\n本地目标：http://127.0.0.1:" + settings.LocalPort,
                "Cloudflare 服务状态",
                MessageBoxButtons.OK,
                string.Equals(cloudflareServiceStatus, "运行中", StringComparison.OrdinalIgnoreCase)
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Warning);
        }

        private void StartCloudflareService()
        {
            ControlCloudflareService("start", "启动", false, RestartTunnel);
        }

        private void StopCloudflareService()
        {
            ControlCloudflareService("stop", "停止", true, null);
        }

        private void RestartCloudflareService()
        {
            ControlCloudflareService("restart", "重启", true, null);
        }

        private void ControlCloudflareService(
            string action,
            string actionLabel,
            bool confirm,
            Action successAction)
        {
            if (!EnsureServiceMode())
            {
                return;
            }

            if (serviceOperationInProgress)
            {
                MessageBox.Show(
                    "另一个 Cloudflare 服务操作正在进行，请等待完成。",
                    "Cloudflare 服务",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (confirm)
            {
                var result = MessageBox.Show(
                    actionLabel + " Cloudflare 服务会暂时中断网页 ChatGPT 的 MCP 连接。是否继续？",
                    "确认" + actionLabel + " Cloudflare 服务",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            Process process;
            try
            {
                var startInfo = new ProcessStartInfo();
                startInfo.FileName = Application.ExecutablePath;
                startInfo.Arguments = "--service-action " + action;
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("未能创建提权服务操作进程。");
                }
            }
            catch (Win32Exception exception)
            {
                var message = exception.NativeErrorCode == 1223
                    ? "操作已取消，Cloudflare 服务没有改变。"
                    : "无法" + actionLabel + " Cloudflare 服务：" + exception.Message;
                MessageBox.Show(
                    message,
                    "Cloudflare 服务",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "无法" + actionLabel + " Cloudflare 服务：" + exception.Message,
                    "Cloudflare 服务",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            serviceOperationInProgress = true;
            var uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            ThreadPool.QueueUserWorkItem(delegate
            {
                string error = null;
                try
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        error = "服务操作失败，退出代码：" + process.ExitCode;
                    }
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                }
                finally
                {
                    process.Dispose();
                }

                uiContext.Post(delegate
                {
                    serviceOperationInProgress = false;
                    lastCloudflareServiceCheckUtc = DateTime.MinValue;
                    RefreshCloudflareServiceStatus(true);
                    RefreshUi();

                    if (!string.IsNullOrEmpty(error))
                    {
                        MessageBox.Show(
                            "无法" + actionLabel + " Cloudflare 服务：" + error,
                            "Cloudflare 服务",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    trayIcon.ShowBalloonTip(
                        2500,
                        "Cloudflare 服务已" + actionLabel,
                        "当前状态：" + cloudflareServiceStatus,
                        ToolTipIcon.Info);
                    if (successAction != null)
                    {
                        successAction();
                    }
                }, null);
            });
        }

        private void RestartAll()
        {
            if (settings.IsServiceTunnel)
            {
                ControlCloudflareService("restart", "重启", true, RestartTunnel);
                return;
            }
            RestartTunnel();
        }

        private void RestartTunnel()
        {
            restarting = true;
            StopOwnedDevSpace();
            StopOwnedTunnel();
            if (processJob != null)
            {
                processJob.Dispose();
                processJob = null;
            }
            restarting = false;
            StartTunnel();
        }

        private void StopOwnedDevSpace()
        {
            var process = devSpaceProcess;
            devSpaceProcess = null;
            if (process == null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            catch
            {
                // The child may already have exited between the checks.
            }
            finally
            {
                process.Dispose();
            }
        }

        private void StopOwnedTunnel()
        {
            var process = tunnelProcess;
            tunnelProcess = null;
            if (process == null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            catch
            {
                // The child may already have exited between the checks.
            }
            finally
            {
                process.Dispose();
            }
        }

        private void ConfirmExit()
        {
            var message = settings.IsServiceTunnel
                ? "退出托盘程序并停止 DevSpace 后端？Cloudflare Windows 服务不会停止。"
                : "退出程序并停止它启动的 DevSpace 与隧道进程？";
            var result = MessageBox.Show(
                message,
                "DevSpace Quick Tunnel",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (result == DialogResult.Yes)
            {
                ExitApplication();
            }
        }

        private void ExitApplication()
        {
            exiting = true;
            uiTimer.Stop();
            StopOwnedDevSpace();
            StopOwnedTunnel();
            if (processJob != null)
            {
                processJob.Dispose();
                processJob = null;
            }
            trayIcon.Visible = false;
            trayIcon.Dispose();
            if (statusForm != null)
            {
                statusForm.Dispose();
            }
            ExitThread();
        }
    }

    internal sealed class ChildProcessJob : IDisposable
    {
        private const uint JobObjectExtendedLimitInformationClass = 9;
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private IntPtr handle;

        public ChildProcessJob()
        {
            handle = CreateJobObject(IntPtr.Zero, null);
            if (handle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var information = new JobObjectExtendedLimitInformation();
            information.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;
            var length = Marshal.SizeOf(typeof(JobObjectExtendedLimitInformation));
            var pointer = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(information, pointer, false);
                if (!SetInformationJobObject(
                        handle,
                        JobObjectExtendedLimitInformationClass,
                        pointer,
                        (uint)length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        public void Add(Process process)
        {
            if (handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException("ChildProcessJob");
            }
            if (!AssignProcessToJobObject(handle, process.Handle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                CloseHandle(handle);
                handle = IntPtr.Zero;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr securityAttributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(
            IntPtr job,
            uint informationClass,
            IntPtr information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly TextBox workspaceBox;
        private readonly ComboBox toolModeBox;
        private readonly NumericUpDown portBox;
        private readonly ComboBox tunnelModeBox;
        private readonly TextBox hostnameBox;
        private readonly TextBox tunnelIdBox;
        private readonly TextBox credentialsBox;
        private readonly TextBox configBox;
        private readonly Button credentialsBrowseButton;
        private readonly Button configBrowseButton;
        private readonly CheckBox autoStartBox;
        private readonly Label tunnelGuideLabel;
        private readonly Label validationLabel;
        private readonly string initialToolMode;

        public AppSettings Value { get; private set; }

        public SettingsForm(AppSettings current)
        {
            initialToolMode = (current.ToolMode ?? "minimal").ToLowerInvariant();
            Text = "DevSpace Quick Tunnel 设置";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(760, 630);
            Font = new Font("Microsoft YaHei UI", 9F);

            workspaceBox = AddTextField("工作区根目录", 20, current.WorkspaceRoot);
            AddBrowseButton(690, 42, delegate
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.SelectedPath = workspaceBox.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        workspaceBox.Text = dialog.SelectedPath;
                    }
                }
            });

            AddLabel("Tool mode（ChatGPT 推荐 minimal）", 86);
            toolModeBox = new ComboBox();
            toolModeBox.Location = new Point(20, 108);
            toolModeBox.Size = new Size(220, 25);
            toolModeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            toolModeBox.Items.AddRange(new object[] { "minimal", "full", "codex" });
            toolModeBox.SelectedItem = initialToolMode;
            Controls.Add(toolModeBox);

            AddLabel("本地端口", 86, 270);
            portBox = new NumericUpDown();
            portBox.Location = new Point(270, 108);
            portBox.Size = new Size(140, 25);
            portBox.Minimum = 1;
            portBox.Maximum = 65535;
            portBox.Value = Math.Max(1, Math.Min(65535, current.LocalPort));
            Controls.Add(portBox);

            AddLabel("Tunnel 模式（必须选择）", 86, 440);
            tunnelModeBox = new ComboBox();
            tunnelModeBox.Location = new Point(440, 108);
            tunnelModeBox.Size = new Size(220, 25);
            tunnelModeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            tunnelModeBox.Items.AddRange(new object[] { "Quick", "Named", "Service" });
            if (current.IsServiceTunnel)
            {
                tunnelModeBox.SelectedItem = "Service";
            }
            else if (current.IsNamedTunnel)
            {
                tunnelModeBox.SelectedItem = "Named";
            }
            else if (string.Equals(current.TunnelMode, "Quick", StringComparison.OrdinalIgnoreCase))
            {
                tunnelModeBox.SelectedItem = "Quick";
            }
            else
            {
                tunnelModeBox.SelectedIndex = -1;
            }
            tunnelModeBox.SelectedIndexChanged += delegate { UpdateNamedControls(); };
            Controls.Add(tunnelModeBox);

            tunnelGuideLabel = new Label();
            tunnelGuideLabel.Location = new Point(20, 143);
            tunnelGuideLabel.Size = new Size(720, 72);
            tunnelGuideLabel.BorderStyle = BorderStyle.FixedSingle;
            tunnelGuideLabel.Padding = new Padding(8, 5, 8, 5);
            tunnelGuideLabel.ForeColor = Color.DimGray;
            Controls.Add(tunnelGuideLabel);

            hostnameBox = AddTextField("固定 hostname（不含 https://）", 224, current.FixedHostname);
            tunnelIdBox = AddTextField("Named Tunnel UUID 或名称", 288, current.NamedTunnelIdOrName);
            credentialsBox = AddTextField("credentials-file 路径（只保存路径，不读取内容）", 352, current.CredentialsFilePath);
            credentialsBrowseButton = AddBrowseButton(690, 374, delegate { BrowseFile(credentialsBox, "JSON files (*.json)|*.json|All files (*.*)|*.*"); });
            configBox = AddTextField("cloudflared config 路径", 416, current.CloudflaredConfigPath);
            configBrowseButton = AddBrowseButton(690, 438, delegate { BrowseFile(configBox, "YAML files (*.yml;*.yaml)|*.yml;*.yaml|All files (*.*)|*.*"); });

            autoStartBox = new CheckBox();
            autoStartBox.Text = "登录 Windows 后自动启动";
            autoStartBox.Location = new Point(20, 475);
            autoStartBox.Size = new Size(260, 24);
            autoStartBox.Checked = current.AutoStart;
            Controls.Add(autoStartBox);

            validationLabel = new Label();
            validationLabel.Location = new Point(20, 505);
            validationLabel.Size = new Size(720, 62);
            validationLabel.ForeColor = Color.Firebrick;
            Controls.Add(validationLabel);

            var saveButton = new Button();
            saveButton.Text = "保存";
            saveButton.Location = new Point(550, 586);
            saveButton.Size = new Size(90, 30);
            saveButton.Click += SaveClicked;
            Controls.Add(saveButton);

            var cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.Location = new Point(650, 586);
            cancelButton.Size = new Size(90, 30);
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
            UpdateNamedControls();
        }

        private void AddLabel(string text, int top)
        {
            AddLabel(text, top, 20);
        }

        private void AddLabel(string text, int top, int left)
        {
            var label = new Label();
            label.Text = text;
            label.Location = new Point(left, top);
            label.Size = new Size(220, 20);
            Controls.Add(label);
        }

        private TextBox AddTextField(string label, int top, string value)
        {
            AddLabel(label, top);
            var box = new TextBox();
            box.Location = new Point(20, top + 22);
            box.Size = new Size(660, 25);
            box.Text = value ?? string.Empty;
            Controls.Add(box);
            return box;
        }

        private Button AddBrowseButton(int left, int top, Action action)
        {
            var button = new Button();
            button.Text = "浏览";
            button.Location = new Point(left, top);
            button.Size = new Size(50, 25);
            button.Click += delegate { action(); };
            Controls.Add(button);
            return button;
        }

        private void BrowseFile(TextBox target, string filter)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = filter;
                dialog.CheckFileExists = true;
                if (File.Exists(target.Text))
                {
                    dialog.FileName = target.Text;
                }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    target.Text = dialog.FileName;
                }
            }
        }

        private void UpdateNamedControls()
        {
            var mode = Convert.ToString(tunnelModeBox.SelectedItem);
            var namedEnabled = string.Equals(
                mode,
                "Named",
                StringComparison.OrdinalIgnoreCase);
            var hostnameEnabled = namedEnabled || string.Equals(
                mode,
                "Service",
                StringComparison.OrdinalIgnoreCase);
            hostnameBox.Enabled = hostnameEnabled;
            tunnelIdBox.Enabled = namedEnabled;
            credentialsBox.Enabled = namedEnabled;
            configBox.Enabled = namedEnabled;
            credentialsBrowseButton.Enabled = namedEnabled;
            configBrowseButton.Enabled = namedEnabled;

            if (namedEnabled)
            {
                tunnelGuideLabel.Text = "Named：固定域名。必须同时具备 hostname、Tunnel UUID/名称、credentials JSON、cloudflared config YAML、cloudflared.exe 和 DevSpace 运行时；任一条件不满足都会阻止保存或启动。";
            }
            else if (string.Equals(mode, "Service", StringComparison.OrdinalIgnoreCase))
            {
                tunnelGuideLabel.Text = "Service：固定域名。必须填写 hostname，且 cloudflared Windows Service 必须已安装、可读取并处于 Running；Service 自身负责 UUID/credentials/ingress。任一条件不满足都不会启动 DevSpace。";
            }
            else if (string.Equals(mode, "Quick", StringComparison.OrdinalIgnoreCase))
            {
                tunnelGuideLabel.Text = "Quick：临时测试模式，不使用自有 UUID/固定域名。必须明确选择，并要求 cloudflared.exe 与 DevSpace 运行时完整；任一条件不满足都不会启动。地址重启后可能变化。";
            }
            else
            {
                tunnelGuideLabel.Text = "必须明确选择 Tunnel 模式。程序不会默认选择 Quick；当前模式所需条件未全部满足时，保存或启动会被强制阻止。";
            }
        }

        private void SaveClicked(object sender, EventArgs eventArgs)
        {
            var candidate = new AppSettings
            {
                SchemaVersion = 1,
                WorkspaceRoot = workspaceBox.Text.Trim(),
                ToolMode = Convert.ToString(toolModeBox.SelectedItem),
                LocalPort = Decimal.ToInt32(portBox.Value),
                TunnelMode = Convert.ToString(tunnelModeBox.SelectedItem),
                FixedHostname = hostnameBox.Text.Trim(),
                NamedTunnelIdOrName = tunnelIdBox.Text.Trim(),
                CredentialsFilePath = credentialsBox.Text.Trim(),
                CloudflaredConfigPath = configBox.Text.Trim(),
                AutoStart = autoStartBox.Checked
            };

            string error;
            if (!TunnelApplicationContext.ValidateSettings(candidate, out error))
            {
                validationLabel.Text = error;
                return;
            }

            if (!string.Equals(
                    initialToolMode,
                    candidate.ToolMode,
                    StringComparison.OrdinalIgnoreCase))
            {
                var codexTransition =
                    string.Equals(initialToolMode, "codex", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.ToolMode, "codex", StringComparison.OrdinalIgnoreCase);
                var warning = codexTransition
                    ? "切换 Tool mode 会更换 DevSpace 暴露给 ChatGPT 的 MCP 工具集合。\r\n\r\n" +
                      "minimal/full 使用 bash 等标准工具；codex 使用 apply_patch、exec_command、write_stdin，并隐藏原工具。\r\n\r\n" +
                      "已有 ChatGPT 连接或旧对话可能缓存之前的工具列表。保存并重启后，请刷新/重新连接 MCP；如果旧对话仍显示旧工具，请新建对话。无需重装。"
                    : "切换 Tool mode 会改变 DevSpace 暴露给 ChatGPT 的 MCP 工具集合。\r\n\r\n" +
                      "保存并重启后，建议刷新/重新连接 MCP；如果旧对话仍显示旧工具，请新建对话。无需重装。";
                var result = MessageBox.Show(
                    warning + "\r\n\r\n是否保存此 Tool mode 变更？",
                    "Tool mode 变更提示",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                {
                    return;
                }
            }
            Value = candidate;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class StatusForm : Form
    {
        private readonly Label statusLabel;
        private readonly Label detailLabel;
        private readonly TextBox tunnelUrlBox;
        private readonly TextBox mcpUrlBox;
        private readonly TextBox commandBox;
        private readonly TextBox ownerTokenBox;
        private readonly Action hideAction;
        private bool allowClose;

        public StatusForm(
            Action copyMcpUrl,
            Action copyCommand,
            Action copyOwnerToken,
            Action restartTunnel,
            Action hideWindow)
        {
            hideAction = hideWindow;
            Text = "DevSpace 远程连接";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            ClientSize = new Size(720, 425);
            Font = new Font("Microsoft YaHei UI", 9F);

            statusLabel = new Label();
            statusLabel.Location = new Point(20, 18);
            statusLabel.Size = new Size(680, 26);
            statusLabel.Font = new Font(Font, FontStyle.Bold);
            statusLabel.Text = "正在启动临时隧道…";

            detailLabel = new Label();
            detailLabel.Location = new Point(20, 45);
            detailLabel.Size = new Size(680, 36);
            detailLabel.ForeColor = Color.DimGray;

            tunnelUrlBox = AddReadOnlyField("公网地址", 88);
            mcpUrlBox = AddReadOnlyField("ChatGPT / MCP 中填写", 146);
            commandBox = AddReadOnlyField("更新 DevSpace 地址的命令", 204);
            ownerTokenBox = AddReadOnlyField("Owner password（已隐藏，请使用复制按钮）", 262);
            ownerTokenBox.UseSystemPasswordChar = true;

            var copyMcpButton = AddButton("复制 MCP 地址", 20, 328, 125);
            copyMcpButton.Click += delegate { copyMcpUrl(); };

            var copyCommandButton = AddButton("复制更新命令", 155, 328, 125);
            copyCommandButton.Click += delegate { copyCommand(); };

            var copyOwnerButton = AddButton("复制 Owner 密码", 290, 328, 140);
            copyOwnerButton.Click += delegate { copyOwnerToken(); };

            var restartButton = AddButton("重新启动全部", 440, 328, 120);
            restartButton.Click += delegate { restartTunnel(); };

            var hideButton = AddButton("隐藏到托盘", 570, 328, 130);
            hideButton.Click += delegate { hideAction(); };

            var note = new Label();
            note.Location = new Point(20, 373);
            note.Size = new Size(680, 42);
            note.ForeColor = Color.DimGray;
            note.Text = "程序会自动初始化并启动 DevSpace，只开放你在设置中选择的工作区。\r\n网页 ChatGPT 中仍需填写上面的 MCP 地址。";
            Controls.Add(note);
        }

        private TextBox AddReadOnlyField(string labelText, int top)
        {
            var label = new Label();
            label.Location = new Point(20, top);
            label.Size = new Size(680, 20);
            label.Text = labelText;
            Controls.Add(label);

            var box = new TextBox();
            box.Location = new Point(20, top + 22);
            box.Size = new Size(680, 25);
            box.ReadOnly = true;
            box.BackColor = Color.White;
            Controls.Add(box);
            return box;
        }

        private Button AddButton(string text, int left, int top, int width)
        {
            var button = new Button();
            button.Location = new Point(left, top);
            button.Size = new Size(width, 32);
            button.Text = text;
            Controls.Add(button);
            return button;
        }

        public void UpdateState(
            string status,
            string detail,
            string tunnelUrl,
            string backendStatus,
            string ownerToken,
            string cloudflareStatus)
        {
            statusLabel.Text = status;
            detailLabel.Text = detail +
                "\r\nDevSpace：" + backendStatus +
                "；Cloudflare：" + cloudflareStatus;
            tunnelUrlBox.Text = tunnelUrl;
            mcpUrlBox.Text = tunnelUrl.Length > 0 ? tunnelUrl + "/mcp" : "等待 Cloudflare 生成地址…";
            commandBox.Text = tunnelUrl.Length > 0
                ? "devspace.cmd config set publicBaseUrl \"" + tunnelUrl + "\""
                : "等待 Cloudflare 生成地址…";
            ownerTokenBox.Text = ownerToken;
        }

        protected override void OnFormClosing(FormClosingEventArgs eventArgs)
        {
            if (!allowClose && eventArgs.CloseReason == CloseReason.UserClosing)
            {
                eventArgs.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(eventArgs);
        }

        protected override void Dispose(bool disposing)
        {
            allowClose = true;
            base.Dispose(disposing);
        }
    }
}
