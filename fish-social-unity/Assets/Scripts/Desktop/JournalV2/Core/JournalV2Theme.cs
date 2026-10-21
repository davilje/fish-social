using UnityEngine;

namespace FishSocial.Desktop.JournalV2
{
    public static class JournalV2Theme
    {
        public static readonly Color Ink=new Color32(117,71,46,255);
        public static readonly Color Soft=new Color(117/255f,71/255f,46/255f,.68f);
        public static readonly Color Coral=new Color32(252,146,60,255);
        public static readonly Color Paper=new Color32(255,247,232,255);
        public static readonly Color Line=new Color32(232,214,182,255);
        public static readonly Color ArtWell=new Color32(251,243,228,255);
        public static readonly Color Blue=new Color32(93,173,226,255);
        public static readonly Color Green=new Color32(107,184,107,255);
        public static readonly Color Gold=new Color32(242,191,71,255);
        static Font font;
        public static Font Font
        {
            get
            {
                if(font==null)
                {
                    var candidates=Resources.LoadAll<Font>("Desktop/Theme/Cozy/fonts");
                    font=candidates.Length>0?candidates[0]:Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return font;
            }
        }
        public static Sprite Sprite(string path)
        {
            if(string.IsNullOrEmpty(path)) return null;
            path=path.Replace('\\','/');
            if(path.EndsWith(".png")||path.EndsWith(".jpg")) path=path.Substring(0,path.Length-4);
            return Resources.Load<Sprite>("Desktop/Theme/Cozy/"+path)??Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/"+path);
        }
    }
}
