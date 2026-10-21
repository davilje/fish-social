using UnityEngine;
using UnityEngine.EventSystems;

namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2MapViewport : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler,IScrollHandler
    {
        [SerializeField] RectTransform viewport,content;
        Vector2 dragPoint,dragOrigin,from,to;
        float zoom=1,fromZoom=1,toZoom=1,elapsed=.35f;
        bool dragging;
        public Vector2 Position => content!=null?content.anchoredPosition:Vector2.zero;
        public float Zoom => zoom;
        public void Configure(RectTransform view,RectTransform artwork){viewport=view;content=artwork;zoom=content.localScale.x;Clamp();}
        public void ResetView(){elapsed=.35f;zoom=1;content.localScale=Vector3.one;content.anchoredPosition=new Vector2(-180,140);Clamp();}
        public void Focus(float x,float y)
        {
            from=content.anchoredPosition;fromZoom=zoom;toZoom=1.15f;to=ClampPosition(new Vector2(viewport.rect.width*.5f-x*1050*toZoom,y*700*toZoom-viewport.rect.height*.5f),toZoom);elapsed=0;
            if(JournalV2MotionState.Immediate)Advance(.35f);
        }
        public void Advance(float delta)
        {
            if(JournalV2MotionState.Paused||content==null||elapsed>=.35f)return;
            elapsed=Mathf.Min(.35f,elapsed+Mathf.Max(0,delta));var t=JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic,elapsed/.35f);
            zoom=Mathf.LerpUnclamped(fromZoom,toZoom,t);content.localScale=Vector3.one*zoom;content.anchoredPosition=Vector2.LerpUnclamped(from,to,t);Clamp();
        }
        void Update(){Advance(Time.unscaledDeltaTime);}
        Vector2 ClampPosition(Vector2 value,float scale)
        {return new Vector2(Mathf.Clamp(value.x,Mathf.Min(0,viewport.rect.width-1050*scale),0),Mathf.Clamp(value.y,0,Mathf.Max(0,700*scale-viewport.rect.height)));}
        void Clamp(){content.anchoredPosition=ClampPosition(content.anchoredPosition,zoom);}
        public void OnBeginDrag(PointerEventData data)
        {
            if(data.pointerPress!=null&&data.pointerPress.transform.IsChildOf(content)&&data.pointerPress.GetComponent<UnityEngine.UI.Button>()!=null)return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,data.position,data.pressEventCamera,out dragPoint))return;
            dragging=true;elapsed=.35f;dragOrigin=content.anchoredPosition;
        }
        public void OnDrag(PointerEventData data)
        {if(!dragging)return;if(RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,data.position,data.pressEventCamera,out var point)){content.anchoredPosition=dragOrigin+point-dragPoint;Clamp();}}
        public void OnEndDrag(PointerEventData data){dragging=false;}
        public void OnScroll(PointerEventData data)
        {
            elapsed=.35f;dragging=false;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,data.position,data.enterEventCamera,out var point))return;
            var previous=zoom;var minimum=Mathf.Max(.65f,viewport.rect.width/1050,viewport.rect.height/700);
            zoom=Mathf.Clamp(zoom*Mathf.Pow(1.12f,data.scrollDelta.y),minimum,1.8f);content.anchoredPosition=point-(point-content.anchoredPosition)*(zoom/previous);content.localScale=Vector3.one*zoom;Clamp();
        }
        void OnDisable(){dragging=false;elapsed=.35f;}
    }
}
