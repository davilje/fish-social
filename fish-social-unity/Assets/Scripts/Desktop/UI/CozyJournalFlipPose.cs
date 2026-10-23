using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>The web flips .content inside a stationary journal. Binding and hit transforms stay intact.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyJournalFlipPose : MonoBehaviour
    {
        public float ContentOffset { get; private set; }
        public float ContentAlpha { get; private set; } = 1;
        bool covered;
        public void CoverContent() { covered = true; Sample(0, 0); }
        // Include content arriving from an API while the sheet is still rising.
        void LateUpdate() { if (covered) Sample(0, 0); }
        public void Sample(float offset, float alpha, float titleOffset = 0, float titleAlpha = 1)
        {
            ContentOffset = offset; ContentAlpha = alpha;
            // The router slot only wraps the page prefab. Measuring from the slot treats the
            // whole sheet, rings included, as one sliding child.
            var root = PageRoot();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.transform == root) continue;
                var top = graphic.transform;
                while (top.parent != root && top.parent != null) top = top.parent;
                var n = top.name;
                if (n == "JournalShadow" || n == "JournalEdge" || n == "JournalDots" ||
                    n == "JournalGrain" || n == "JournalStitch" || n.StartsWith("JournalRing")) continue;
                var title = n == "DesignHeader" && graphic.transform.name == "Title";
                if (n == "DesignHeader" && !title) continue;
                var effect = graphic.GetComponent<CozyJournalFlipGraphic>();
                if (effect == null) effect = graphic.gameObject.AddComponent<CozyJournalFlipGraphic>();
                effect.Sample(root, title ? titleOffset : offset, title ? titleAlpha : alpha);
            }
        }

        Transform PageRoot()
        {
            if (IsJournalPage(transform)) return transform;
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (IsJournalPage(child)) return child;
            }
            return transform;
        }

        static bool IsJournalPage(Transform node)
        {
            return node != null && (node.Find("JournalShadow") != null || node.Find("DesignHeader") != null);
        }
        public void ResetPose() { covered = false; Sample(0, 1); }
        void OnDisable() { ResetPose(); }
    }

    public sealed class CozyJournalFlipGraphic : BaseMeshEffect
    {
        Vector3 translation;
        float opacity = 1;
        public void Sample(Transform owner, float x, float alpha)
        {
            translation = transform.InverseTransformVector(owner.TransformVector(new Vector3(x,0,0)));
            opacity = alpha;
            if (graphic != null) graphic.SetVerticesDirty();
        }
        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive()) return;
            var vertex = new UIVertex();
            for(var i=0;i<helper.currentVertCount;i++)
            {
                helper.PopulateUIVertex(ref vertex,i);
                vertex.position += translation;
                vertex.color.a=(byte)Mathf.RoundToInt(vertex.color.a*opacity);
                helper.SetUIVertex(vertex,i);
            }
        }
    }
}
