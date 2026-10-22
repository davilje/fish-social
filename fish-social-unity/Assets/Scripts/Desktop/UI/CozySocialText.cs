using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>CSS line-height is measured in pixels; Unity's lineSpacing multiplies the font's own metrics.</summary>
    public static class CozySocialText
    {
        public static float Apply(Text text,int fontSize,float lineHeight,float width)
        {
            if(text==null)return lineHeight;
            text.fontSize=fontSize;text.resizeTextForBestFit=false;text.supportRichText=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
            var settings=text.GetGenerationSettings(new Vector2(Mathf.Max(1,width),0));
            settings.lineSpacing=1;settings.verticalOverflow=VerticalWrapMode.Overflow;
            var generator=new TextGenerator();
            var one=generator.GetPreferredHeight("鱼",settings);
            var two=generator.GetPreferredHeight("鱼\n鱼",settings);
            var pixel=text.pixelsPerUnit;
            var baseline=(two-one)/Mathf.Max(.0001f,pixel);
            text.lineSpacing=baseline>.01f?lineHeight/baseline:1;
            settings=text.GetGenerationSettings(new Vector2(Mathf.Max(1,width),0));
            settings.verticalOverflow=VerticalWrapMode.Overflow;
            generator.Populate(text.text??"",settings);
            var lines=Mathf.Max(1,generator.lineCount);
            return lines*lineHeight;
        }
    }
}
