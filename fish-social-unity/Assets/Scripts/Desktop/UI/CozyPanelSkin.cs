using System.Collections.Generic;
using UnityEngine;

namespace FishSocial.Desktop
{
    [DisallowMultipleComponent]
    public sealed class CozyPanelSkin : MonoBehaviour
    {
        readonly HashSet<int> knownChildren = new HashSet<int>();
        readonly HashSet<int> styledComponents = new HashSet<int>();
        readonly List<Transform> addedChildren = new List<Transform>();
        bool skinned;
        bool childrenChanged;

        public void ApplyNow()
        {
            CatTheme.SkinTree(transform);
        }

        internal void MarkSkinned()
        {
            skinned = true;
        }

        internal bool MarkStyled(Component component)
        {
            return styledComponents.Add(component.GetInstanceID());
        }

        internal void RememberChildren()
        {
            knownChildren.Clear();
            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
                knownChildren.Add(transform.GetChild(childIndex).GetInstanceID());
            childrenChanged = false;
        }

        void OnEnable()
        {
            childrenChanged = true;
        }

        void OnTransformChildrenChanged()
        {
            childrenChanged = true;
        }

        void LateUpdate()
        {
            if (!skinned)
            {
                ApplyNow();
                return;
            }
            if (!childrenChanged)
                return;
            childrenChanged = false;
            addedChildren.Clear();
            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                var child = transform.GetChild(childIndex);
                if (!knownChildren.Contains(child.GetInstanceID()) && !CatTheme.IsGeneratedVisual(child))
                    addedChildren.Add(child);
            }
            foreach (var child in addedChildren)
            {
                if (child != null)
                    CatTheme.SkinTree(child);
            }
            RememberChildren();
            var visual = GetComponentInParent<CozyButtonVisual>();
            if (visual != null)
                visual.RefreshContent();
        }
    }
}
