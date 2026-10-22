#if UNITY_EDITOR
using FishSocial.Desktop.Auth;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    /// <summary>Original prototype examples supplied as API DTOs, rendered by the production controller.</summary>
    public static class CozyProfileRefinementPreview
    {
        public static void FillEdit(GameObject page, string state = "avatars")
        {
            var controller = page.GetComponent<DesktopProfileEditPanel>();
            if (controller == null) return;
            var ids = new[] { "koi", "mandarin", "grass_carp", "golden_line", "carp", "crucian", "bream", "silver_carp" };
            var sizes = new[] { .62f, .38f, .84f, .31f, .45f, .23f, .34f, .51f };
            var fish = new FishInventoryItemDto[ids.Length];
            for (var i=0;i<ids.Length;i++) fish[i]=new FishInventoryItemDto { id="workspace-fish-"+i, speciesId=ids[i], sizeM=sizes[i], quality=i==0?"red":"purple", caughtAt=1792654938000-i*660000, pondId="pond-calm" };
            var profile = new PlayerProfileDto {playerId="workspace-preview",nickname="小橘",bio="喜欢晴天和湖水，也喜欢等一条鱼上钩。",avatarUrl="/image/profile/cat_avatar_orange.png",showcaseFishIds=new[]{fish[0].id,fish[3].id,fish[1].id},createdAt=1790294400000};
            controller.PresentSnapshot(profile,fish,state!="avatars",state=="picker"?0:-1);
            CozyItemArt.Text(page.transform,"JournalEditHeading","编辑我的手账");
        }

        public static void Fill(GameObject page,string tab)
        {
            if(!page.name.StartsWith("PanelProfileHub")&&!page.name.StartsWith("PanelAchievements"))return;
            var ids=new[]{"koi","golden_line","crucian","carp","grass_carp","black_bass","mandarin","silver_carp","bream","catfish","snakehead","yellow_croaker"};
            var sizes=new[]{.62f,.31f,.21f,.58f,.86f,.44f,.38f,.72f,.29f,.66f,.53f,.35f};
            var codex=new FishCodexEntryDto[ids.Length];
            for(var i=0;i<ids.Length;i++)codex[i]=new FishCodexEntryDto{speciesId=ids[i],maxSizeM=sizes[i],totalCaught=i==0?7:5,firstCaughtAt=1791907200000,lastCaughtAt=1792512000000};
            var fish=new FishInventoryItemDto[8];var positions=new[]{0,1,6};
            for(var i=0;i<positions.Length;i++){var index=positions[i];fish[i]=new FishInventoryItemDto{id="preview-fish-"+i,speciesId=ids[index],quality=i==0?"red":"purple",sizeM=sizes[index],caughtAt=1792654938000-i*660000,pondId="pond-calm"};}
            var titles=new[]{"初次上钩","锦鲤附体","百鱼斩","湖泊旅人","早起的猫","满载而归"};
            var desc=new[]{"钓到第一条鱼","钓到一条锦鲤","累计钓到 100 条鱼","去过 5 个湖泊","清晨钓到 10 条鱼","一次卖出 1000 金币"};
            var icons=new[]{"hook","star","fish","map","sun","coin"};
            var achievements=new AchievementViewDto[titles.Length];
            for(var i=0;i<titles.Length;i++)achievements[i]=new AchievementViewDto{achievementId="a"+(i+1),name=titles[i],desc=desc[i],iconKey=icons[i],category="catch",unlocked=i==0};
            var hub=new ProfileHubDto{isSelf=true,canEdit=true,profile=new ProfileHubProfileDto{playerId="self",nickname="小橘",avatarUrl="/image/profile/cat_avatar_orange.png",bio="喜欢晴天和湖水，也喜欢等一条鱼上钩。",showcaseFishIds=new[]{"preview-fish-0","preview-fish-1","preview-fish-2"},shareVisibility="public"},progress=new ProfileHubProgressDto{level=7,xp=320},codexSummary=new ProfileHubCodexSummaryDto{unlockedCount=12,totalSpecies=DesktopGameData.Species.Length},showcaseFish=fish,achievements=achievements};
            if(tab=="相册")return;
            var selected=tab=="展示柜"?DesktopProfileHubPanel.HubTab.Showcase:tab=="图鉴"?DesktopProfileHubPanel.HubTab.Codex:tab=="成就"||page.name.StartsWith("PanelAchievements")?DesktopProfileHubPanel.HubTab.Achievements:DesktopProfileHubPanel.HubTab.Profile;
            page.GetComponent<DesktopProfileHubPanel>().PresentSnapshot(hub,codex,selected);
            foreach(var progress in page.GetComponentsInChildren<CozyJournalProgressFill>(true))progress.Refresh();
        }
    }
}
#endif
