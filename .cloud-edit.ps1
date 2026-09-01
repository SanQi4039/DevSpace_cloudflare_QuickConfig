$ErrorActionPreference = 'Stop'

function Replace-Required([string]$Text, [string]$Old, [string]$New, [string]$Label) {
    if (-not $Text.Contains($Old)) { throw "Required source block not found: $Label" }
    return $Text.Replace($Old, $New)
}

$sourcePath = 'DevSpaceQuickTunnelTray.cs'
$source = Get-Content $sourcePath -Raw

$old = @'
        public static int Execute(string action)
'@
$new = @'
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
'@
$source = Replace-Required $source $old $new 'service preflight helpers'
$source = Replace-Required $source '                TunnelMode = "Quick",' '                TunnelMode = string.Empty,' 'no silent Quick default'
$source = Replace-Required $source '                error = "首次运行：请选择工作区根目录。";' '                error = "首次运行：请先完成工作区和 Cloudflare Tunnel 配置。所有必填条件满足前不会启动。";' 'first-run guidance'
$source = Replace-Required $source '                error = "Tunnel 模式必须是 Quick、Named 或 Service。";' '                error = "请选择 Tunnel 模式：Quick、Named 或 Service。程序不会默认选择。";' 'tunnel mode validation'

$old = @'
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
            return true;
'@
$new = @'
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
'@
$source = Replace-Required $source $old $new 'mode-specific settings validation'

$old = @'
        private void StartTunnel()
        {
            namedTunnelStartRequested = false;
'@
$new = @'
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
'@
$source = Replace-Required $source $old $new 'central startup preflight gate'

$source = Replace-Required $source '            ControlCloudflareService("start", "启动", false, null);' '            ControlCloudflareService("start", "启动", false, RestartTunnel);' 'service start resumes blocked startup'

$source = Replace-Required $source '        private readonly CheckBox autoStartBox;`r`n        private readonly Label validationLabel;' '        private readonly CheckBox autoStartBox;`r`n        private readonly Label tunnelGuideLabel;`r`n        private readonly Label validationLabel;' 'settings guide field'
if ($source -notmatch 'tunnelGuideLabel') {
    $source = Replace-Required $source "        private readonly CheckBox autoStartBox;`n        private readonly Label validationLabel;" "        private readonly CheckBox autoStartBox;`n        private readonly Label tunnelGuideLabel;`n        private readonly Label validationLabel;" 'settings guide field LF'
}

$source = Replace-Required $source '            ClientSize = new Size(760, 535);' '            ClientSize = new Size(760, 630);' 'settings dialog height'
$source = Replace-Required $source '            AddLabel("Tunnel 模式", 86, 440);' '            AddLabel("Tunnel 模式（必须选择）", 86, 440);' 'tunnel mode required label'

$old = @'
            tunnelModeBox.Items.AddRange(new object[] { "Quick", "Named", "Service" });
            tunnelModeBox.SelectedItem = current.IsServiceTunnel
                ? "Service"
                : (current.IsNamedTunnel ? "Named" : "Quick");
            tunnelModeBox.SelectedIndexChanged += delegate { UpdateNamedControls(); };
            Controls.Add(tunnelModeBox);

            hostnameBox = AddTextField("固定 hostname（不含 https://）", 154, current.FixedHostname);
            tunnelIdBox = AddTextField("Named Tunnel UUID 或名称", 218, current.NamedTunnelIdOrName);
            credentialsBox = AddTextField("credentials-file 路径（只保存路径，不读取内容）", 282, current.CredentialsFilePath);
            credentialsBrowseButton = AddBrowseButton(690, 304, delegate { BrowseFile(credentialsBox, "JSON files (*.json)|*.json|All files (*.*)|*.*"); });
            configBox = AddTextField("cloudflared config 路径", 346, current.CloudflaredConfigPath);
            configBrowseButton = AddBrowseButton(690, 368, delegate { BrowseFile(configBox, "YAML files (*.yml;*.yaml)|*.yml;*.yaml|All files (*.*)|*.*"); });
'@
$new = @'
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
'@
$source = Replace-Required $source $old $new 'settings explicit tunnel selection and guide'
$source = Replace-Required $source '            autoStartBox.Location = new Point(20, 405);' '            autoStartBox.Location = new Point(20, 475);' 'autostart position'
$source = Replace-Required $source '            validationLabel.Location = new Point(20, 435);' '            validationLabel.Location = new Point(20, 505);' 'validation position'
$source = Replace-Required $source '            validationLabel.Size = new Size(720, 40);' '            validationLabel.Size = new Size(720, 62);' 'validation size'
$source = Replace-Required $source '            saveButton.Location = new Point(550, 492);' '            saveButton.Location = new Point(550, 586);' 'save position'
$source = Replace-Required $source '            cancelButton.Location = new Point(650, 492);' '            cancelButton.Location = new Point(650, 586);' 'cancel position'

$old = @'
            configBox.Enabled = namedEnabled;
            credentialsBrowseButton.Enabled = namedEnabled;
            configBrowseButton.Enabled = namedEnabled;
        }
'@
$new = @'
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
'@
$source = Replace-Required $source $old $new 'mode guide strong gate text'
Set-Content $sourcePath $source -Encoding UTF8

$settingsPath = 'settings.example.json'
$settings = Get-Content $settingsPath -Raw
$settings = Replace-Required $settings '  "TunnelMode": "Quick",' '  "TunnelMode": "",' 'example requires explicit mode'
Set-Content $settingsPath $settings -Encoding UTF8

$readmePath = 'README.md'
$readme = Get-Content $readmePath -Raw
$old = @'
## First run

1. Start `DevSpaceQuickTunnelTray.exe`.
2. Select the workspace root that DevSpace is allowed to access.
3. Select `minimal`, `full`, or `codex` tool mode.
4. Leave Tunnel mode as `Quick` for the simplest setup, or configure Named/Service mode if you already operate a fixed Cloudflare Tunnel.
5. Copy the displayed MCP URL into ChatGPT.
6. Copy the Owner password when the MCP authorization page requests it.

Quick Tunnel URLs change after restart. Named/Service mode can keep a fixed hostname.
'@
$new = @'
## First run

1. Start `DevSpaceQuickTunnelTray.exe`.
2. Complete the settings dialog. A fresh install has no default Tunnel mode.
3. Select an existing workspace root, `minimal` / `full` / `codex` tool mode, and a valid local port.
4. Explicitly select `Quick`, `Named`, or `Service` and satisfy every requirement for that mode.
5. Only after the full preflight passes will the tray start DevSpace and Cloudflare connectivity.
6. Copy the displayed MCP URL into ChatGPT.
7. Copy the Owner password when the MCP authorization page requests it.

### Strict startup gate

Startup is fail-closed. The tray does not partially start DevSpace and then hope the remaining Cloudflare configuration works. If any global or mode-specific prerequisite is missing, the status window reports `启动已阻止` and no DevSpace/tunnel startup is attempted.

Global prerequisites for every mode:

- workspace root exists;
- tool mode and local port are valid;
- the DevSpace runtime is complete: Node.js and DevSpace CLI must both be available;
- a Tunnel mode was explicitly selected.

Mode-specific prerequisites:

| Mode | Required before startup | Notes |
| --- | --- | --- |
| `Quick` | `cloudflared.exe` plus all global prerequisites | Temporary test mode only. It deliberately has no user-owned Tunnel UUID or fixed hostname; Cloudflare generates a `trycloudflare.com` URL that can change after restart. |
| `Named` | fixed hostname, Tunnel UUID/name, existing credentials JSON, existing cloudflared YAML config, `cloudflared.exe`, plus all global prerequisites | Use this when the tray owns the fixed named tunnel process. Missing any field/file blocks saving or startup. |
| `Service` | fixed hostname, an installed/readable `cloudflared` Windows Service, and that service must be `Running`, plus all global prerequisites | The Windows Service owns its Tunnel UUID, credentials, and ingress/config. If the service is missing or stopped, DevSpace is not started. Starting the service from the tray re-runs the full startup gate. |

If you need a stable Cloudflare domain, use `Named` or `Service`; do not use `Quick`.

For a typical Named Tunnel, Cloudflare must provide a Tunnel UUID/name and a DNS hostname routed to that Tunnel. Enter the hostname without `https://`, select the credentials JSON file, and select a cloudflared YAML config whose ingress target points to the local DevSpace port, for example `http://127.0.0.1:7676`.
'@
$readme = Replace-Required $readme $old $new 'README first-run and strict gate'
Set-Content $readmePath $readme -Encoding UTF8
