using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal sealed class TrayMenu : ContextMenuStrip {
        private readonly float scale;
        private readonly Font menuFont;
        internal bool NativeCornersRequested {get;private set;}
        internal TrayMenu(float dpiScale) {
            scale=dpiScale;menuFont=new Font("Microsoft YaHei UI",10f,FontStyle.Regular);Font=menuFont;
            ShowImageMargin=false;ShowCheckMargin=false;BackColor=Color.White;ForeColor=PanelTheme.Ink;
            AutoSize=false;AutoClose=true;Renderer=new TrayRenderer(scale);Padding=new Padding(S(8));DropShadowEnabled=true;
        }
        private int S(int value){return Math.Max(1,(int)Math.Round(value*scale));}
        internal ToolStripMenuItem AddAction(string text,EventHandler action) {
            var item=new ToolStripMenuItem(text,null,action) {AutoSize=false,Size=new Size(S(200),S(38)),Margin=Padding.Empty,Padding=new Padding(S(12),0,S(8),0)};
            Items.Add(item);UpdateSize();return item;
        }
        internal void AddDivider(){Items.Add(new ToolStripSeparator {AutoSize=false,Size=new Size(S(200),S(14)),Margin=Padding.Empty});UpdateSize();}
        private void UpdateSize(){int height=S(16);foreach(ToolStripItem item in Items)height+=item.Height;Size=new Size(S(216),height);}
        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);if(scale<=0||Width<1||Height<1)return;
            UpdateOutline();
        }
        protected override void OnHandleCreated(EventArgs e) {base.OnHandleCreated(e);if(scale>0)UpdateOutline();}
        private void UpdateOutline() {
            var old=Region;Region=null;
            if(old!=null)old.Dispose();
            if(IsHandleCreated) {
                int corner=2,border=226|(231<<8)|(238<<16);
                NativeCornersRequested=Native.DwmSetWindowAttribute(Handle,33,ref corner,4)==0;
                if(NativeCornersRequested)Native.DwmSetWindowAttribute(Handle,34,ref border,4);
            }
            var renderer=Renderer as TrayRenderer;if(renderer!=null)renderer.NativeOutline=NativeCornersRequested;
            if(!NativeCornersRequested)using(var path=PanelTheme.Rounded(new RectangleF(0,0,Width,Height),14*scale))Region=new Region(path);
        }
        protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing&&menuFont!=null)menuFont.Dispose();}
    }
    internal sealed class TrayRenderer : ToolStripProfessionalRenderer {
        private readonly float scale;
        internal bool NativeOutline;
        internal TrayRenderer(float dpiScale){scale=dpiScale;RoundedEdges=false;}
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(Color.White);}
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) {
            if(NativeOutline)return;
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=PanelTheme.Rounded(new RectangleF(scale/2,scale/2,e.ToolStrip.Width-scale,e.ToolStrip.Height-scale),14*scale-scale/2))using(var pen=new Pen(PanelTheme.Border,scale))e.Graphics.DrawPath(pen,path);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
            if(!e.Item.Selected||!e.Item.Enabled)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=PanelTheme.Rounded(new RectangleF(0,0,e.Item.Width-1,e.Item.Height-1),8*scale))using(var brush=new SolidBrush(PanelTheme.Surface))e.Graphics.FillPath(brush,path);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){
            int inset=(int)Math.Round(16*scale);
            TextRenderer.DrawText(e.Graphics,e.Text,e.Item.Font,new Rectangle(inset,0,e.Item.Width-inset*2,e.Item.Height),PanelTheme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){using(var pen=new Pen(PanelTheme.Border,scale))e.Graphics.DrawLine(pen,8*scale,e.Item.Height/2,e.Item.Width-8*scale,e.Item.Height/2);}
    }
}
