using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace CrusaderTrailManager {
 public static class Theme {
  public static readonly Color Background=Color.FromArgb(17,23,31),Surface=Color.FromArgb(25,33,44),Raised=Color.FromArgb(34,44,57),Line=Color.FromArgb(54,65,80),Gold=Color.FromArgb(216,181,113),Text=Color.FromArgb(232,234,237),Muted=Color.FromArgb(158,171,187);
  public static void Apply(Control root){foreach(Control c in root.Controls){c.ForeColor=Text;
   if(c is Label){c.BackColor=Color.Transparent;c.ForeColor=Muted;}
   else if(c is TextBox){var b=(TextBox)c;b.BorderStyle=BorderStyle.FixedSingle;b.BackColor=Surface;b.ForeColor=Muted;}
   else if(c is ComboBox){var b=(ComboBox)c;b.BackColor=Raised;b.ForeColor=Text;b.FlatStyle=FlatStyle.Flat;b.DrawMode=DrawMode.OwnerDrawFixed;b.ItemHeight=25;b.DrawItem+=delegate(object sender,DrawItemEventArgs e){if(e.Index<0)return;bool on=(e.State&DrawItemState.Selected)!=0;using(var brush=new SolidBrush(on?Line:Raised))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,b.Items[e.Index].ToString(),b.Font,new Rectangle(e.Bounds.X+9,e.Bounds.Y,e.Bounds.Width-10,e.Bounds.Height),Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};}
   else if(c is ListBox){var b=(ListBox)c;b.BackColor=Surface;b.BorderStyle=BorderStyle.None;b.DrawMode=DrawMode.OwnerDrawFixed;b.ItemHeight=31;b.DrawItem+=delegate(object sender,DrawItemEventArgs e){if(e.Index<0)return;bool on=(e.State&DrawItemState.Selected)!=0;using(var brush=new SolidBrush(on?Raised:Surface))e.Graphics.FillRectangle(brush,e.Bounds);if(on)using(var brush=new SolidBrush(Gold))e.Graphics.FillRectangle(brush,e.Bounds.X,e.Bounds.Y,3,e.Bounds.Height);TextRenderer.DrawText(e.Graphics,b.Items[e.Index].ToString(),b.Font,new Rectangle(e.Bounds.X+15,e.Bounds.Y,e.Bounds.Width-20,e.Bounds.Height),on?Gold:Muted,TextFormatFlags.VerticalCenter);};}
   else if(c is ListView){var b=(ListView)c;b.BackColor=Surface;b.BorderStyle=BorderStyle.None;b.OwnerDraw=true;b.DrawColumnHeader+=delegate(object sender,DrawListViewColumnHeaderEventArgs e){using(var brush=new SolidBrush(Raised))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header.Text,new Font(b.Font,FontStyle.Bold),new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-14,e.Bounds.Height),Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};b.DrawItem+=delegate(object sender,DrawListViewItemEventArgs e){if(b.View!=View.Details)e.DrawDefault=true;};b.DrawSubItem+=delegate(object sender,DrawListViewSubItemEventArgs e){bool on=e.Item.Selected;using(var brush=new SolidBrush(on?Line:e.ItemIndex%2==0?Surface:Background))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.SubItem.Text,b.Font,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-15,e.Bounds.Height),on?Gold:Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};}
   else if(c is Panel)c.BackColor=Surface;
   Apply(c);
  }}
 }
 public class StyledButton:Button {
  public bool Accent;bool hover;
  public StyledButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.SupportsTransparentBackColor,true);}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new Rectangle(1,1,Width-3,Height-3);using(var path=new GraphicsPath()){int d=12;path.AddArc(r.X,r.Y,d,d,180,90);path.AddArc(r.Right-d,r.Y,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.X,r.Bottom-d,d,d,90,90);path.CloseFigure();using(var brush=new SolidBrush(Accent?(hover?Color.FromArgb(235,202,143):Theme.Gold):(hover?Theme.Line:Theme.Raised)))e.Graphics.FillPath(brush,path);using(var pen=new Pen(Accent?Theme.Gold:Theme.Line))e.Graphics.DrawPath(pen,path);}TextRenderer.DrawText(e.Graphics,Text,Font,r,Enabled?(Accent?Theme.Background:Theme.Text):Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(6,6,Width-12,Height-12),Theme.Muted,Color.Transparent);}
 }
 public class StyledComboBox:ComboBox {
  protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg==0xF){using(var g=Graphics.FromHwnd(Handle)){using(var brush=new SolidBrush(Theme.Raised))g.FillRectangle(brush,Width-25,1,24,Height-2);using(var pen=new Pen(Theme.Line))g.DrawRectangle(pen,0,0,Width-1,Height-1);int x=Width-14,y=Height/2;using(var brush=new SolidBrush(Theme.Gold))g.FillPolygon(brush,new[]{new Point(x-4,y-2),new Point(x+4,y-2),new Point(x,y+3)});}}}
 }
}
