using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CompactActionsVerification
{
    const string Dir="/Users/red/Desktop/fish/output/compact-actions-2026-10-30/";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks; static List<string> failures=new List<string>();static List<string> notes=new List<string>();static string languageBefore;
    [Serializable] class Rods { public DesktopGameData.RodDef[] items; }
    [Serializable] class Vessels { public ShopVesselDto[] items; }
    static void Check(bool ok,string label){checks++;if(!ok)failures.Add(label);}
    static Text T(GameObject p,string path)=>p.transform.Find(path).GetComponent<Text>();
    static Button B(GameObject p,string path)=>p.transform.Find(path).GetComponent<Button>();
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
    static object Get(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
    static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
    [InitializeOnLoadMethod] static void Resume(){if(!SessionState.GetBool("CompactActionsVerify",false))return;EditorApplication.playModeStateChanged+=state=>{if(state!=PlayModeStateChange.EnteredPlayMode)return;SessionState.SetBool("CompactActionsVerify",false);EditorApplication.delayCall+=Run;};}
    public static void Start(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("CompactActionsVerify",true);EditorApplication.isPlaying=true;}
    static void Run(){var stack=new Stack<IEnumerator>();stack.Push(Frames());EditorApplication.CallbackFunction tick=null;tick=()=>{try{while(stack.Count>0){var frame=stack.Peek();if(!frame.MoveNext()){stack.Pop();continue;}if(frame.Current is IEnumerator nested){stack.Push(nested);continue;}return;}EditorApplication.update-=tick;}catch(Exception e){File.WriteAllText(Dir+"exception.txt",e+"\n"+string.Join("\n",failures));if(languageBefore!=null)DesktopLocalization.SetLanguage(languageBefore);EditorApplication.update-=tick;EditorApplication.Exit(1);}};EditorApplication.update+=tick;}
    static IEnumerator Pause(float seconds=.4f){var end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
    static IEnumerator Frames(){
        languageBefore=DesktopLocalization.Language;
        var root=new GameObject("VerificationCanvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));root.GetComponent<RectTransform>().sizeDelta=new Vector2(1440,900);
        var events=new GameObject("Events",typeof(EventSystem),typeof(StandaloneInputModule));
        foreach(var language in new[]{"zh-CN","en-US"}){
            DesktopLocalization.SetLanguage(language);
            foreach(var name in new[]{"PanelShop","PanelEquipment","PanelCatch"}){
                var page=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/"+name),root.transform,false);DesktopLocalization.BindPrefab(page,"Desktop/Prefabs/"+name);
                if(name=="PanelCatch") {var bag=page.GetComponent<DesktopCatchBagModalView>();bag.Bind(null,null);Call(bag,"OnInventory",(object)new[]{new FishInventoryItemDto{id="f",speciesId="koi",quality="red",sizeM=1.28f}});}
                else {var shop=page.GetComponent<DesktopShopPanel>();shop.SetEquipmentMode(name=="PanelEquipment");shop.Bind(null);var def=JsonUtility.FromJson<Rods>("{\"items\":"+Resources.Load<TextAsset>("GameData/rods").text+"}").items[0];var rod=JsonUtility.FromJson<ShopTackleDto>(JsonUtility.ToJson(def));rod.id=def.rodId;rod.price=def.priceGold;DesktopLocalization.BindData(rod);Set(shop,"_tackles",new[]{rod});Set(shop,"_gear",new ShopGearDto{playerLevel=10,equippedRod=rod.id,ownedRods=new[]{rod.id}});Set(shop,"_catalogReady",true);Call(shop,"Render");}
                var entry=B(page,"ItemDetails");entry.gameObject.SetActive(true);
                var label=T(page,"ItemDetails/Label");var visual=entry.GetComponent<CozyButtonVisual>();
                CozyItemDetailsDialog.Bind(page.transform);
                yield return Pause();
                Check(entry.GetComponent<RectTransform>().rect.width==96,"Details hit width");Check(label.preferredWidth<=label.rectTransform.rect.width+1,"Details label fits "+language);Check(label.fontStyle==FontStyle.Normal,"Details uses regular text");
                Check(Hit(page,"ItemDetails")==entry.gameObject,"Details receives pointer hits");
                foreach(var state in new[]{"default","hover","pressed","disabled"}){
                    State(entry,state);yield return Pause(.08f);Capture(page,name+"-"+language+"-"+state);Centered(entry);Check(label.cachedTextGenerator.lineCount==1 && label.cachedTextGenerator.characterCountVisible>=label.text.Length,"Details caption fully visible "+language+" "+state);
                    Check(entry.transform.Find("ActionIcon").GetComponent<CanvasRenderer>()!=null,"Native icon renderer exists");var wash=entry.transform.Find("CozyVisual/ButtonWash").GetComponent<CozyCssGraphic>();Check(wash!=null && wash.border==1,"Flat outlined details plate");
                    Check(entry.transform.Find("CozyVisual").GetComponent<Image>().sprite==null,"Details has no sliced pedestal");
                }
                State(entry,"default");entry.onClick.Invoke();Check(page.GetComponent<CozyItemDetailsDialog>().IsOpen,"Redesign retains dialog action");page.GetComponent<CozyItemDetailsDialog>().Close();
                UnityEngine.Object.Destroy(page);yield return null;
            }
            var feed=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/PanelSocialFeed"),root.transform,false);DesktopLocalization.BindPrefab(feed,"Desktop/Prefabs/PanelSocialFeed");
            var api=new CompactActionApi();var panel=feed.GetComponent<DesktopSocialFeedPanel>();panel.Bind(api,null);panel.OnOpened();yield return Pause(1f);
            var card=feed.GetComponentInChildren<DesktopSocialPostCard>();Check(card!=null,"Feed renders production card");
            card.PresentComments(CompactActionApi.Comments(),2,true);yield return Pause(.8f);
            var rows=feed.GetComponentsInChildren<Button>(true).Where(button=>button.name=="Delete" && button.gameObject.activeInHierarchy).ToArray();Check(rows.Length==2,"Post owner sees comment actions");
            foreach(var delete in rows){
                var row=delete.transform.parent;var author=row.Find("Author").GetComponent<Text>();
                Check(delete.GetComponent<RectTransform>().rect.width==88,"Delete hit width");Check(T(delete.gameObject,"Label").preferredWidth<=T(delete.gameObject,"Label").rectTransform.rect.width+1,"Delete label fits "+language);
                Check(author.rectTransform.rect.xMax<=delete.GetComponent<RectTransform>().rect.xMin+delete.transform.localPosition.x+1,"Author has room beside delete");
            }
            foreach(var state in new[]{"default","hover","pressed","disabled"}){
                foreach(var delete in rows)State(delete,state);yield return Pause(.08f);Capture(feed,"feed-"+language+"-"+state);foreach(var button in rows)Centered(button);foreach(var delete in rows){var caption=T(delete.gameObject,"Label");Check(caption.cachedTextGenerator.lineCount==1 && caption.cachedTextGenerator.characterCountVisible>=caption.text.Length,"Delete caption fully visible "+language+" "+state);}
                var wash=rows[0].transform.Find("CozyVisual/ButtonWash").GetComponent<CozyCssGraphic>();Check(state!="default" || wash.color.a==0,"Delete rests as quiet text action");
            }
            foreach(var delete in rows)State(delete,"default");
            rows[0].onClick.Invoke();yield return Pause(.1f);Check(api.Deletions==1,"Delete action still reaches API");Check(rows[0]==null||!rows[0].gameObject.activeInHierarchy,"Deleted comment disappears");
            card.Bind(new SocialPostDto{id="p",playerId="other",nickname="湖畔钓友",text="今天收获不错。"},api);card.PresentComments(CompactActionApi.Comments(),2,true);yield return Pause(.3f);
            Check(feed.GetComponentsInChildren<Button>().Count(button=>button.name=="Delete" && button.gameObject.activeInHierarchy)==1,"Only own comment can be deleted on other player's post");
            panel.OnClosed();UnityEngine.Object.Destroy(feed);yield return null;
        }
        DesktopLocalization.SetLanguage(languageBefore);File.WriteAllText(Dir+"verification.txt","Checks: "+checks+"\nFailures: "+failures.Count+"\n"+string.Join("\n",failures)+"\n"+string.Join("\n",notes));UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(events);yield return null;EditorApplication.Exit(failures.Count==0?0:1);
    }
    static void Centered(Button button){
        var rect=button.GetComponent<RectTransform>();var icon=button.transform.Find("ActionIcon") as RectTransform;var caption=T(button.gameObject,"Label");
        var right=caption.rectTransform.anchoredPosition.x+caption.rectTransform.rect.width*.5f+caption.preferredWidth*.5f;
        Check(Mathf.Abs((icon.anchoredPosition.x+right)*.5f-rect.rect.width*.5f)<=1,"Icon and text group centered");
        var textLeft=right-caption.preferredWidth;Check(Mathf.Abs(textLeft-icon.anchoredPosition.x-icon.rect.width-6f)<=1,"Consistent six-pixel icon gap");
        Check(Mathf.Abs(icon.anchoredPosition.y-icon.rect.height*.5f+rect.rect.height*.5f)<=1,"Icon vertically centered");
    }
    static void State(Button button,string state){
        var visual=button.GetComponent<CozyButtonVisual>();button.interactable=state!="disabled";
        Set(visual,"hovered",state=="hover"||state=="pressed");Set(visual,"focused",false);Set(visual,"pointerPressed",state=="pressed");Call(visual,"RefreshState");
    }
    static GameObject Hit(GameObject page,string path) {
        // Editor batch GameView stays at 640x480. A render camera gives both raycasting and
        // coordinate conversion the same 1440x900 viewport used for production-size captures.
        var canvas=page.GetComponentInParent<Canvas>();var cameraRoot=new GameObject("HitCamera",typeof(Camera));var camera=cameraRoot.GetComponent<Camera>();
        camera.orthographic=true;camera.orthographicSize=450;camera.nearClipPlane=.1f;camera.farClipPlane=100;
        var texture=new RenderTexture(1440,900,24);camera.targetTexture=texture;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();
        var rect=page.transform.Find(path) as RectTransform;var position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
        var data=new PointerEventData(EventSystem.current){position=position};var results=new List<RaycastResult>();EventSystem.current.RaycastAll(data,results);
        var hit=results.FirstOrDefault(result=>result.gameObject.transform.IsChildOf(page.transform));var clicked=hit.gameObject==null?null:ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
        notes.Add("Hit "+path+" point="+position+" handler="+(clicked!=null?clicked.name:"none"));
        UnityEngine.Object.DestroyImmediate(cameraRoot);UnityEngine.Object.DestroyImmediate(texture);return clicked;
    }
    static void Capture(GameObject panel,string name) {
        var canvas=panel.GetComponent<Canvas>();if(canvas==null)canvas=panel.GetComponentInParent<Canvas>();var cameraRoot=new GameObject("CaptureCamera",typeof(Camera));var camera=cameraRoot.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=450;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.94f,.91f,.85f);camera.nearClipPlane=.1f;camera.farClipPlane=100;
        var texture=new RenderTexture(1440,900,24);camera.targetTexture=texture;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();var prior=RenderTexture.active;RenderTexture.active=texture;var output=new Texture2D(1440,900,TextureFormat.RGB24,false);output.ReadPixels(new Rect(0,0,1440,900),0,0);output.Apply();File.WriteAllBytes(Dir+name+".png",output.EncodeToPNG());RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(output);UnityEngine.Object.DestroyImmediate(cameraRoot);UnityEngine.Object.DestroyImmediate(texture);
    }
}

public sealed class CompactActionApi : IAuthenticatedApiClient { public bool CanUse => true; public string PlayerId => "self"; public string BaseUrl => ""; public int PublicRequests; public int Deletions; public static PostCommentDto[] Comments()=>new[]{new PostCommentDto{id="c1",postId="p",playerId="self",nickname="湖畔钓友",text="这条锦鲤真漂亮，明天也来这里试试。"},new PostCommentDto{id="c2",postId="p",playerId="friend",nickname="小溪",text="恭喜！这个鱼塘的风景也很舒服。"}};
public IEnumerator GetInventory(Action<bool, string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetInventory");}
public IEnumerator GetInventoryItems(Action<bool, FishInventoryItemDto[], string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetInventoryItems");}
public IEnumerator GetCoins(Action<bool, int, string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetCoins");}
public IEnumerator GetCodex(Action<bool, FishCodexEntryDto[], string> onCompleted) {onCompleted(true, new FishCodexEntryDto[0], null); yield break;}
public IEnumerator GetFriends(Action<bool, FriendInfoDto[], string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetFriends");}
public IEnumerator GetFriendRequests(Action<bool, FriendRequestDto[], FriendRequestDto[], string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetFriendRequests");}
public IEnumerator AcceptFriendRequest(string requestId, Action<bool, string> onCompleted) {throw new InvalidOperationException("Unexpected API: AcceptFriendRequest");}
public IEnumerator RejectFriendRequest(string requestId, Action<bool, string> onCompleted) {throw new InvalidOperationException("Unexpected API: RejectFriendRequest");}
public IEnumerator RemoveFriend(string friendPlayerId, Action<bool, string> onCompleted) {throw new InvalidOperationException("Unexpected API: RemoveFriend");}
public IEnumerator GetConversations(Action<bool, DmConversationDto[], string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetConversations");}
public IEnumerator GetDirectMessages(string friendPlayerId, Action<bool, DirectMessageDto[], string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetDirectMessages");}
public IEnumerator SendDirectMessage(string toPlayerId, string fromNickname, string text, Action<bool, DirectMessageDto, string> onCompleted) {throw new InvalidOperationException("Unexpected API: SendDirectMessage");}
public IEnumerator SellFish(string fishId, Action<bool, int, int, FishInventoryItemDto[], string> onCompleted) {throw new InvalidOperationException("Unexpected API: SellFish");}
public IEnumerator ReturnFishToPond(
            string inventoryItemId,
            Action<bool, int, int, int, float, float, int, FishInventoryItemDto[], string> onCompleted) {throw new InvalidOperationException("Unexpected API: ReturnFishToPond");}
public IEnumerator ShareFish(string fishId, string nickname, Action<bool, string> onCompleted) {throw new InvalidOperationException("Unexpected API: ShareFish");}
public IEnumerator ShareFish(string fishId, string nickname, string visibility, string text, Action<bool, string> onCompleted) {throw new InvalidOperationException("Unexpected API: ShareFish");}
public IEnumerator GetShopCatalog(Action<bool, ShopBaitDto[], ShopTackleDto[], ShopVesselDto[], string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetShopCatalog");}
public IEnumerator GetShopGear(Action<bool, ShopGearDto, int, string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetShopGear");}
public IEnumerator BuyBait(string baitId, int quantity, Action<bool, ShopGearDto, int, string> onCompleted) {throw new InvalidOperationException("Unexpected API: BuyBait");}
public IEnumerator BuyTackle(string tackleId, Action<bool, ShopGearDto, int, string> onCompleted) {throw new InvalidOperationException("Unexpected API: BuyTackle");}
public IEnumerator BuyVessel(string vesselId, Action<bool, ShopGearDto, int, string> onCompleted) {throw new InvalidOperationException("Unexpected API: BuyVessel");}
public IEnumerator EquipBait(string baitId, Action<bool, ShopGearDto, string> onCompleted) {throw new InvalidOperationException("Unexpected API: EquipBait");}
public IEnumerator EquipTackle(string tackleId, Action<bool, ShopGearDto, string> onCompleted) {throw new InvalidOperationException("Unexpected API: EquipTackle");}
public IEnumerator GetPlayerProfile(Action<bool, PlayerProfileDto, string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetPlayerProfile");}
public IEnumerator GetProfileHub(
            string playerId, string tab,
            Action<bool, ProfileHubDto, string> onCompleted) {onCompleted(true, null, null); yield break;}
public IEnumerator ChangeAlbumPin(
            string action, string candidateOrPinId,
            Action<bool, AlbumCardDto[], string> onCompleted) {throw new InvalidOperationException("Unexpected API: ChangeAlbumPin");}
public IEnumerator GetPublicPlayerView(
            string playerId, int limit,
            Action<bool, PublicPlayerViewDto, string> onCompleted) {PublicRequests++; onCompleted(false, null, "Unexpected profile load"); yield break;}
public IEnumerator SendFriendRequest(
            string toPlayerId, string fromNickname,
            Action<bool, string> onCompleted) {throw new InvalidOperationException("Unexpected API: SendFriendRequest");}
public IEnumerator UpdatePlayerProfile(
            string nickname, string bio, string avatarUrl,
            Action<bool, PlayerProfileDto, string> onCompleted) {throw new InvalidOperationException("Unexpected API: UpdatePlayerProfile");}
public IEnumerator SetShowcase(string[] slots, Action<bool, PlayerProfileDto, string> onCompleted) {throw new InvalidOperationException("Unexpected API: SetShowcase");}
public IEnumerator GetSocialFeed(
            bool friendsOnly, int limit, int offset,
            Action<bool, SocialPostDto[], string> onCompleted) {onCompleted(true,new[]{new SocialPostDto{id="p",playerId="self",nickname="湖畔钓友",text="收获一条漂亮的锦鲤，分享今天的好心情。",fish=new FishInventoryItemDto{speciesId="koi",quality="red",sizeM=1.28f},commentCount=2}},null);yield break;}
public IEnumerator GetMySocialFeed(int limit, int offset, Action<bool, SocialPostDto[], string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetMySocialFeed");}
public IEnumerator GetPostComments(string postId, Action<bool, PostCommentDto[], int, string> onCompleted) {onCompleted(true,Comments(),2,null);yield break;}
public IEnumerator TogglePostLike(string postId, Action<bool, bool, int, string> onCompleted) {throw new InvalidOperationException("Unexpected API: TogglePostLike");}
public IEnumerator AddPostComment(string postId, string text, Action<bool, PostCommentDto, int, string> onCompleted) {throw new InvalidOperationException("Unexpected API: AddPostComment");}
public IEnumerator DeletePostComment(string postId, string commentId, Action<bool, int, string> onCompleted) {Deletions++;onCompleted(true,1,null);yield break;}
public IEnumerator GetLeaderboard(
            string boardType, string pondId, int limit,
            Action<bool, LeaderboardEntryDto[], string, string> onCompleted) {onCompleted(true, new LeaderboardEntryDto[0], "fixture", null); yield break;}
public IEnumerator GetMyLeaderboardRank(
            string boardType, string pondId,
            Action<bool, LeaderboardMyRankDto, string> onCompleted) {onCompleted(true, new LeaderboardMyRankDto {hasRank = false}, null); yield break;}
public IEnumerator GetFishingProgress(Action<bool, FishingProgressDto, string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetFishingProgress");}
public IEnumerator CompleteOnboarding(Action<bool, FishingProgressDto, string> onCompleted) {throw new InvalidOperationException("Unexpected API: CompleteOnboarding");}
public IEnumerator ResetOnboarding(Action<bool, FishingProgressDto, string> onCompleted) {throw new InvalidOperationException("Unexpected API: ResetOnboarding");}
public IEnumerator TriggerDebugPoliceRaid(Action<bool, string> onCompleted) {throw new InvalidOperationException("Unexpected API: TriggerDebugPoliceRaid");}
public IEnumerator PostGameplayDebug(string action, Action<bool, string> onCompleted) {throw new InvalidOperationException("Unexpected API: PostGameplayDebug");}
public IEnumerator PostGameplayDebug(string action, string fishId, Action<bool, string> onCompleted) {throw new InvalidOperationException("Unexpected API: PostGameplayDebug");}
public IEnumerator PostGameplayDebug(string action, string fishId, Action<bool, string, bool> onCompleted) {throw new InvalidOperationException("Unexpected API: PostGameplayDebug");}
public IEnumerator GetGameplayDebugPondFish(
            string scope, Action<bool, NativeOverlayDebugFishDto[], string, string, string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetGameplayDebugPondFish");}
public IEnumerator GetGameplayDebugSpotStats(Action<bool, string, string, string> onCompleted) {throw new InvalidOperationException("Unexpected API: GetGameplayDebugSpotStats");}
}
