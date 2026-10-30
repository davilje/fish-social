using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using FishSocial.Motion;

namespace FishSocialOverlay
{
    // Frozen vector meshes share the decoded source bitmap, not newly rasterized PNGs.
    // Global bounded LRU prevents memory growing with sessions or character switches.
    internal static class OverlayFishingRestDrawing
    {
        const int Capacity = 128;
        sealed class Entry { public ImageSource Source, Result; public int Step; }
        static readonly LinkedList<Entry> Cache = new LinkedList<Entry>();
        public static ImageSource Warp(ImageSource source, double nod)
        {
            if (source == null || nod < .0001) return source;
            var step = (int)Math.Round(Math.Max(0, Math.Min(1, nod)) * 48);
            if (step == 0) return source;
            for (var node = Cache.First; node != null; node = node.Next)
                if (ReferenceEquals(node.Value.Source, source) && node.Value.Step == step)
                {
                    var result = node.Value.Result; Cache.Remove(node); Cache.AddFirst(node); return result;
                }
            var group = new DrawingGroup { ClipGeometry = new RectangleGeometry(new Rect(0,0,640,512)) };
            // Preserve the full logical canvas bounds even when edge pixels are empty.
            group.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,640,512))));
            var amount = step / 48d;
            for (var y = 0; y < 512; y += 32)
                for (var x = 0; x < 640; x += 64)
                {
                    var p = new Point(x,y); var q = new Point(x+64,y);
                    var r = new Point(x+64,y+32); var s = new Point(x,y+32);
                    Add(group,source,p,q,r,amount); Add(group,source,p,r,s,amount);
                }
            group.Freeze();
            var image = new DrawingImage(group); image.Freeze();
            Cache.AddFirst(new Entry { Source=source, Step=step, Result=image });
            while (Cache.Count > Capacity) Cache.RemoveLast();
            return image;
        }
        static Point Map(Point p,double amount)
        { FishingRestGeometry.Point(p.X,p.Y,amount,out var x,out var y);return new Point(x,y); }
        static void Add(DrawingGroup group,ImageSource image,Point p,Point q,Point r,double amount)
        {
            var u=Map(p,amount);var v=Map(q,amount);var w=Map(r,amount);
            var a=q.X-p.X;var b=q.Y-p.Y;var c=r.X-p.X;var d=r.Y-p.Y;var determinant=a*d-b*c;
            var x=v.X-u.X;var y=v.Y-u.Y;var z=w.X-u.X;var h=w.Y-u.Y;
            var xx=(x*d-z*b)/determinant;var yx=(y*d-h*b)/determinant;
            var xy=(z*a-x*c)/determinant;var yy=(h*a-y*c)/determinant;
            var brush=new ImageBrush(image) { ViewportUnits=BrushMappingMode.Absolute,
                Viewport=new Rect(0,0,640,512), Stretch=Stretch.Fill,
                Transform=new MatrixTransform(xx,yx,xy,yy,u.X-xx*p.X-xy*p.Y,u.Y-yx*p.X-yy*p.Y) };
            var geometry=new StreamGeometry();
            using(var context=geometry.Open())
            { context.BeginFigure(u,true,true);context.LineTo(v,true,false);context.LineTo(w,true,false); }
            group.Children.Add(new GeometryDrawing(brush,null,geometry));
        }
    }
}
