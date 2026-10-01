using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EdgeDirect
{
    internal static class Pop
    {
        public static readonly Color Paper = Color.FromArgb(246, 243, 235);
        public static readonly Color White = Color.FromArgb(255, 253, 247);
        public static readonly Color Ink = Color.FromArgb(49, 54, 49);
        public static readonly Color Muted = Color.FromArgb(100, 105, 96);
        public static readonly Color Line = Color.FromArgb(83, 88, 78);
        public static readonly Color Sage = Color.FromArgb(211, 222, 196);
        public static readonly Color Peach = Color.FromArgb(231, 192, 174);
        public static readonly Color Butter = Color.FromArgb(236, 219, 165);
        public static readonly Color Lavender = Color.FromArgb(218, 208, 231);
        public static readonly Color Blue = Color.FromArgb(209, 225, 228);
        public static readonly Color Green = Color.FromArgb(58, 91, 65);
        public static readonly Color Red = Color.FromArgb(146, 69, 57);
        public static Font Font(float size, bool bold) { return new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular); }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var path = new GraphicsPath(); float d = radius * 2;
            path.AddArc(r.X,r.Y,d,d,180,90); path.AddArc(r.Right-d,r.Y,d,d,270,90);
            path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); path.AddArc(r.X,r.Bottom-d,d,d,90,90); path.CloseFigure();
            return path;
        }

        public static void Dots(Graphics g, Rectangle r, Color color, int step, int radius)
        {
            using(var brush = new SolidBrush(color))
                for(int y=r.Y;y<r.Bottom;y+=step) for(int x=r.X;x<r.Right;x+=step) g.FillEllipse(brush,x,y,radius,radius);
        }

        public static void Arrow(Graphics g, PointF from, PointF to, Color color, float width)
        {
            using(var pen = new Pen(color,width))
            {
                pen.StartCap=LineCap.Round; pen.EndCap=LineCap.Round;
                g.DrawLine(pen,from,to);
                double a=Math.Atan2(to.Y-from.Y,to.X-from.X); float length=8;
                g.DrawLine(pen,to,new PointF(to.X-length*(float)Math.Cos(a-.55),to.Y-length*(float)Math.Sin(a-.55)));
                g.DrawLine(pen,to,new PointF(to.X-length*(float)Math.Cos(a+.55),to.Y-length*(float)Math.Sin(a+.55)));
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        public static void Caption(Form form)
        {
            try { int color=Paper.R | (Paper.G<<8) | (Paper.B<<16); DwmSetWindowAttribute(form.Handle,35,ref color,4); }
            catch(DllNotFoundException) { } catch(EntryPointNotFoundException) { }
        }
    }

    internal class PopPanel : Panel
    {
        public Color Fill=Pop.White;
        public bool Shadow=true;
        public bool Outline=true;
        public PopPanel()
        {
            BackColor=Pop.Paper;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float scale=e.Graphics.DpiX/96F; float offset=Shadow ? 4*scale : 0;
            var rect=new RectangleF(1*scale,1*scale,Width-2*scale-offset,Height-2*scale-offset);
            if(Shadow) using(var shape=Pop.Round(new RectangleF(rect.X+offset,rect.Y+offset,rect.Width,rect.Height),13*scale))
                using(var brush=new SolidBrush(Pop.Line)) e.Graphics.FillPath(brush,shape);
            using(var shape=Pop.Round(rect,13*scale))
            {
                using(var brush=new SolidBrush(Fill)) e.Graphics.FillPath(brush,shape);
                if(Outline) using(var pen=new Pen(Pop.Line,1.3F*scale)) e.Graphics.DrawPath(pen,shape);
            }
        }
    }

    internal sealed class PopButton : Button
    {
        public Color Fill=Pop.White;
        public bool Shadow=true;
        private bool hover,pressed;
        public PopButton()
        {
            FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0; BackColor=Pop.Paper;
            Font=Pop.Font(10,true); ForeColor=Pop.Ink; Cursor=Cursors.Hand;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            AccessibleRole=AccessibleRole.PushButton;
        }
        protected override void OnMouseEnter(EventArgs e) { hover=true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover=false; pressed=false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed=e.Button==MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed=false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if(e.KeyCode==Keys.Space) { pressed=true;Invalidate(); } base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { pressed=false;Invalidate();base.OnKeyUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { hover=false;pressed=false;Invalidate();base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float s=e.Graphics.DpiX/96F; float shift=pressed && Enabled ? 2*s : 0; float shadow=Shadow ? 3*s : 0;
            var r=new RectangleF(1*s+shift,1*s+shift,Width-2*s-shadow,Height-2*s-shadow);
            if(Shadow && Enabled) using(var shape=Pop.Round(new RectangleF(s+shadow,s+shadow,r.Width,r.Height),9*s))
                using(var brush=new SolidBrush(Pop.Line)) e.Graphics.FillPath(brush,shape);
            Color fill=Enabled ? Fill : Color.FromArgb(232,232,221);
            if(hover && Enabled) fill=ControlPaint.Light(fill,.12F);
            using(var shape=Pop.Round(r,9*s))
            {
                using(var brush=new SolidBrush(fill)) e.Graphics.FillPath(brush,shape);
                using(var pen=new Pen(Enabled ? Pop.Line : Color.FromArgb(167,172,160),1.3F*s)) e.Graphics.DrawPath(pen,shape);
            }
            var textRect=Rectangle.Round(r);textRect.Inflate(-(int)(8*s),0);
            TextRenderer.DrawText(e.Graphics,Text,Font,textRect,Enabled ? ForeColor : Pop.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            if(Focused && ShowFocusCues) { textRect.Inflate(-2,-7);ControlPaint.DrawFocusRectangle(e.Graphics,textRect,ForeColor,fill); }
        }
    }

    internal sealed class RouteArt : Control
    {
        public bool Compact;
        public bool Brand;
        public RouteArt() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;TabStop=false; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            float s=e.Graphics.DpiX/96F;g.ScaleTransform(s,s);
            if(Brand)
            {
                using(var brush=new SolidBrush(Pop.Blue)) g.FillEllipse(brush,2,2,38,38);
                using(var pen=new Pen(Pop.Line,2)) g.DrawEllipse(pen,2,2,38,38);
                using(var brush=new SolidBrush(Pop.Ink)) using(var font=new Font("Segoe UI",28,FontStyle.Bold,GraphicsUnit.Pixel)) g.DrawString("e",font,brush,9,2);
                Pop.Arrow(g,new PointF(55,21),new PointF(99,21),Pop.Ink,2);
                Pop.Dots(g,new Rectangle(113,6,15,28),Pop.Muted,8,2);
            }
            else if(Compact)
            {
                using(var pen=new Pen(Pop.Line,2))
                {
                    using(var b=new SolidBrush(Pop.Blue)) g.FillEllipse(b,5,5,35,35);
                    using(var b=new SolidBrush(Pop.Butter)) g.FillEllipse(b,5,59,35,35);
                    g.DrawEllipse(pen,5,5,35,35);g.DrawEllipse(pen,5,59,35,35);
                }
                using(var font=new Font("Segoe UI",23,FontStyle.Bold,GraphicsUnit.Pixel)) using(var brush=new SolidBrush(Pop.Ink))
                { g.DrawString("e",font,brush,13,6);g.DrawString("C",font,brush,10,60); }
                Pop.Arrow(g,new PointF(110,23),new PointF(169,23),Pop.Green,2);
                Pop.Arrow(g,new PointF(110,77),new PointF(169,77),Pop.Line,2);
            }
            else
            {
                Pop.Dots(g,new Rectangle(119,3,74,58),Color.FromArgb(139,157,130),10,2);
                using(var pen=new Pen(Pop.Line,2))
                {
                    using(var b=new SolidBrush(Pop.White)) g.FillEllipse(b,22,35,109,109);
                    g.DrawEllipse(pen,22,35,109,109);
                    using(var b=new SolidBrush(Pop.Blue)) g.FillEllipse(b,38,51,77,77);
                    g.DrawEllipse(pen,38,51,77,77);
                    Pop.Arrow(g,new PointF(92,89),new PointF(177,89),Pop.Ink,3);
                    g.DrawArc(pen,144,111,36,36,25,285);
                }
                using(var font=new Font("Segoe UI",54,FontStyle.Bold,GraphicsUnit.Pixel)) using(var brush=new SolidBrush(Pop.Ink)) g.DrawString("e",font,brush,53,49);
                using(var b=new SolidBrush(Pop.Peach)) g.FillEllipse(b,15,23,21,21);
                using(var pen=new Pen(Pop.Line,2)) g.DrawEllipse(pen,15,23,21,21);
                using(var font=new Font("Segoe UI",12,FontStyle.Bold,GraphicsUnit.Pixel)) using(var brush=new SolidBrush(Pop.Ink)) g.DrawString("DIRECT",font,brush,69,159);
            }
        }
    }

    internal sealed class UiSnapshot
    {
        public bool Direct,BackupPresent,PortReachable,ManualProxy,Pac,EdgeInstalled;
        public string Conflict,Error,BackupError;
        public DateTime CheckedAt;
        public static UiSnapshot Read()
        {
            var value=new UiSnapshot {CheckedAt=DateTime.Now};
            try
            {
                var policy=new PolicyStore();value.Direct=Controller.IsDirect(policy.Read());value.Conflict=policy.Conflict();
                value.EdgeInstalled=Browser.Locate()!=null;
                try { value.BackupPresent=new BackupStore().Load()!=null; } catch(Exception ex) { value.BackupError=ex.Message; }
                using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    value.ManualProxy=key!=null && Convert.ToInt32(key.GetValue("ProxyEnable",0))!=0;
                    value.Pac=key!=null && !String.IsNullOrEmpty(key.GetValue("AutoConfigURL") as string);
                }
                value.PortReachable=Browser.PortOpen(7688);
            }
            catch(Exception ex) { value.Error=ex.Message; }
            return value;
        }
    }

    internal sealed class UiPresentation
    {
        public string Title,Description,Tag,Primary,PrimaryAction;
        public bool CanPrimary,CanRestore,CanVerify;
        public Color Hero;
        public static UiPresentation From(UiSnapshot state,bool busy,string operation)
        {
            var view=new UiPresentation {Hero=Pop.Sage};
            if(state==null)
            { view.Title="正在整理网络状态…";view.Description="马上为你检查 Edge 设置和本地代理。";view.Tag="检查中";view.Primary="正在读取…";return view; }
            if(state.Error!=null)
            { view.Title="状态暂时读不到。";view.Description="重新检查一次，或在诊断页查看原因。";view.Tag="需要检查";view.Hero=Pop.Peach;view.Primary="重新检查";view.PrimaryAction="refresh"; }
            else if(state.Conflict!=null)
            { view.Title="这条路线，需要确认。";view.Description="发现其他 Edge 代理策略，请先查看生效来源。";view.Tag="发现策略冲突";view.Hero=Pop.Butter;view.Primary="查看 Edge 策略";view.PrimaryAction="policy"; }
            else if(state.Direct)
            { view.Title="Edge，走直连。";view.Description="直连策略已配置；在浏览器里确认生效即可。";view.Tag="直连设置已开启";view.Primary="打开 Edge  ↗";view.PrimaryAction="open"; }
            else
            { view.Title="让 Edge，走自己的路。";view.Description="开启始终直连，避免继续连接已经关闭的代理。";view.Tag="直连尚未开启";view.Hero=Pop.Blue;view.Primary="开启始终直连";view.PrimaryAction="enable"; }
            view.CanPrimary=!busy && (view.PrimaryAction=="refresh" || view.PrimaryAction=="enable" && state.BackupError==null || (view.PrimaryAction=="open" || view.PrimaryAction=="policy") && state.EdgeInstalled);
            view.CanRestore=!busy && state.Error==null && state.BackupPresent && state.BackupError==null;
            view.CanVerify=!busy && state.Error==null && state.Conflict==null && state.Direct && state.EdgeInstalled;
            if(busy)
            { view.Tag=operation=="enable" ? "正在保存设置" : operation=="restore" ? "正在恢复设置" : "检查中";view.Primary=operation=="enable" ? "正在开启…" : operation=="restore" ? "正在恢复…" : "正在检查…"; }
            return view;
        }
    }

    public sealed class MainForm : Form
    {
        private readonly Func<UiSnapshot> read;
        private readonly Func<string,string> mutate;
        private readonly Action<string> openBrowser;
        private UiSnapshot snapshot;
        private bool busy,loaded;
        private string operation="refresh",page="overview",feedbackTitle="一次设置，持续生效。",feedbackText="启用后可以关闭助手，照常从原来的入口打开 Edge。";
        private bool feedbackError;
        private string operationError;
        private readonly Dictionary<string,PopButton> navigation=new Dictionary<string,PopButton>();
        private readonly Panel content;
        private readonly PopButton refresh,restore;
        private readonly Label checkedAt;
        private PopButton primary,verify;
        private readonly ToolTip tips=new ToolTip {AutoPopDelay=8000,InitialDelay=350,ReshowDelay=100};

        public MainForm() : this(UiSnapshot.Read,Program.Mutate,Browser.Open) { }
        internal MainForm(Func<UiSnapshot> read,Func<string,string> mutate,Action<string> openBrowser)
        {
            this.read=read;this.mutate=mutate;this.openBrowser=openBrowser;
            SuspendLayout();Text="Edge 直连助手 · Soft Pop";Font=Pop.Font(9.5F,false);BackColor=Pop.Paper;ForeColor=Pop.Ink;
            ClientSize=new Size(960,640);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;Icon=SystemIcons.Application;
            DoubleBuffered=true;
            var side=new Panel {Bounds=new Rectangle(0,0,180,640),BackColor=Color.FromArgb(238,235,223)};
            side.Paint+=delegate(object sender,PaintEventArgs e) {using(var pen=new Pen(Pop.Line,1)) e.Graphics.DrawLine(pen,side.Width-1,0,side.Width-1,side.Height);};
            Controls.Add(side);
            var logo=new RouteArt {Bounds=new Rectangle(23,27,133,43),Brand=true};side.Controls.Add(logo);
            side.Controls.Add(Label("EDGE",23,82,134,35,22,true));side.Controls.Add(Label("DIRECT",24,117,136,30,17,true));
            side.Controls.Add(Label("浏览器路线助手",25,161,145,24,9,false,Pop.Muted));
            AddNavigation(side,"overview","01   直连概览",222);AddNavigation(side,"diagnostics","02   连接诊断",278);AddNavigation(side,"help","03   使用帮助",334);
            var stamp=new PopPanel {Bounds=new Rectangle(23,431,133,83),Fill=Pop.Butter,Shadow=false};
            stamp.Controls.Add(Label("SET ONCE.",14,13,107,22,10,true));stamp.Controls.Add(Label("关闭助手也生效",14,43,111,24,9,false));side.Controls.Add(stamp);
            restore=Button("恢复原设置",23,550,133,43,Pop.White);restore.Shadow=false;restore.Click+=delegate { ShowRestore(); };side.Controls.Add(restore);
            tips.SetToolTip(restore,"恢复启用前的 Edge 代理设置。");side.Controls.Add(Label("SOFT POP  /  2.0",25,606,142,20,8,false,Pop.Muted));
            content=new Panel {Bounds=new Rectangle(207,0,726,640),BackColor=Pop.Paper};Controls.Add(content);
            refresh=Button("刷新状态",596,27,127,40,Pop.White);refresh.Shadow=false;refresh.Click+=async delegate {await RefreshStatus();};content.Controls.Add(refresh);
            checkedAt=Label("正在读取…",526,75,196,20,8,false,Pop.Muted);checkedAt.TextAlign=ContentAlignment.MiddleRight;content.Controls.Add(checkedAt);
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ResumeLayout(false);
            RenderPage();Shown+=async delegate {Pop.Caption(this);if(!loaded) {loaded=true;await RefreshStatus();}};
            FormClosing+=delegate(object sender,FormClosingEventArgs e) {if(busy && operation!="refresh") {e.Cancel=true;SetFeedback("操作还在进行。","完成管理员授权或等待操作结束后，即可关闭助手。",false);RenderPage();}};
        }

        private void AddNavigation(Panel side,string key,string text,int y)
        {
            var button=Button(text,23,y,133,43,Pop.White);button.Shadow=false;button.Click+=delegate {Navigate(key);};navigation.Add(key,button);side.Controls.Add(button);
        }

        private static Label Label(string text,int x,int y,int w,int h,float size,bool bold,Color? color=null)
        { return new Label {Text=text,Bounds=new Rectangle(x,y,w,h),Font=Pop.Font(size,bold),ForeColor=color ?? Pop.Ink,BackColor=Color.Transparent,AutoEllipsis=false,UseMnemonic=false}; }
        private static PopButton Button(string text,int x,int y,int w,int h,Color fill)
        { return new PopButton {Text=text,Bounds=new Rectangle(x,y,w,h),Fill=fill,AccessibleName=text}; }
        private static PopPanel Card(int x,int y,int w,int h,Color fill)
        {return new PopPanel {Bounds=new Rectangle(x,y,w,h),Fill=fill};}
        private void Add(Control control) {content.Controls.Add(control);}
        private void Navigate(string target) {page=target;RenderPage();}
        private void SetFeedback(string title,string text,bool error) {feedbackTitle=title;feedbackText=text;feedbackError=error;if(!error) operationError=null;}

        private void RenderPage()
        {
            SuspendLayout();content.SuspendLayout();
            // Only rebuild page content, retaining navigation and refresh focus.
            var old=new List<Control>();foreach(Control c in content.Controls) if(c!=refresh && c!=checkedAt) old.Add(c);
            foreach(Control c in old) {content.Controls.Remove(c);c.Dispose();}
            foreach(var item in navigation) {item.Value.Fill=item.Key==page ? Pop.Sage : Color.FromArgb(238,235,223);item.Value.Invalidate();}
            var view=UiPresentation.From(snapshot,busy,operation);restore.Enabled=view.CanRestore;refresh.Enabled=!busy;
            restore.AccessibleDescription=snapshot!=null && snapshot.BackupPresent ? "恢复启用前的设置" : "没有本程序的备份，因此无需恢复";
            tips.SetToolTip(restore,snapshot!=null && snapshot.BackupError!=null ? snapshot.BackupError : restore.AccessibleDescription);
            checkedAt.Text=busy ? "检查与保存时请稍候" : snapshot==null ? "尚未读取" : "上次检查 "+snapshot.CheckedAt.ToString("HH:mm:ss");
            Add(Label("EDGE DIRECT  /  YOUR NETWORK, YOUR WAY",2,25,553,19,8,true,Pop.Muted));
            Add(Label(page=="overview" ? "让浏览器，各走各的。" : page=="diagnostics" ? "每一条路线，都有答案。" : "设置一次，轻松使用。",0,51,562,43,22,true));
            if(page=="overview") Overview(view);else if(page=="diagnostics") Diagnostics(view);else Help(view);
            Feedback();Add(Label("当前 Windows 用户  ·  设置保留，助手无需常驻",2,608,620,23,8.5F,false,Pop.Muted));
            refresh.BringToFront();checkedAt.BringToFront();
            content.ResumeLayout(false);ResumeLayout(false);
            // New children are added after DPI autoscaling; scale each from design units.
            float factor=CurrentAutoScaleDimensions.Width/96F;
            if(factor!=1F) foreach(Control c in content.Controls) if(c!=refresh && c!=checkedAt) c.Scale(new SizeF(factor,factor));
        }

        private void Overview(UiPresentation view)
        {
            var hero=Card(0,113,723,194,view.Hero);Add(hero);
            hero.Controls.Add(Label(view.Tag,23,17,435,23,9,true,Pop.Green));
            hero.Controls.Add(Label(view.Title,22,46,515,48,24,true));
            hero.Controls.Add(Label(view.Description,24,99,503,30,9.5F,false));
            primary=Button(view.Primary,24,139,185,41,Pop.Peach);primary.Enabled=view.CanPrimary;primary.BackColor=view.Hero;primary.Click+=async delegate {await PrimaryAction();};hero.Controls.Add(primary);
            bool needsDetails=snapshot!=null && (snapshot.Error!=null || snapshot.Conflict!=null || snapshot.BackupError!=null);
            verify=Button(needsDetails ? "查看原因" : snapshot!=null && snapshot.Direct ? "确认生效" : "了解直连",227,139,126,41,Pop.White);verify.BackColor=view.Hero;verify.Enabled=needsDetails ? !busy : snapshot!=null && snapshot.Direct ? view.CanVerify : !busy;
            verify.Click+=delegate {if(needsDetails) Navigate("diagnostics");else if(snapshot!=null && snapshot.Direct) ShowVerify();else Navigate("help");};hero.Controls.Add(verify);
            hero.Controls.Add(new RouteArt {Bounds=new Rectangle(516,0,193,185)});
            var route=Card(0,329,398,163,Pop.White);Add(route);
            route.Controls.Add(Label("浏览器的两条路线",19,15,291,25,11,true));
            route.Controls.Add(new RouteArt {Bounds=new Rectangle(18,49,183,105),Compact=true});
            route.Controls.Add(Label("Edge",63,57,64,24,10,true));route.Controls.Add(Label("Chrome",63,111,84,24,9.5F,true));
            route.Controls.Add(Label(snapshot!=null && snapshot.Error==null && snapshot.Conflict==null && snapshot.Direct ? "直连网络" : "等待设置 / 确认",203,57,177,25,10,true,Pop.Green));
            route.Controls.Add(Label("保持原有设置",203,111,176,24,9.5F,false,Pop.Muted));
            var proxy=Card(419,329,304,163,Pop.Lavender);Add(proxy);
            proxy.Controls.Add(Label("本地代理 · 127.0.0.1",20,15,260,24,9.5F,true));
            proxy.Controls.Add(Label("7688",18,42,113,52,27,true));
            proxy.Controls.Add(Label(snapshot==null || busy ? "检查中" : snapshot.Error!=null ? "未能读取" : snapshot.PortReachable ? "端口可达" : "端口未连接",153,60,122,30,11,true));
            proxy.Controls.Add(Label("此端口状态不影响 Edge 直连。",20,105,262,27,9,false));
            proxy.Controls.Add(Label(snapshot==null ? "读取系统代理…" : snapshot.Error!=null ? "请前往连接诊断查看详情" : "系统代理："+(snapshot.ManualProxy ? "开启" : "关闭")+"  /  PAC "+(snapshot.Pac ? "已配置" : "未配置"),20,133,267,23,8,false,Pop.Muted));
        }

        private void Diagnostics(UiPresentation view)
        {
            var edge=Card(0,113,723,131,snapshot!=null && snapshot.Conflict!=null ? Pop.Butter : Pop.Sage);Add(edge);
            edge.Controls.Add(Label("01  /  EDGE 策略",21,15,330,23,9,true));
            edge.Controls.Add(Label(snapshot==null ? "正在检查…" : snapshot.Error!=null ? "读取失败" : snapshot.Conflict!=null ? "发现冲突" : snapshot.Direct ? "已配置直连" : "尚未配置直连",20,45,435,36,19,true));
            edge.Controls.Add(Label("这里显示本机配置；浏览器最终生效状态请在策略页确认。",22,91,465,24,8.5F,false,Pop.Muted));
            var policy=Button("在 Edge 确认  ↗",528,46,166,44,Pop.White);policy.BackColor=edge.Fill;policy.Enabled=!busy && snapshot!=null && snapshot.EdgeInstalled;policy.Click+=delegate {ShowVerify();};edge.Controls.Add(policy);
            var port=Card(0,266,347,163,Pop.Lavender);Add(port);
            port.Controls.Add(Label("02  /  本地代理端口",20,15,301,24,9,true));
            port.Controls.Add(Label("127.0.0.1:7688",19,46,306,35,18,true));
            port.Controls.Add(Label(snapshot==null || busy ? "检查中…" : snapshot.Error!=null ? "未能完成检测" : snapshot.PortReachable ? "TCP 连接成功 · 端口可达" : "TCP 未连接 · 代理可能已关闭",21,93,304,25,9,false));
            port.Controls.Add(Label("仅检测端口，不代表代理转发成功。",21,127,299,23,8,false,Pop.Muted));
            var system=Card(368,266,355,163,Pop.White);Add(system);
            system.Controls.Add(Label("03  /  WINDOWS 网络",20,15,315,24,9,true));
            system.Controls.Add(Label(snapshot==null ? "正在读取…" : snapshot.Error!=null ? "读取失败" : "手动代理  "+(snapshot.ManualProxy ? "开启" : "关闭"),19,47,310,34,18,true));
            system.Controls.Add(Label(snapshot==null ? "PAC：正在读取…" : snapshot.Error!=null ? "请查看下方反馈" : "PAC 脚本："+(snapshot.Pac ? "已配置" : "未配置"),21,93,306,25,9,false));
            system.Controls.Add(Label("自动检测由系统控制，Chrome 保持原设置。",21,127,317,23,8,false,Pop.Muted));
            var copy=Button("复制诊断信息",0,452,168,39,Pop.White);copy.Shadow=false;copy.Enabled=!busy && snapshot!=null;copy.Click+=delegate {CopyDiagnostics();};Add(copy);
            var details=Button("查看详细原因",188,452,166,39,Pop.Butter);details.Shadow=false;details.Enabled=operationError!=null || snapshot!=null && (snapshot.Error!=null || snapshot.Conflict!=null || snapshot.BackupError!=null);details.Click+=delegate {ShowDetails();};Add(details);
        }

        private void Help(UiPresentation view)
        {
            var steps=Card(0,113,723,183,Pop.Butter);Add(steps);
            steps.Controls.Add(Label("三步，把路线分清楚。",21,16,660,30,16,true));
            string[] headings={"开启直连","浏览器内确认","照常打开 Edge"};
            string[] texts={"在概览页开启。\r\n如需管理员授权，选择“是”。","重新加载 Edge 策略。\r\n检查 ProxySettings 状态为 OK。","从原图标或链接打开。\r\n助手可以随时关闭。"};
            for(int i=0;i<3;i++)
            {
                int x=23+i*233;steps.Controls.Add(Label("0"+(i+1),x,63,47,31,16,true,Pop.Green));steps.Controls.Add(Label(headings[i],x+48,65,182,29,11,true));steps.Controls.Add(Label(texts[i],x,106,223,55,8.5F,false));
            }
            var left=Card(0,319,347,173,Pop.White);Add(left);
            left.Controls.Add(Label("为什么代理关了也能用？",20,17,307,27,11,true));
            left.Controls.Add(Label("Edge 固定直连，不再跟随系统代理\r\n或 PAC 地址；代理关闭后，也不会\r\n继续连接失效的端口。",21,58,308,77,9.5F,false));
            left.Controls.Add(Label("收藏夹和已有登录信息继续保留。",21,139,301,23,8.5F,false,Pop.Muted));
            var right=Card(368,319,355,173,Pop.Blue);Add(right);
            right.Controls.Add(Label("什么时候需要额外分流？",20,17,313,27,11,true));
            right.Controls.Add(Label("TUN / VPN 会在浏览器之外接管流量。\r\n这类情况需在代理软件里，将\r\nmsedge.exe 设置为直连。",21,58,316,77,9.5F,false));
            right.Controls.Add(Label("本助手不处理 DNS 或外部网络故障。",21,139,309,23,8.5F,false,Pop.Muted));
        }

        private void Feedback()
        {
            var box=Card(0,514,723,76,feedbackError ? Pop.Peach : Pop.White);box.Shadow=false;Add(box);
            var mark=Label(feedbackError ? "!" : "i",18,17,34,34,17,true,feedbackError ? Pop.Red : Pop.Green);mark.TextAlign=ContentAlignment.MiddleCenter;box.Controls.Add(mark);
            box.Controls.Add(Label(feedbackTitle,67,12,633,25,10,true));
            box.Controls.Add(Label(feedbackText,67,40,628,28,8.5F,false,Pop.Muted));
        }

        private async Task PrimaryAction()
        {
            if(busy) return;
            var view=UiPresentation.From(snapshot,false,operation);
            if(!view.CanPrimary) return;
            if(view.PrimaryAction=="enable") await Apply("enable");
            else if(view.PrimaryAction=="refresh") await RefreshStatus();
            else if(view.PrimaryAction=="policy") ShowVerify();
            else SafeOpen("edge://newtab","Edge 已打开。","若刚开启直连，请到策略页重新加载并确认生效。");
        }

        private void SafeOpen(string url,string title,string message)
        {
            try {openBrowser(url);SetFeedback(title,message,false);}
            catch(Exception ex) {operationError=ex.Message;SetFeedback("浏览器未能打开。",Short(ex.Message),true);}
            RenderPage();
        }

        private void ShowVerify()
        {
            if(busy) return;
            string detail="1. 在 Edge 策略页点击“重新加载策略”。\r\n\r\n2. 找到 ProxySettings，确认值为\r\n    {\"ProxyMode\":\"direct\"}。\r\n\r\n3. 确认状态显示“确定 / OK”。\r\n    已打开的网页如仍异常，保存后完整退出并重开 Edge。";
            using(var dialog=new PopDialog("在浏览器里，确认一次。",detail,"打开策略页  ↗","返回助手",Pop.Blue))
            {dialog.Opacity=Opacity;if(dialog.ShowDialog(this)==DialogResult.OK) SafeOpen("edge://policy","下一步：在 Edge 确认生效。","重新加载策略，检查 ProxySettings 的值为 direct、状态为 OK。");}
        }

        private async void ShowRestore()
        {
            if(!UiPresentation.From(snapshot,busy,operation).CanRestore) return;
            using(var dialog=new PopDialog("把 Edge 设置还原？","恢复后，Edge 会重新使用启用前的代理设置。\r\n\r\n如果原先跟随 Windows 代理，也会重新跟随。\r\n\r\n你的收藏夹与登录信息会保留；恢复后请重新加载\r\nEdge 策略，或保存网页后退出并重开 Edge。","恢复原设置","先保留直连",Pop.Peach))
            {dialog.Opacity=Opacity;if(dialog.ShowDialog(this)!=DialogResult.OK) return;}
            await Apply("restore");
        }

        private async Task Apply(string action)
        {
            if(busy) return;
            busy=true;operation=action;
            operationError=null;
            SetFeedback(action=="enable" ? "正在开启始终直连…" : "正在恢复原设置…","如 Windows 请求管理员授权，请在系统窗口中完成确认。",false);RenderPage();
            Exception failure=null;
            try
            {
                await Task.Run(delegate {return mutate(action);});
                snapshot=await Task.Run(read);
                bool expected=snapshot.Error==null && (action=="enable" ? snapshot.Direct && snapshot.Conflict==null : !snapshot.BackupPresent);
                if(expected) SetFeedback(action=="enable" ? "直连设置已保存。" : "原设置已恢复。",action=="enable" ? "接下来点击“确认生效”，或保存网页后完整退出并重开 Edge。" : "请重新加载 Edge 策略，或保存网页后完整退出并重开 Edge。",false);
                else {operationError="设置回读与操作预期不一致。请检查当前 Edge 策略和备份状态，必要时在 edge://policy 确认生效来源。";SetFeedback("操作结束，还需要检查。","设置回读与预期不一致，请打开连接诊断查看当前状态。",true);}
            }
            catch(Exception ex)
            {
                failure=ex;operationError=ex.Message;
                SetFeedback(ex.Message.IndexOf("取消",StringComparison.Ordinal)>=0 ? "授权已取消，可以稍后重试。" : "这次操作未能完成。",Short(ex.Message),true);
            }
            if(failure!=null)
                try {snapshot=await Task.Run(read);} catch(Exception readEx) {snapshot=new UiSnapshot {Error=readEx.Message,CheckedAt=DateTime.Now};}
            busy=false;operation="refresh";if(!IsDisposed) RenderPage();
        }

        public async Task RefreshStatus()
        {
            if(busy || IsDisposed) return;
            busy=true;operation="refresh";RenderPage();
            try
            {
                snapshot=await Task.Run(read);
                if(snapshot.Error!=null) SetFeedback("状态读取未完成。",Short(snapshot.Error),true);
                else if(snapshot.Conflict!=null) SetFeedback("发现其他代理策略。","请打开连接诊断查看原因，再在 Edge 策略页确认来源。",true);
                else if(snapshot.BackupError!=null) SetFeedback("原设置备份需要检查。","直连状态已读取；备份异常会阻止启用和恢复，请查看详细原因。",true);
                else if(feedbackError) SetFeedback("状态已重新读取。","现在可以按当前状态继续操作，详细配置见连接诊断。",false);
            }
            catch(Exception ex) {snapshot=new UiSnapshot {Error=ex.Message,CheckedAt=DateTime.Now};SetFeedback("状态读取未完成。",Short(ex.Message),true);}
            finally {busy=false;if(!IsDisposed) RenderPage();}
        }

        private static string Short(string value) {var text=(value ?? "未知原因，请重新检查。").Replace("\r"," ").Replace("\n"," ");return text.Length>73 ? text.Substring(0,70)+"…（诊断页查看详情）" : text;}
        private void ShowDetails()
        {
            if(snapshot==null && operationError==null) return;
            string details=operationError ?? snapshot.Error ?? snapshot.Conflict ?? snapshot.BackupError ?? "未发现异常。";
            using(var dialog=new PopDialog("这次检查的详细信息。",details,"我知道了",null,Pop.Butter)) dialog.ShowDialog(this);
        }
        private void CopyDiagnostics()
        {
            if(snapshot==null || busy) return;
            try
            {
                Clipboard.SetText("Edge Direct 2.0\r\n检查时间："+snapshot.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss")+"\r\nEdge 直连已配置："+snapshot.Direct+"\r\nEdge 已安装："+snapshot.EdgeInstalled+"\r\n127.0.0.1:7688 TCP 可达："+snapshot.PortReachable+"\r\n系统手动代理："+snapshot.ManualProxy+"\r\nPAC 已配置："+snapshot.Pac+"\r\n本程序备份存在："+snapshot.BackupPresent+"\r\n策略冲突："+(snapshot.Conflict ?? "无")+"\r\n读取错误："+(snapshot.Error ?? "无")+"\r\n备份错误："+(snapshot.BackupError ?? "无")+"\r\n操作错误："+(operationError ?? "无"));
                SetFeedback("诊断信息已复制。","可以粘贴这些信息，用于定位连接或策略问题。",false);
            }
            catch(Exception ex) {operationError=ex.Message;SetFeedback("暂时无法复制。",Short(ex.Message),true);}
            RenderPage();
        }

        internal void SuppressInitialRefresh() {loaded=true;}
        internal void Preview(string scenario)
        {
            loaded=true;busy=false;snapshot=new UiSnapshot {Direct=true,BackupPresent=true,EdgeInstalled=true,PortReachable=true,CheckedAt=new DateTime(2026,10,1,10,28,0)};
            if(scenario=="disabled") {snapshot.Direct=false;snapshot.BackupPresent=false;snapshot.PortReachable=false;}
            if(scenario=="conflict") snapshot.Conflict="检测到计算机级 Edge 代理策略，请在 edge://policy 确认来源。";
            if(scenario=="error") {snapshot.Error="无法读取注册表策略。请检查访问权限后重试。";SetFeedback("状态读取未完成。",snapshot.Error,true);}
            if(scenario=="busy") {busy=true;operation="enable";SetFeedback("正在开启始终直连…","如 Windows 请求管理员授权，请在系统窗口中完成确认。",false);}
            if(scenario=="diagnostics" || scenario=="help") page=scenario;
            RenderPage();
        }
    }

    internal sealed class PopDialog : Form
    {
        public PopDialog(string title,string body,string confirm,string cancel,Color fill)
        {
            SuspendLayout();Text="Edge 直连助手";Font=Pop.Font(10,false);BackColor=Pop.Paper;ForeColor=Pop.Ink;ClientSize=new Size(545,355);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
            var card=new PopPanel {Bounds=new Rectangle(21,21,503,83),Fill=fill};
            card.Controls.Add(new Label {Text=title,Bounds=new Rectangle(19,25,466,38),Font=Pop.Font(19,true),BackColor=Color.Transparent,ForeColor=Pop.Ink});Controls.Add(card);
            var text=new TextBox {Text=body,Bounds=new Rectangle(25,125,493,154),Font=Pop.Font(9.5F,false),Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Pop.Paper,ForeColor=Pop.Ink,TabStop=false};Controls.Add(text);
            var yes=new PopButton {Text=confirm,Bounds=new Rectangle(cancel==null ? 322 : 283,296,cancel==null ? 196 : 238,42),Fill=fill,DialogResult=DialogResult.OK};Controls.Add(yes);
            if(cancel!=null) {var no=new PopButton {Text=cancel,Bounds=new Rectangle(23,296,237,42),Fill=Pop.White,DialogResult=DialogResult.Cancel};Controls.Add(no);CancelButton=no;}
            AcceptButton=yes;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ResumeLayout(false);Shown+=delegate {Pop.Caption(this);};
        }
    }
}
