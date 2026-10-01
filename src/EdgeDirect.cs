using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EdgeDirect
{
    // This is the only browser policy this application writes.
    public sealed class SavedValue
    {
        public bool Exists { get; set; }
        public int Kind { get; set; }
        public string Text { get; set; }
        public string[] Strings { get; set; }
        public string Binary { get; set; }
        public long Number { get; set; }

        public static SavedValue Read(RegistryKey key, string name)
        {
            if (key == null || !Array.Exists(key.GetValueNames(), delegate(string n) { return String.Equals(n, name, StringComparison.OrdinalIgnoreCase); })) return new SavedValue();
            var result = new SavedValue { Exists = true, Kind = (int)key.GetValueKind(name) };
            object value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            switch ((RegistryValueKind)result.Kind)
            {
                case RegistryValueKind.String: case RegistryValueKind.ExpandString: result.Text = (string)value; break;
                case RegistryValueKind.MultiString: result.Strings = (string[])value; break;
                case RegistryValueKind.DWord: result.Number = (int)value; break;
                case RegistryValueKind.QWord: result.Number = (long)value; break;
                case RegistryValueKind.Binary: case RegistryValueKind.None: result.Binary = Convert.ToBase64String((byte[])value); break;
                default: throw new InvalidOperationException("无法备份未知类型的 Edge 策略。");
            }
            return result;
        }

        public object Raw()
        {
            switch ((RegistryValueKind)Kind)
            {
                case RegistryValueKind.String: case RegistryValueKind.ExpandString: return Text;
                case RegistryValueKind.MultiString: return Strings;
                case RegistryValueKind.DWord: return checked((int)Number);
                case RegistryValueKind.QWord: return Number;
                case RegistryValueKind.Binary: case RegistryValueKind.None: return Convert.FromBase64String(Binary);
                default: throw new InvalidOperationException("备份中的注册表类型无效。");
            }
        }

        public bool SameAs(SavedValue other)
        {
            if (other == null || Exists != other.Exists) return false;
            if (!Exists) return true;
            return new JavaScriptSerializer().Serialize(this) == new JavaScriptSerializer().Serialize(other);
        }
    }

    public sealed class Backup
    {
        public int Version { get; set; }
        public string UserSid { get; set; }
        public string CreatedUtc { get; set; }
        public SavedValue Original { get; set; }
    }

    public interface IPolicyStore
    {
        SavedValue Read();
        void Write(SavedValue value);
        string Conflict();
    }

    public interface IBackupStore
    {
        Backup Load();
        void Save(Backup value);
        void Delete();
    }

    public sealed class PolicyStore : IPolicyStore
    {
        public const string KeyPath = @"Software\Policies\Microsoft\Edge";
        public const string ValueName = "ProxySettings";
        public SavedValue Read()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(KeyPath)) return SavedValue.Read(key, ValueName);
        }

        public void Write(SavedValue value)
        {
            if (value.Exists)
            {
                using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    key.SetValue(ValueName, value.Raw(), (RegistryValueKind)value.Kind);
                    key.Flush();
                }
            }
            else
            {
                using (var key = Registry.CurrentUser.OpenSubKey(KeyPath, true))
                    if (key != null) { key.DeleteValue(ValueName, false); key.Flush(); }
            }
        }

        public string Conflict()
        {
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = root.OpenSubKey(KeyPath))
                {
                    if (key != null)
                    {
                        foreach (string name in new[] { "ProxySettings", "ProxyMode", "ProxyServer", "ProxyPacUrl", "ProxyBypassList", "ProxyOverrideRules" })
                            if (key.GetValue(name) != null)
                                return "检测到计算机级 Edge 代理策略（" + name + "）。它可能优先于当前用户设置，请在 edge://policy 核实；程序不会覆盖此策略。";
                    }
                }
            }
            using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
                if (key != null)
                    using (var rules = key.OpenSubKey("ProxyOverrideRules"))
                        if (key.GetValue("ProxyOverrideRules") != null || rules != null)
                            return "检测到 Edge 代理覆盖规则 ProxyOverrideRules，请先在 edge://policy 核实其来源。";
            return null;
        }
    }

    public sealed class BackupStore : IBackupStore
    {
        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdgeDirect");
        public static readonly string FilePath = Path.Combine(Folder, "policy-backup.json");
        private static string Sid { get { return System.Security.Principal.WindowsIdentity.GetCurrent().User.Value; } }

        public Backup Load()
        {
            if (!File.Exists(FilePath)) return null;
            Backup value;
            try { value = new JavaScriptSerializer().Deserialize<Backup>(File.ReadAllText(FilePath, Encoding.UTF8)); }
            catch (Exception ex) { throw new InvalidOperationException("无法读取原设置备份；为保护恢复能力，未修改策略。备份：" + FilePath, ex); }
            if (value == null || value.Version != 1 || value.Original == null || value.UserSid != Sid)
                throw new InvalidOperationException("原设置备份格式或用户不匹配；未修改策略。备份：" + FilePath);
            if (value.Original.Exists) value.Original.Raw();
            return value;
        }

        public void Save(Backup value)
        {
            value.UserSid = Sid;
            Directory.CreateDirectory(Folder);
            string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(value));
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                // No overwrite: a second enable must never replace the original snapshot.
                File.Move(temporary, FilePath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public void Delete() { File.Delete(FilePath); }
    }

    public sealed class Controller
    {
        public const string DirectJson = "{\"ProxyMode\":\"direct\"}";
        private readonly IPolicyStore policy;
        private readonly IBackupStore backup;
        public Controller(IPolicyStore policy, IBackupStore backup) { this.policy = policy; this.backup = backup; }
        public static SavedValue DirectValue() { return new SavedValue { Exists = true, Kind = (int)RegistryValueKind.String, Text = DirectJson }; }
        public static bool IsDirect(SavedValue value)
        {
            if (value == null || !value.Exists || value.Kind != (int)RegistryValueKind.String) return false;
            try
            {
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(value.Text);
                object mode;
                return data != null && data.TryGetValue("ProxyMode", out mode) && mode is string && (string)mode == "direct";
            }
            catch { return false; }
        }

        public string Enable()
        {
            string conflict = policy.Conflict();
            if (conflict != null) throw new InvalidOperationException(conflict);
            Backup saved = backup.Load();
            SavedValue current = policy.Read();
            if (saved != null && !current.SameAs(DirectValue()) && !current.SameAs(saved.Original))
                throw new InvalidOperationException("Edge 代理策略在上次启用后被其他软件修改。为避免覆盖新设置，已停止；请查看 edge://policy。");
            if (saved == null)
            {
                if (IsDirect(current)) return "Edge 已有始终直连策略，无需重复设置。";
                backup.Save(new Backup { Version = 1, CreatedUtc = DateTime.UtcNow.ToString("o"), Original = current });
            }
            policy.Write(DirectValue());
            if (!policy.Read().SameAs(DirectValue())) throw new IOException("策略写入后校验失败，原设置备份已保留。");
            return "已设置 Edge 始终直连。请保存网页后完整退出并重新打开 Edge，或在 edge://policy 中重新加载策略。";
        }

        public string Restore()
        {
            Backup saved = backup.Load();
            if (saved == null) return "没有本程序保存的原设置，无需恢复。";
            SavedValue current = policy.Read();
            if (!current.SameAs(DirectValue()) && !current.SameAs(saved.Original))
                throw new InvalidOperationException("Edge 策略已被其他软件修改。为避免覆盖新设置，已停止恢复；原备份仍保留。");
            if (!current.SameAs(saved.Original)) policy.Write(saved.Original);
            if (!policy.Read().SameAs(saved.Original)) throw new IOException("恢复校验失败，原设置备份已保留。");
            backup.Delete();
            return "已恢复启用前的 Edge 代理设置。重新加载策略或重启 Edge 后生效。";
        }
    }

    public static class Browser
    {
        public static string Locate()
        {
            var candidates = new List<string>();
            foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
                using (var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe"))
                    if (key != null && key.GetValue("") is string) candidates.Add(((string)key.GetValue("")).Trim('"'));
            foreach (string folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
                if (!String.IsNullOrEmpty(folder)) candidates.Add(Path.Combine(folder, @"Microsoft\Edge\Application\msedge.exe"));
            foreach (string candidate in candidates) if (File.Exists(candidate)) return candidate;
            return null;
        }

        public static void Open(string url)
        {
            string executable = Locate();
            if (executable == null) throw new FileNotFoundException("未找到 Microsoft Edge，请先安装 Edge。");
            // Existing profiles are retained. Enforcement comes from policy, not a flag
            // that would silently be discarded by an already running Edge process.
            Process.Start(new ProcessStartInfo(executable, "--new-window " + url) { UseShellExecute = true });
        }

        public static bool PortOpen(int port)
        {
            using (var client = new TcpClient())
            {
                try
                {
                    var pending = client.BeginConnect("127.0.0.1", port, null, null);
                    using (pending.AsyncWaitHandle)
                    {
                        if (!pending.AsyncWaitHandle.WaitOne(550)) return false;
                        client.EndConnect(pending);
                        return client.Connected;
                    }
                }
                catch (SocketException) { return false; }
            }
        }
    }

    internal static class Program
    {
        public static string Mutate(string action)
        {
            try { return MutateCore(action); }
            catch (UnauthorizedAccessException) { return Elevate(action); }
            catch (System.Security.SecurityException) { return Elevate(action); }
        }

        private static string Elevate(string action)
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            if (new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                throw new UnauthorizedAccessException("管理员权限仍无法写入 Edge 策略，请检查该电脑的组织管理设置。");
            Directory.CreateDirectory(BackupStore.Folder);
            string response = Path.Combine(BackupStore.Folder, "operation-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                var start = new ProcessStartInfo(Application.ExecutablePath,
                    "--" + action + " --elevated --expected-sid " + identity.User.Value + " --result \"" + response + "\"")
                { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
                using (var process = Process.Start(start))
                {
                    process.WaitForExit();
                    string result = File.Exists(response) ? File.ReadAllText(response, Encoding.UTF8) : "管理员操作未返回结果，请刷新状态后重试。";
                    if (process.ExitCode != 0) throw new InvalidOperationException(result);
                    return result;
                }
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223) throw new InvalidOperationException("已取消管理员授权。本次策略操作未执行，可稍后重试。");
                throw;
            }
            finally { if (File.Exists(response)) File.Delete(response); }
        }

        private static string MutateCore(string action)
        {
            string sid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            using (var mutex = new Mutex(false, @"Local\EdgeDirect.Policy." + sid))
            {
                bool entered;
                try { entered = mutex.WaitOne(5000); } catch (AbandonedMutexException) { entered = true; }
                if (!entered) throw new IOException("另一个直连助手正在保存设置，请稍后重试。");
                try
                {
                    var controller = new Controller(new PolicyStore(), new BackupStore());
                    return action == "enable" ? controller.Enable() : controller.Restore();
                }
                finally { mutex.ReleaseMutex(); }
            }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length == 0) { Application.Run(new MainForm()); return 0; }
                if (args[0] == "--enable" || args[0] == "--restore")
                {
                    bool elevated = Array.IndexOf(args, "--elevated") >= 0;
                    if (elevated)
                    {
                        int sidIndex = Array.IndexOf(args, "--expected-sid");
                        string currentSid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
                        if (sidIndex < 0 || sidIndex + 1 >= args.Length || args[sidIndex + 1] != currentSid)
                            throw new InvalidOperationException("管理员授权使用了另一个 Windows 账户。为避免改错用户，已停止；请使用当前账户的管理员授权。");
                    }
                    Report(args, elevated ? MutateCore(args[0].Substring(2)) : Mutate(args[0].Substring(2)));
                    return 0;
                }
                if (args[0] == "--status")
                {
                    var store = new PolicyStore();
                    Report(args, new JavaScriptSerializer().Serialize(new { directConfigured = Controller.IsDirect(store.Read()), conflict = store.Conflict(), edgePath = Browser.Locate(), proxyPort7688Reachable = Browser.PortOpen(7688), backupPresent = File.Exists(BackupStore.FilePath), backupPath = BackupStore.FilePath }));
                    return 0;
                }
                if (args[0] == "--render" && (args.Length == 2 || args.Length == 3))
                {
                    using (var form = new MainForm())
                    {
                        form.ShowInTaskbar = false;
                        form.SuppressInitialRefresh();
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(-32000, -32000);
                        form.Show();
                        if(args.Length==3) form.Preview(args[2]);
                        var task = args.Length==3 ? Task.FromResult(true) : form.RefreshStatus();
                        while (!task.IsCompleted) { Application.DoEvents(); Thread.Sleep(10); }
                        task.GetAwaiter().GetResult();
                        using (var bitmap = new Bitmap(form.Width, form.Height))
                        { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png); }
                        form.Close();
                    }
                    return 0;
                }
                throw new ArgumentException("用法：EdgeDirect.exe [--enable|--restore|--status] [--result 文件路径]");
            }
            catch (Exception ex)
            {
                Report(args, "ERROR: " + ex.Message);
                return 1;
            }
        }

        private static void Report(string[] args, string text)
        {
            int resultIndex = Array.IndexOf(args, "--result");
            if (resultIndex >= 0 && resultIndex + 1 < args.Length)
                File.WriteAllText(Path.GetFullPath(args[resultIndex + 1]), text, new UTF8Encoding(false));
            else if (args.Length == 0) MessageBox.Show(text, "Edge 直连助手");
            else Console.WriteLine(text);
        }
    }
}
