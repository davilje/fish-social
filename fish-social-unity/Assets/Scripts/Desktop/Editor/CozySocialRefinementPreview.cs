#if UNITY_EDITOR
using FishSocial.Desktop.Auth;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    /// <summary>Feeds fixtures through the same selection and comment presenter as live server responses.</summary>
    public static class CozySocialRefinementPreview
    {
        public static void Fill(GameObject page)
        {
            var feed=page.GetComponent<DesktopSocialFeedPanel>();
            if(feed==null)return;
            var card=page.GetComponentInChildren<DesktopSocialPostCard>(true);
            if(card==null)return;
            feed.PresentSelectedComments(card,new[]{new PostCommentDto
            {id="preview-comment-1",playerId="preview-bai",nickname="小白",text="这条锦鲤真漂亮！下次一起去千岛湖吧。"}},1);
            foreach(var motion in page.GetComponentsInChildren<CozyStickerMotion>(true))motion.ApplyFinalPose();
            CozyJournalInlineContent.ReflowTree(page.transform);
        }
    }
}
#endif
