using System;
using System.Drawing;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal enum DockSide { None,Left,Right }
    // Pure geometry for the dock contract; no window state, no preferences.
    internal static class DockGeometry {
        internal const int ThresholdDip=12,TabWidthDip=32,TabHeightDip=80,TabRadius=12;
        internal static int Dip(int value,float scale){return Math.Max(1,(int)Math.Round(value*scale));}
        // A release docks only when a vertical edge of the restored rectangle
        // sits within the threshold of the working area boundary. Ties keep
        // the previous side, otherwise the right edge wins.
        internal static DockSide Hit(Rectangle rect,Rectangle area,int threshold,DockSide previous) {
            if(rect.Width<=0||area.Width<=0||threshold<0)return DockSide.None;
            bool spans=rect.Bottom>area.Top&&rect.Top<area.Bottom;
            bool left=spans&&Math.Abs(rect.Left-area.Left)<=threshold;
            bool right=spans&&Math.Abs(rect.Right-area.Right)<=threshold;
            if(left&&right)return previous==DockSide.Left||previous==DockSide.Right?previous:DockSide.Right;
            if(left)return DockSide.Left;
            if(right)return DockSide.Right;
            return DockSide.None;
        }
        // A boundary shared with another monitor's full bounds around the tab
        // rectangle is a seam: docking there would park the label across displays.
        internal static bool HasSeam(Rectangle areaBounds,DockSide side,Rectangle tabBounds,Screen[] screens) {
            if(screens==null)return false;
            foreach(var screen in screens) {
                if(screen.Bounds==areaBounds)continue;
                if(screen.Bounds.IntersectsWith(tabBounds))return true;
            }
            return false;
        }
        internal static Rectangle TabBounds(DockSide side,Rectangle area,int centerY,float scale) {
            int width=Dip(TabWidthDip,scale),height=Dip(TabHeightDip,scale);
            int x=side==DockSide.Left?area.Left:area.Right-width;
            int y=Math.Max(area.Top,Math.Min(centerY-height/2,area.Bottom-height));
            return new Rectangle(x,y,width,height);
        }
        internal static Rectangle Fit(Rectangle bounds,Rectangle area) {
            int width=Math.Max(1,Math.Min(bounds.Width,area.Width)),height=Math.Max(1,Math.Min(bounds.Height,area.Height));
            return new Rectangle(Math.Max(area.Left,Math.Min(bounds.Left,area.Right-width)),Math.Max(area.Top,Math.Min(bounds.Top,area.Bottom-height)),width,height);
        }
    }
    // Independent, non-activating edge label. The hidden main window stays
    // alive; this window never owns the chat, the WebView or the taskbar.
    internal sealed class EdgeDockTab : Form {
        private readonly DockSide side;
        private readonly float scale;
        private readonly Icon appIcon;
        private bool hover;
        internal event EventHandler ActivateRequested;
        internal EdgeDockTab(DockSide dockSide,float dpiScale,Icon icon) {
            side=dockSide;scale=dpiScale;appIcon=icon;
            FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;
            ShowInTaskbar=false;AutoScaleMode=AutoScaleMode.None;Text="DeepSeek-Reflex";
            BackColor=PanelTheme.Back;Cursor=Cursors.Hand;
            AccessibleName="展开 DeepSeek-Reflex";
            int width=Dip(DockGeometry.TabWidthDip),height=Dip(DockGeometry.TabHeightDip);
            Size=new Size(width,height);MinimumSize=Size;MaximumSize=Size;
            MouseEnter+=delegate{hover=true;Invalidate();};
            MouseLeave+=delegate{hover=false;Invalidate();};
            var tabTips=new ToolTip();tabTips.SetToolTip(this,"点击展开 DeepSeek-Reflex，按唤起键同样有效");
            Click+=delegate{if(ActivateRequested!=null)ActivateRequested(this,EventArgs.Empty);};
            Paint+=PaintTab;
        }
        internal void ApplyTheme(){BackColor=PanelTheme.Back;Invalidate(true);}
        private int Dip(int value){return DockGeometry.Dip(value,scale);}
        private void PaintTab(object sender,PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(PanelTheme.Back);
            int radius=Dip(DockGeometry.TabRadius);
            var rect=new RectangleF(0.5f,0.5f,Width-1,Height-1);
            if(hover)using(var brush=new SolidBrush(PanelTheme.Hover))g.FillPath(brush,PanelTheme.Rounded(rect,radius));
                using(var pen=new Pen(WindowFrame.Outline,1f))g.DrawPath(pen,PanelTheme.Rounded(rect,radius));
            int icon=Dip(18);
            if(appIcon!=null)g.DrawIcon(appIcon,new Rectangle((Width-icon)/2,Dip(12),icon,icon));
            // Inward chevron under the fish marks the restore action.
            float cx=Width/2f,cy=Height*0.64f,arm=Dip(5),direction=side==DockSide.Left?1:-1;
            using(var pen=new Pen(PanelTheme.Ink,1.5f)) {
                g.DrawLine(pen,cx-arm*direction,cy-arm,cx+arm*direction,cy);
                g.DrawLine(pen,cx+arm*direction,cy,cx-arm*direction,cy+arm);
            }
        }
        internal void Place(Rectangle bounds) { Bounds=bounds; }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams {
            get {
                var value=base.CreateParams;
                value.ExStyle|=0x08000000; // WS_EX_NOACTIVATE: never takes keyboard focus
                value.ExStyle|=0x00000080; // WS_EX_TOOLWINDOW: no taskbar or Alt+Tab entry
                return value;
            }
        }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            using(var path=PanelTheme.Rounded(new RectangleF(0,0,Width,Height),Dip(DockGeometry.TabRadius)))Region=new Region(path);
        }
    }
    // Owns the docked state: one tab, the saved restore rectangle and the
    // display-change fallbacks. Never touches the WebView or preferences.
    internal sealed class EdgeDockController {
        private readonly ChatWindow owner;
        private EdgeDockTab tab;
        private DockSide side;
        private Rectangle savedRect;
        private Screen savedScreen;
        private float savedScale=1;
        private bool docked;
        internal bool Docked {get{return docked;}}
        internal void ApplyTheme(){if(tab!=null)tab.ApplyTheme();}
        internal DockSide Side {get{return side;}}
        internal Rectangle SavedRect {get{return savedRect;}}
        internal EdgeDockTab Tab {get{return tab;}}
        internal EdgeDockController(ChatWindow chatWindow) { owner=chatWindow; }
        // Real title-bar drags only; the caller records the pre-move rectangle.
        internal void OnDragEnded(Rectangle before) {
            if(docked||owner.Settings==null||!owner.Settings.EdgeDockEnabled)return;
            if(owner.WindowState!=FormWindowState.Normal||owner.ModalOpen||!owner.Visible)return;
            // A drag that started already inside dock range belongs to a move
            // within the edge band, not a deliberate dock release.
            if(DockGeometry.Hit(before,Screen.FromRectangle(before).WorkingArea,Threshold(),side)!=DockSide.None)return;
            Dock(owner.Bounds);
        }
        internal bool Dock(Rectangle rect) {
            if(docked)return true;
            var screen=Screen.FromRectangle(rect);
            var hit=DockGeometry.Hit(rect,screen.WorkingArea,Threshold(),side);
            if(hit==DockSide.None)return false;
            side=hit;
            savedRect=rect;savedScreen=screen;savedScale=owner.DockScale;
            ShowTab(DockGeometry.TabBounds(hit,screen.WorkingArea,rect.Top+rect.Height/2,savedScale));
            docked=true;
            owner.Hide();
            owner.WriteHealth();
            return true;
        }
        private void ShowTab(Rectangle tabBounds) {
            if(tab==null||tab.IsDisposed) {
                tab=new EdgeDockTab(side,savedScale,owner.AppIcon);
                tab.ActivateRequested+=delegate { if(owner.IsHandleCreated)owner.BeginInvoke(new Action(delegate { owner.WakeFromDock(); })); };
            }
            tab.Place(tabBounds);
            tab.TopMost=owner.TopMost;
            tab.Show();
        }
        // Undock only: make the restored rectangle current again. The caller
        // keeps using the normal wake / focus flow afterwards.
        internal void Restore() {
            if(!docked)return;
            docked=false;
            HideTab();
            var screen=ResolveScreen();
            if(screen!=null)savedRect=DockGeometry.Fit(savedRect,screen.WorkingArea);
            owner.Bounds=savedRect;
            owner.SaveBounds();
        }
        private void HideTab() {
            if(tab!=null&&!tab.IsDisposed)tab.Close();
            tab=null;
        }
        internal void OnDisplaysChanged() {
            if(!docked)return;
            var screen=ResolveScreen();
            if(screen==null) { owner.BeginInvoke(new Action(delegate { owner.WakeFromDock(); })); return; }
            savedScreen=screen;
            savedRect=DockGeometry.Fit(savedRect,screen.WorkingArea);
            var hit=DockGeometry.Hit(savedRect,screen.WorkingArea,Threshold(),side);
            if(hit==DockSide.None) { owner.BeginInvoke(new Action(delegate { owner.WakeFromDock(); })); return; }
            side=hit;
            ShowTab(DockGeometry.TabBounds(side,screen.WorkingArea,savedRect.Top+savedRect.Height/2,savedScale));
        }
        private Screen ResolveScreen() {
            foreach(var screen in Screen.AllScreens)if(screen.DeviceName==savedScreen.DeviceName)return screen;
            Screen nearest=null;long best=long.MaxValue;
            var center=savedRect.Location;
            foreach(var screen in Screen.AllScreens) {
                long dx=screen.Bounds.X-center.X,dy=screen.Bounds.Y-center.Y;
                long distance=dx*dx+dy*dy;
                if(distance<best) { best=distance;nearest=screen; }
            }
            return nearest;
        }
        internal void Shutdown() { docked=false;HideTab(); }
        private int Threshold(){return DockGeometry.Dip(DockGeometry.ThresholdDip,owner.DockScale);}
    }
}
