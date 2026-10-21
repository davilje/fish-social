using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public static class JournalV2Shell
    {
        public static GameObject Build()
        {
            var root=new GameObject("JournalV2Shell",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(JournalV2App));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1672,941);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var stage=JournalV2Nodes.Rect(root.transform,"Stage",0,0,1672,941);stage.anchorMin=stage.anchorMax=stage.pivot=new Vector2(.5f,.5f);stage.anchoredPosition=Vector2.zero;
            JournalV2Nodes.Image(stage,"Background",JournalV2Theme.Sprite("scenes/home-sakura"),0,0,1672,941);
            var header=JournalV2Nodes.Rect(stage,"Header",0,0,1672,140);
            var profile=JournalV2Nodes.Button(header,"Profile","",null,33,29,415,103);
            var pv=profile.transform.Find("Visual");
            pv.Find("Label").gameObject.SetActive(false);
            JournalV2Nodes.Image(pv,"Avatar",JournalV2Theme.Sprite("characters/avatars/orange"),14,10.5f,82,82);
            JournalV2Nodes.Text(pv,"Name","钓友",110,18,201,34,26);
            var level=JournalV2Nodes.Plate(pv,"LevelBadge",176,24,70,30,JournalV2Theme.Green,9,0);level.ShadowColor=Color.clear;
            JournalV2Nodes.Text(pv,"Level","Lv.1",176,24,70,30,20,TextAnchor.MiddleCenter).color=Color.white;
            var xp=JournalV2Nodes.Plate(pv,"Experience",110,64,285,18,new Color32(234,224,202,255),9,0);xp.ShadowColor=Color.clear;
            JournalV2Nodes.Text(pv,"ExperienceLabel","",110,64,285,18,13,TextAnchor.MiddleCenter);
            var world=JournalV2Nodes.Button(header,"World","钓鱼世界","map",562,29,320,76);
            var wv=world.transform.Find("Visual");Position(wv.Find("Icon"),22,18,40,40);Position(wv.Find("Label"),78,9,190,34);
            wv.Find("Label").GetComponent<Text>().fontSize=25;wv.Find("Label").GetComponent<Text>().alignment=TextAnchor.MiddleLeft;
            JournalV2Nodes.Text(wv,"Hint","探索地图 · 选择鱼塘",78,46,210,18,13).color=JournalV2Theme.Soft;
            JournalV2Nodes.Text(wv,"Arrow","↗",270,18,28,40,28).font=Resources.GetBuiltinResource<Font>("Arial.ttf");
            var coins=JournalV2Nodes.Button(header,"Coins","—","coin",983,29,235,64);
            Position(coins.transform.Find("Visual/Icon"),16,12,40,40);Position(coins.transform.Find("Visual/Label"),66,10,112,44);coins.GetComponentInChildren<Text>().fontSize=28;
            JournalV2Nodes.Text(coins.transform.Find("Visual"),"Plus","+",185,13,38,38,26,TextAnchor.MiddleCenter);
            var fish=JournalV2Nodes.Button(header,"Catch","—","fish",1237,29,216,64);
            Position(fish.transform.Find("Visual/Icon"),16,12,40,40);Position(fish.transform.Find("Visual/Label"),66,10,90,44);fish.GetComponentInChildren<Text>().fontSize=28;
            JournalV2Nodes.Text(fish.transform.Find("Visual"),"Hint","鱼获",166,20,36,24,16);
            foreach(var pair in new[]{new[]{"Mail","bell"},new[]{"Settings","settings"}})
            {
                var button=JournalV2Nodes.Button(header,pair[0],"",pair[1],pair[0]=="Mail"?1472:1564,29,76,64);
                Position(button.transform.Find("Visual/Icon"),18,12,40,40);
            }
            JournalV2Nodes.Rect(stage,"PageHost",0,0,1672,941);
            var nav=JournalV2Nodes.Rect(stage,"Nav",320,807,1032,108);
            var dock=JournalV2Nodes.Plate(nav,"Paper",0,0,1032,108,new Color32(255,249,237,255),20,2);dock.ShadowOffset=new Vector2(0,-6);
            JournalV2Nodes.Image(nav,"Stitch",JournalV2Theme.Sprite("decor/dock-stitch"),18,96,996,2);
            var ids=new[]{"bag","equip","shop","ach","rank","profile","feed"};var labels=new[]{"背包","装备","商店","成就","排行榜","个人信息","钓友动态"};var icons=new[]{"bag","rod","shop","medal","rank","user","social"};
            for(var i=0;i<ids.Length;i++)
            {
                JournalV2Nodes.Button(nav,ids[i],labels[i],icons[i],2+i*146.857f,2,146.857f,104,JournalV2ButtonKind.Nav);
                if(i>0){var line=JournalV2Nodes.Plate(nav,"Divider"+i,i*146.857f+1,24,2,60,new Color(117/255f,71/255f,46/255f,.08f),0,0);line.ShadowColor=Color.clear;}
            }
            var catSprite=JournalV2Theme.Sprite("illustrations/cat-fishing");
            JournalV2Nodes.Image(stage,"CornerCat",catSprite,1434,945-250*catSprite.rect.height/catSprite.rect.width,250,250*catSprite.rect.height/catSprite.rect.width);
            JournalV2Nodes.Rect(stage,"Effects",0,0,1672,941);
            var modal=JournalV2Nodes.Rect(stage,"ModalHost",0,0,1672,941);
            var dim=JournalV2Nodes.Plate(modal,"Shade",0,0,1672,941,new Color(84/255f,54/255f,31/255f,.4f),0,0);dim.ShadowColor=Color.clear;dim.raycastTarget=true;modal.gameObject.SetActive(false);
            var toast=JournalV2Nodes.Rect(stage,"Toast",1212,150,430,100);
            JournalV2Nodes.Plate(toast,"Paper",0,0,430,100,JournalV2Theme.Paper,18,2);
            JournalV2Nodes.Text(toast,"Message","",20,12,390,76,20);toast.gameObject.SetActive(false);
            var login=JournalV2Nodes.Rect(stage,"Login",536,240,600,450);
            JournalV2Nodes.Plate(login,"Paper",0,0,600,450,JournalV2Theme.Paper,22,2);
            JournalV2Nodes.Text(login,"Title","猫咪钓鱼手账",40,40,520,70,36,TextAnchor.MiddleCenter);
            JournalV2Nodes.Text(login,"Status","通过 Steam 登录，继续你的钓鱼手账。",40,150,520,90,22,TextAnchor.MiddleCenter);
            JournalV2Nodes.Button(login,"Connect","通过 Steam 登录","user",120,300,360,58,JournalV2ButtonKind.Primary);
            login.gameObject.SetActive(false);
            return root;
        }
        static void Position(Transform node,float x,float y,float w,float h)
        {var r=(RectTransform)node;r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    }
}
