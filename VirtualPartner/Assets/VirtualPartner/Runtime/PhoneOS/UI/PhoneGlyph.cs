using UnityEngine;
using UnityEngine.UI;
namespace VirtualPartner.Runtime.PhoneOS
{
    public enum PhoneGlyphKind { Chat, Camera, Settings, Debug, Back, Home, Recent, Search, Mic, Send, Chevron, More, Sun, Wifi, Battery, Signal, Plus, Move, Rotate, Check, Copy, Reset, Heart, Phone }
    /// <summary>Small original vector icons, rendered as UI geometry rather than font characters.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PhoneGlyph : MaskableGraphic
    {
        public PhoneGlyphKind kind;
        public float stroke = 1.7f;
        private VertexHelper mesh;
        private Texture2D symbol;
        public override Texture mainTexture => (symbol=PhoneVisualTheme.Symbol(kind))!=null?symbol:base.mainTexture;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); mesh = vh;
            symbol=PhoneVisualTheme.Symbol(kind);
            if(symbol!=null)
            {
                var r=rectTransform.rect;
                vh.AddVert(new Vector3(r.xMin,r.yMin),color,new Vector2(0,0));
                vh.AddVert(new Vector3(r.xMin,r.yMax),color,new Vector2(0,1));
                vh.AddVert(new Vector3(r.xMax,r.yMax),color,new Vector2(1,1));
                vh.AddVert(new Vector3(r.xMax,r.yMin),color,new Vector2(1,0));
                vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);return;
            }
            switch (kind)
            {
                case PhoneGlyphKind.Chat: Path(4,4,20,4,20,16,11,16,5,21,5,16,4,16,4,4); Path(8,8,16,8); Path(8,12,14,12); break;
                case PhoneGlyphKind.Camera: Path(3,7,7,7,9,4,15,4,17,7,21,7,21,20,3,20,3,7); Circle(12,13,4); break;
                case PhoneGlyphKind.Settings: Circle(12,12,7); Circle(12,12,2.7f); for(var i=0;i<8;i++) Radial(12,12,7,10,i*45); break;
                case PhoneGlyphKind.Debug: Path(7,8,17,8,17,18,14,21,10,21,7,18,7,8); Path(9,8,9,4,15,4,15,8); Path(12,9,12,19); for(var i=0;i<3;i++){Line(3,9+i*5,7,11+i*3);Line(17,11+i*3,21,9+i*5);} break;
                case PhoneGlyphKind.Back: Path(15,5,8,12,15,19); break;
                case PhoneGlyphKind.Home: Circle(12,12,7); break;
                case PhoneGlyphKind.Recent: Line(7,6,7,18);Line(12,6,12,18);Line(17,6,17,18);break;
                case PhoneGlyphKind.Search: Circle(10,10,6);Line(14.5f,14.5f,21,21);break;
                case PhoneGlyphKind.Mic: Path(9,5,10,3,14,3,15,5,15,12,14,14,10,14,9,12,9,5);Path(6,11,6,13,8,17,12,18,16,17,18,13,18,11);Line(12,18,12,22);break;
                case PhoneGlyphKind.Send: Path(3,3,22,12,3,21,6,12,3,3);Line(6,12,16,12);break;
                case PhoneGlyphKind.Chevron: Path(9,6,15,12,9,18);break;
                case PhoneGlyphKind.More: Circle(12,5,1);Circle(12,12,1);Circle(12,19,1);break;
                case PhoneGlyphKind.Sun: Circle(12,12,4);for(var i=0;i<8;i++)Radial(12,12,7,10,i*45);break;
                case PhoneGlyphKind.Wifi: Arc(12,19,15,225,315);Arc(12,19,10,225,315);Arc(12,19,5,225,315);Circle(12,19,.7f);break;
                case PhoneGlyphKind.Battery: Path(3,7,20,7,20,17,3,17,3,7);Line(22,10,22,14);Line(6,10,16,10);Line(6,14,16,14);break;
                case PhoneGlyphKind.Signal: for(var i=0;i<4;i++)Line(5+i*4,19,5+i*4,16-i*4);break;
                case PhoneGlyphKind.Plus: Line(12,5,12,19);Line(5,12,19,12);break;
                case PhoneGlyphKind.Move: Line(12,3,12,21);Line(3,12,21,12);Path(8,7,12,3,16,7);Path(8,17,12,21,16,17);Path(7,8,3,12,7,16);Path(17,8,21,12,17,16);break;
                case PhoneGlyphKind.Rotate: case PhoneGlyphKind.Reset: Arc(12,12,8,20,315);Path(12,3,18,3,18,9);break;
                case PhoneGlyphKind.Check: Path(4,12,9,17,20,6);break;
                case PhoneGlyphKind.Copy: Path(8,8,20,8,20,21,8,21,8,8);Path(5,17,3,17,3,3,16,3,16,5);break;
                case PhoneGlyphKind.Heart: Path(12,21,3,12,3,6,6,3,9,3,12,6,15,3,18,3,21,6,21,12,12,21);break;
                case PhoneGlyphKind.Phone: Path(6,2,18,2,18,22,6,22,6,2);Line(10,5,14,5);Line(10,19,14,19);break;
            }
        }
        private Vector2 Point(float x,float y)
        { var r=rectTransform.rect;return new Vector2(r.xMin+x/24*r.width,r.yMax-y/24*r.height); }
        private void Line(float x1,float y1,float x2,float y2)
        {
            var a=Point(x1,y1);var b=Point(x2,y2);var d=b-a;
            var n=new Vector2(-d.y,d.x).normalized*stroke*rectTransform.rect.width/48;
            var i=mesh.currentVertCount;
            mesh.AddVert(a-n,color,Vector2.zero);mesh.AddVert(a+n,color,Vector2.zero);
            mesh.AddVert(b+n,color,Vector2.zero);mesh.AddVert(b-n,color,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
        private void Path(params float[] p) { for(var i=2;i<p.Length;i+=2)Line(p[i-2],p[i-1],p[i],p[i+1]); }
        private void Circle(float x,float y,float r)=>Arc(x,y,r,0,360);
        private void Arc(float x,float y,float r,float start,float end)
        { for(var i=0;i<32;i++){var a=Mathf.Lerp(start,end,i/32f)*Mathf.Deg2Rad;var b=Mathf.Lerp(start,end,(i+1)/32f)*Mathf.Deg2Rad;Line(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r,x+Mathf.Cos(b)*r,y+Mathf.Sin(b)*r);} }
        private void Radial(float x,float y,float a,float b,float degrees)
        {var v=new Vector2(Mathf.Cos(degrees*Mathf.Deg2Rad),Mathf.Sin(degrees*Mathf.Deg2Rad));Line(x+v.x*a,y+v.y*a,x+v.x*b,y+v.y*b);}
    }
}
