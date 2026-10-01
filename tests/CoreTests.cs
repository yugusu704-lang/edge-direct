using System;
using EdgeDirect;
using Microsoft.Win32;

namespace EdgeDirect
{
    internal sealed class MemoryPolicy : IPolicyStore
    {
        public SavedValue Current = new SavedValue();
        public string ConflictMessage;
        public bool FailWrite;
        public bool DropWrite;
        public int Writes;
        public SavedValue Read() { return Current; }
        public void Write(SavedValue value) { Writes++; if (FailWrite) throw new Exception("write failed"); if (!DropWrite) Current = value; }
        public string Conflict() { return ConflictMessage; }
    }
    internal sealed class MemoryBackup : IBackupStore
    {
        public Backup Value;
        public bool FailSave;
        public bool FailDelete;
        public int Saves;
        public Backup Load() { return Value; }
        public void Save(Backup value) { Saves++; if (FailSave) throw new Exception("save failed"); Value = value; }
        public void Delete() { if (FailDelete) throw new Exception("delete failed"); Value = null; }
    }
    internal static class Tests
    {
        private static int count;
        private static void Assert(bool yes, string message) { if (!yes) throw new Exception(message); }
        private static void Throws(Action action)
        { bool thrown = false; try { action(); } catch { thrown = true; } Assert(thrown, "Expected an error"); }
        private static void Test(string name, Action test) { test(); count++; Console.WriteLine("PASS " + name); }
        [STAThread]
        public static int Main()
        {
            try
            {
                Test("enable and restore an initially absent policy", delegate {
                    var p = new MemoryPolicy(); var b = new MemoryBackup(); var c = new Controller(p,b);
                    c.Enable(); Assert(Controller.IsDirect(p.Current), "must enable direct"); Assert(!b.Value.Original.Exists,"backup absence");
                    c.Restore(); Assert(!p.Current.Exists && b.Value == null,"restore absence");
                });
                Test("preserve existing PAC policy exactly; repeated enable is idempotent", delegate {
                    var old = new SavedValue { Exists=true, Kind=1, Text="{\"ProxyMode\":\"pac_script\",\"ProxyPacUrl\":\"http://localhost:7688/pac\"}" };
                    var p = new MemoryPolicy {Current=old}; var b = new MemoryBackup(); var c = new Controller(p,b);
                    c.Enable(); c.Enable(); Assert(b.Saves==1,"must not replace backup"); c.Restore(); Assert(p.Current.SameAs(old),"exact restore");
                });
                Test("failed backup never writes browser policy", delegate {
                    var p = new MemoryPolicy(); var b = new MemoryBackup {FailSave=true}; var c = new Controller(p,b);
                    Throws(delegate { c.Enable(); }); Assert(p.Writes==0,"policy changed without backup");
                });
                Test("failed write retains recoverable original", delegate {
                    var p = new MemoryPolicy {FailWrite=true}; var b = new MemoryBackup(); var c = new Controller(p,b);
                    Throws(delegate { c.Enable(); }); Assert(b.Value!=null,"backup lost"); p.FailWrite=false; c.Enable(); c.Restore(); Assert(!p.Current.Exists,"bad recovery");
                });
                Test("unexpected policy replacement blocks enable and restore", delegate {
                    var p = new MemoryPolicy(); var b = new MemoryBackup(); var c = new Controller(p,b); c.Enable();
                    var replacement = new SavedValue {Exists=true,Kind=1,Text="{\"ProxyMode\":\"system\"}"}; p.Current=replacement;
                    Throws(delegate { c.Enable(); }); Throws(delegate { c.Restore(); }); Assert(p.Current==replacement && b.Value!=null,"new policy overwritten");
                });
                Test("machine policy conflict leaves user settings untouched", delegate {
                    var p = new MemoryPolicy {ConflictMessage="conflict"}; var b = new MemoryBackup(); var c = new Controller(p,b);
                    Throws(delegate { c.Enable(); }); Assert(p.Writes==0 && b.Value==null,"conflict overwritten");
                });
                Test("already direct external policy is not adopted or removed", delegate {
                    var old = new SavedValue {Exists=true,Kind=1,Text="{ \"ProxyMode\": \"direct\" }"};
                    var p = new MemoryPolicy {Current=old}; var b = new MemoryBackup(); var c = new Controller(p,b);
                    c.Enable(); c.Restore(); Assert(p.Current==old && b.Value==null,"external direct policy changed");
                });
                Test("write readback is required", delegate {
                    var p = new MemoryPolicy {DropWrite=true}; var b = new MemoryBackup(); var c = new Controller(p,b);
                    Throws(delegate { c.Enable(); }); Assert(b.Value!=null,"backup discarded after failed verification");
                });
                Test("failed restore retains backup for retry", delegate {
                    var p = new MemoryPolicy(); var b = new MemoryBackup(); var c = new Controller(p,b); c.Enable();
                    p.FailWrite=true; Throws(delegate { c.Restore(); }); Assert(b.Value!=null,"backup lost");
                    p.FailWrite=false; c.Restore(); Assert(!p.Current.Exists && b.Value==null,"retry failed");
                });
                Test("interrupted restore can finish after original was restored", delegate {
                    var p = new MemoryPolicy(); var b = new MemoryBackup(); var c = new Controller(p,b); c.Enable();
                    b.FailDelete=true; Throws(delegate { c.Restore(); }); Assert(!p.Current.Exists && b.Value!=null,"restore failed");
                    b.FailDelete=false; c.Restore(); Assert(b.Value==null,"retry cleanup failed");
                });
                Test("typed original values survive round trip", delegate {
                    var values = new[] {
                        new SavedValue{Exists=true,Kind=(int)RegistryValueKind.DWord,Number=-1},
                        new SavedValue{Exists=true,Kind=(int)RegistryValueKind.QWord,Number=long.MaxValue},
                        new SavedValue{Exists=true,Kind=(int)RegistryValueKind.ExpandString,Text="%LOCALAPPDATA%"},
                        new SavedValue{Exists=true,Kind=(int)RegistryValueKind.MultiString,Strings=new[]{"a","b"}},
                        new SavedValue{Exists=true,Kind=(int)RegistryValueKind.Binary,Binary="AAEC"}
                    };
                    foreach (var old in values) { var p=new MemoryPolicy{Current=old}; var b=new MemoryBackup(); var c=new Controller(p,b); old.Raw(); c.Enable(); c.Restore(); Assert(p.Current.SameAs(old),"type changed"); }
                });
                Test("malformed or non-direct JSON cannot be shown as direct", delegate {
                    foreach (string s in new[]{"not json", "null", "[]", "{}", "{\"ProxyMode\":true}", "{\"ProxyMode\":\"fixed_servers\"}"})
                        Assert(!Controller.IsDirect(new SavedValue{Exists=true,Kind=1,Text=s}),"false direct: " + s);
                    Assert(!Controller.IsDirect(new SavedValue()),"absent considered direct");
                });
                Test("already configured UI opens browser instead of reapplying policy",delegate {
                    var v=UiPresentation.From(new UiSnapshot{Direct=true,EdgeInstalled=true,BackupPresent=true},false,"refresh");
                    Assert(v.PrimaryAction=="open" && v.CanPrimary && v.CanVerify && v.CanRestore,"wrong ready actions");
                });
                Test("proxy port being down must not disable direct browser action",delegate {
                    var v=UiPresentation.From(new UiSnapshot{Direct=true,EdgeInstalled=true,PortReachable=false},false,"refresh");
                    Assert(v.PrimaryAction=="open" && v.CanPrimary,"proxy port incorrectly gates direct");
                });
                Test("conflict UI routes to policy inspection instead of enable",delegate {
                    var v=UiPresentation.From(new UiSnapshot{Conflict="machine policy",EdgeInstalled=true},false,"refresh");
                    Assert(v.PrimaryAction=="policy" && v.CanPrimary && !v.CanVerify,"conflicting setting shown ready");
                });
                Test("busy operation disables duplicate mutation and browser verification",delegate {
                    var v=UiPresentation.From(new UiSnapshot{Direct=true,EdgeInstalled=true,BackupPresent=true},true,"restore");
                    Assert(!v.CanPrimary && !v.CanRestore && !v.CanVerify,"busy action still clickable");
                });
                Test("missing or damaged backup cannot expose restore action",delegate {
                    Assert(!UiPresentation.From(new UiSnapshot{Direct=true,BackupPresent=false},false,"refresh").CanRestore,"missing backup can restore");
                    var v=UiPresentation.From(new UiSnapshot{BackupPresent=true,BackupError="broken backup"},false,"refresh");
                    Assert(!v.CanRestore && !v.CanPrimary,"damaged backup exposes write actions");
                });
                Test("failed status read offers retry instead of changing policy",delegate {
                    var v=UiPresentation.From(new UiSnapshot{Error="access denied",Direct=true,BackupPresent=true},false,"refresh");
                    Assert(v.PrimaryAction=="refresh" && v.CanPrimary && !v.CanRestore && !v.CanVerify,"error routes to write");
                });
                Console.WriteLine(count + " core/state tests passed."); NativeUiTests.Run();return 0;
            }
            catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
