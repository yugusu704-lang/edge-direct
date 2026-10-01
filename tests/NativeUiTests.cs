using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdgeDirect
{
    internal static class NativeUiTests
    {
        private static void Assert(bool yes,string message) {if(!yes) throw new Exception(message);}
        private static void Pump(Func<bool> done)
        {
            DateTime deadline=DateTime.UtcNow.AddSeconds(8);
            while(!done()) {if(DateTime.UtcNow>deadline) throw new Exception("Native UI test timed out");Application.DoEvents();Thread.Sleep(5);}
            Application.DoEvents();
        }
        private static void Wait(Task task) {Pump(delegate {return task.IsCompleted;});task.GetAwaiter().GetResult();}
        private static Control Find(Control root,Func<Control,bool> match)
        {
            if(match(root)) return root;
            foreach(Control c in root.Controls) {var found=Find(c,match);if(found!=null) return found;}
            return null;
        }
        private static Button GetButton(Control root,string prefix)
        {return (Button)Find(root,delegate(Control c) {return c is Button && c.Text.StartsWith(prefix,StringComparison.Ordinal);});}
        private static bool Has(Control root,string text)
        {return Find(root,delegate(Control c) {return c is Label && c.Text.Contains(text);})!=null;}
        private static MainForm Form(Func<UiSnapshot> read,Func<string,string> mutate,Action<string> open)
        {
            var form=new MainForm(read,mutate,open);form.SuppressInitialRefresh();form.StartPosition=FormStartPosition.Manual;
            form.Location=new Point(-32000,-32000);form.ShowInTaskbar=false;form.Opacity=0;form.Show();
            Wait(form.RefreshStatus());return form;
        }
        public static void Run()
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Exception failure=null;
            using(var host=new System.Windows.Forms.Form())
            {
                host.StartPosition=FormStartPosition.Manual;host.Location=new Point(-32000,-32000);host.ShowInTaskbar=false;host.Opacity=0;
                host.Shown+=delegate {
                    try {RunFixtures();} catch(Exception ex) {failure=ex;Console.Error.WriteLine("Native UI failure: "+ex.GetType().Name+": "+ex.Message);}
                    finally {host.Close();}
                };
                Application.Run(host);
            }
            if(failure!=null) throw new InvalidOperationException("Native interaction checks failed",failure);
        }
        private static void RunFixtures()
        {
            int count=0;
            using(var form=Form(delegate {return new UiSnapshot{Direct=true,BackupPresent=true,EdgeInstalled=true};},delegate(string a) {throw new Exception("Unexpected policy mutation");},delegate(string url) {Assert(url=="edge://newtab","wrong browser URL");count++;}))
            {
                GetButton(form,"打开 Edge").PerformClick();Assert(count==1,"browser button did not dispatch exactly once");
                GetButton(form,"02").PerformClick();Assert(Has(form,"127.0.0.1:7688"),"diagnostic navigation failed");
                GetButton(form,"03").PerformClick();Assert(Has(form,"三步"),"help navigation failed");
                GetButton(form,"01").PerformClick();Assert(GetButton(form,"打开 Edge")!=null,"return navigation failed");
                form.Close();
            }
            Console.WriteLine("PASS native buttons navigate and open Edge without rewriting policy");

            bool direct=false;int writes=0,reads=0;
            using(var gate=new ManualResetEvent(false))
            using(var form=Form(delegate {reads++;return new UiSnapshot{Direct=direct,BackupPresent=direct,EdgeInstalled=true};},delegate(string action) {Interlocked.Increment(ref writes);gate.WaitOne();direct=true;return "saved";},delegate(string url) {}))
            {
                GetButton(form,"开启始终直连").PerformClick();Pump(delegate {return writes==1;});
                Assert(!GetButton(form,"正在开启").Enabled,"busy primary was enabled");
                int before=reads;GetButton(form,"刷新状态").PerformClick();Assert(reads==before,"refresh raced with policy operation");
                GetButton(form,"正在开启").PerformClick();Assert(writes==1,"duplicate operation was dispatched");
                // Page changes remain responsive during an operation.
                GetButton(form,"03").PerformClick();Assert(Has(form,"三步"),"UI blocked during policy operation");
                gate.Set();Pump(delegate {return GetButton(form,"刷新状态").Enabled;});
                GetButton(form,"01").PerformClick();Assert(GetButton(form,"打开 Edge").Enabled,"primary did not advance after enable");
                Assert(Has(form,"直连设置已保存"),"success feedback missing");form.Close();
            }
            Console.WriteLine("PASS native enable prevents duplicate clicks and concurrent refresh while navigation stays responsive");

            using(var form=Form(delegate {return new UiSnapshot{EdgeInstalled=true};},delegate(string action) {throw new InvalidOperationException("已取消管理员授权。本次操作未执行。");},delegate(string url) {}))
            {
                GetButton(form,"开启始终直连").PerformClick();Pump(delegate {return GetButton(form,"刷新状态").Enabled;});
                Assert(Has(form,"授权已取消"),"cancellation feedback missing");Assert(GetButton(form,"开启始终直连").Enabled,"cancel did not allow retry");
                GetButton(form,"02").PerformClick();Assert(GetButton(form,"查看详细原因").Enabled,"full failure details were lost");form.Close();
            }
            Console.WriteLine("PASS native authorization cancellation keeps full details and permits retry");

            int restores=0;
            using(var form=Form(delegate {return new UiSnapshot{Direct=true,BackupPresent=true,EdgeInstalled=true};},delegate(string action) {restores++;return "restored";},delegate(string url) {}))
            {
                bool cancelled=false;
                form.BeginInvoke(new Action(delegate {
                    foreach(var dialog in form.OwnedForms) if(dialog is PopDialog) {GetButton(dialog,"先保留直连").PerformClick();cancelled=true;}
                }));
                GetButton(form,"恢复原设置").PerformClick();
                Assert(cancelled && restores==0,"cancelled restore modified settings");Assert(GetButton(form,"打开 Edge").Enabled,"restore cancel changed ready state");form.Close();
            }
            Console.WriteLine("PASS native restore confirmation cancel preserves direct settings");

            bool restored=false;int restoreWrites=0;
            using(var form=Form(delegate {return new UiSnapshot{Direct=!restored,BackupPresent=!restored,EdgeInstalled=true};},delegate(string action) {Assert(action=="restore","wrong restore command");restoreWrites++;restored=true;return "restored";},delegate(string url) {}))
            {
                form.BeginInvoke(new Action(delegate {
                    foreach(var dialog in form.OwnedForms) if(dialog is PopDialog) GetButton(dialog,"恢复原设置").PerformClick();
                }));
                GetButton(form,"恢复原设置").PerformClick();Pump(delegate {return GetButton(form,"刷新状态").Enabled;});
                Assert(restoreWrites==1 && Has(form,"原设置已恢复"),"confirmed restore did not finish once");
                Assert(!GetButton(form,"恢复原设置").Enabled && GetButton(form,"开启始终直连").Enabled,"restore did not return to initial state");form.Close();
            }
            Console.WriteLine("PASS native confirmed restore returns to enable state and clears restore action");

            string policyUrl=null;
            using(var form=Form(delegate {return new UiSnapshot{Direct=true,BackupPresent=true,EdgeInstalled=true};},delegate(string action) {throw new Exception("Verification must not write policy");},delegate(string url) {policyUrl=url;}))
            {
                form.BeginInvoke(new Action(delegate {
                    foreach(var dialog in form.OwnedForms) if(dialog is PopDialog) GetButton(dialog,"打开策略页").PerformClick();
                }));
                GetButton(form,"确认生效").PerformClick();Assert(policyUrl=="edge://policy","verification did not open real policy page");
                Assert(Has(form,"下一步"),"verification instruction feedback missing");form.Close();
            }
            Console.WriteLine("PASS native verification opens policy page with instructions and no policy mutation");

            using(var form=Form(delegate {throw new Exception("Simulated registry read failure");},delegate(string action) {throw new Exception("Unexpected mutation after read failure");},delegate(string url) {}))
            {
                Assert(GetButton(form,"重新检查").Enabled,"read failure did not expose retry");Assert(!GetButton(form,"恢复原设置").Enabled,"read failure exposed restore");
                Assert(Has(form,"状态读取未完成"),"read failure feedback missing");form.Close();
            }
            Console.WriteLine("PASS native read failure keeps write actions unavailable and provides retry");
            Console.WriteLine("7 native interaction tests passed.");
        }
    }
}
