using System;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>workspace.js: 240ms fade/up 12px, sections staggered by 40ms.
    /// Sibling decorations share their section's clock without reparenting layout or hit areas.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyWorkspaceBodyMotion : MonoBehaviour
    {
        [Serializable] public sealed class Section { public Transform[] roots; }
        [SerializeField] Section[] sections;
        float elapsed;
        bool playing;
        public void Configure(params Transform[][] roots)
        {
            Finish();
            sections = new Section[roots.Length];
            for (var i=0;i<roots.Length;i++) sections[i]=new Section {roots=roots[i]};
        }
        public void PlayEntrance() { elapsed=0; playing=true; Sample(0); }
        public void Sample(float seconds)
        {
            if (sections == null) return;
            for (var i=0;i<sections.Length;i++)
            {
                var t=CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic,
                    Mathf.Clamp01((seconds-(CozyMotion.Reduced?0:i*.04f))/(CozyMotion.Reduced?.12f:.24f)));
                foreach(var node in sections[i].roots)
                {
                    if(node==null)continue;
                    foreach(var graphic in node.GetComponentsInChildren<Graphic>(true))
                    {
                        var effect=graphic.GetComponent<CozyWorkspaceBodyGraphic>()??graphic.gameObject.AddComponent<CozyWorkspaceBodyGraphic>();
                        effect.Sample(transform,CozyMotion.Reduced?0:-12*(1-t),t);
                    }
                    foreach(var progress in node.GetComponentsInChildren<CozyJournalProgressFill>(true))
                        progress.SampleEntrance(seconds);
                }
            }
        }
        public void Advance(float seconds)
        {
            if(!playing||CozyMotion.Paused||CozyMotion.EntranceHeld)return;
            elapsed+=Mathf.Max(0,seconds);Sample(elapsed);
            if(elapsed>=.7f)playing=false;
        }
        public void Finish() { playing=false;elapsed=1;Sample(1); }
        void Update(){Advance(Time.unscaledDeltaTime);}
        // Bind graphics created asynchronously after this frame's Update to the same pose.
        void LateUpdate(){if(playing)Sample(elapsed);}
        void OnEnable()
        {
            CozyMotion.ReducedChanged+=Settle;CozyMotion.PausedChanged+=Settle;
            var owner=GetComponentInParent<CozyJournalFlipPose>();
            if(owner!=null&&owner.TryGetEntranceElapsed(out var age)){PlayEntrance();Advance(age);}
        }
        void OnDisable(){CozyMotion.ReducedChanged-=Settle;CozyMotion.PausedChanged-=Settle;Finish();}
        void Settle(bool value){if(value)Finish();}
    }

    public sealed class CozyWorkspaceBodyGraphic : BaseMeshEffect
    {
        Vector3 offset;
        float alpha=1;
        public float Opacity=>alpha;
        public Vector3 Offset=>offset;
        public void Sample(Transform owner,float y,float opacity)
        {
            offset=transform.InverseTransformVector(owner.TransformVector(new Vector3(0,y,0)));
            alpha=opacity;if(graphic!=null)graphic.SetVerticesDirty();
        }
        public override void ModifyMesh(VertexHelper helper)
        {
            if(!IsActive())return;
            var v=new UIVertex();
            for(var i=0;i<helper.currentVertCount;i++)
            {helper.PopulateUIVertex(ref v,i);v.position+=offset;v.color.a=(byte)Mathf.RoundToInt(v.color.a*alpha);helper.SetUIVertex(v,i);}
        }
    }
}
