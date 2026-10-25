using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace DeepSeekFloat {
    internal sealed class TrayMenu : ContextMenuStrip {
        internal const TextFormatFlags TextFlags=TextFormatFlags.Left|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix;
        private readonly float scale;
        private readonly Font menuFont;
        internal bool NativeCornersRequested {get;private set;}
        internal TrayMenu(float dpiScale) {
            scale=dpiScale;
            // Pixel-unit font: GDI measure and paint stay identical at any
            // monitor DPI, unlike point fonts whose pixel size tracks the DC.
            menuFont=new Font(PanelTheme.FontFamily,Math.Max(9f,(float)Math.Round(12f*Math.Max(1f,dpiScale)*2f)/2f),GraphicsUnit.Pixel);Font=menuFont;
            ShowImageMargin=false;ShowCheckMargin=false;BackColor=PanelTheme.Back;ForeColor=PanelTheme.Ink;
            AutoSize=false;AutoClose=true;Renderer=new TrayRenderer(scale);Padding=new Padding(0,S(4),0,S(4));DropShadowEnabled=true;
        }
        internal int S(int value){return Math.Max(1,(int)Math.Round(value*scale));}
        internal ToolStripMenuItem AddAction(string text,EventHandler action) {
            var item=new ToolStripMenuItem(text,null,action) {AutoSize=false,Size=new Size(S(200),S(PanelTheme.MenuRowHeight)),Margin=Padding.Empty,Padding=Padding.Empty};
            Items.Add(item);UpdateSize();return item;
        }
        internal void AddDivider(){Items.Add(new ToolStripSeparator {AutoSize=false,Size=new Size(S(200),S(14)),Margin=Padding.Empty});UpdateSize();}
        internal int Side{get;private set;}
        private void UpdateSize(){
            // Width hugs the widest item; the fixed 216 DIP pill left large
            // blank margins next to short mode names. Side margins derive
            // from the measured text itself, not from dpiScale: GDI measures
            // at a DC that does not always follow the window scale, and any
            // mismatch between the two surfaced as a blank column after the
            // last glyph.
            int textWidth=0;
            foreach(ToolStripItem item in Items){
                if(item is ToolStripSeparator)continue;
                textWidth=Math.Max(textWidth,TextRenderer.MeasureText(item.Text,menuFont,new Size(int.MaxValue,int.MaxValue),TextFlags).Width);
            }
            Side=Math.Max(S(8),(int)Math.Round(textWidth*0.16f));
            // The check gutter in the renderer needs a fixed slice on every
            // row so the text column stays aligned whether or not the row
            // shows a mark. Items span the full menu width edge to edge:
            // the hover strip is a full-bleed band, so no inset chrome may
            // survive on either side. Width hugs the text with no floor
            // beyond the clamp — a raised minimum just turns into blank
            // margins next to four-character mode names.
            int inner=Math.Max(S(56),Math.Min(S(300),textWidth+Side*2+S(20)));
            int height=S(8);
            foreach(ToolStripItem item in Items){item.Size=new Size(inner,item.Height);height+=item.Height;}
            Size=new Size(inner,height);
        }
        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);if(scale<=0||Width<1||Height<1)return;
            UpdateOutline();
        }
        protected override void OnHandleCreated(EventArgs e) {base.OnHandleCreated(e);if(scale>0)UpdateOutline();}
        private void UpdateOutline() {
            var old=Region;Region=null;
            if(old!=null)old.Dispose();
            if(IsHandleCreated) {
                int corner=2,border=PanelTheme.Border.R|(PanelTheme.Border.G<<8)|(PanelTheme.Border.B<<16);
                NativeCornersRequested=Native.DwmSetWindowAttribute(Handle,33,ref corner,4)==0;
                if(NativeCornersRequested)Native.DwmSetWindowAttribute(Handle,34,ref border,4);
            }
            var renderer=Renderer as TrayRenderer;if(renderer!=null)renderer.NativeOutline=NativeCornersRequested;
            if(!NativeCornersRequested)using(var path=PanelTheme.Rounded(new RectangleF(0,0,Width,Height),PanelTheme.PanelRadius*scale))Region=new Region(path);
        }
        protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing&&menuFont!=null)menuFont.Dispose();}
    }
    internal sealed class TrayRenderer : ToolStripProfessionalRenderer {
        private readonly float scale;
        internal bool NativeOutline;
        internal TrayRenderer(float dpiScale){scale=dpiScale;RoundedEdges=false;}
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(PanelTheme.Back);}
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) {
            if(NativeOutline)return;
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=PanelTheme.Rounded(new RectangleF(scale/2,scale/2,e.ToolStrip.Width-scale,e.ToolStrip.Height-scale),PanelTheme.PanelRadius*scale-scale/2))using(var pen=new Pen(PanelTheme.Border,scale))e.Graphics.DrawPath(pen,path);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
            if(!e.Item.Selected||!e.Item.Enabled)return;
            // Full-bleed band: the highlight spans the item rect with square
            // corners, so the strip reads as one even band across the menu.
            using(var brush=new SolidBrush(PanelTheme.Hover))e.Graphics.FillRectangle(brush,0,0,e.Item.Width,e.Item.Height);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){
            var owner=e.Item.Owner as TrayMenu;
            int inset=owner!=null?owner.Side:(int)Math.Round(8*scale);
            int gutter=owner!=null?owner.S(20):(int)Math.Round(20*scale);
            // Paint with the menu's own font: measuring and painting must use
            // one font, or a DPI change makes the painted text wider than the
            // width the menu was sized with. The check mark lives in a fixed
            // gutter so every text column starts at the same x.
            var font=owner!=null?owner.Font:e.Item.Font;
            var markable=e.Item as ToolStripMenuItem;
            if(markable!=null&&markable.Checked)TextRenderer.DrawText(e.Graphics,"✓",font,new Rectangle(inset,0,gutter,e.Item.Height),PanelTheme.Accent,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics,e.Text,font,new Rectangle(inset+gutter,0,Math.Max(1,e.Item.Width-inset*2-gutter),e.Item.Height),e.Item.Enabled?PanelTheme.Ink:PanelTheme.Muted,TrayMenu.TextFlags|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){using(var pen=new Pen(PanelTheme.Border,scale))e.Graphics.DrawLine(pen,0,e.Item.Height/2,e.Item.Width,e.Item.Height/2);}
    }
}
