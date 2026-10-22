using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
                if (!knownChildren.Contains(child.GetInstanceID()) && !CatTheme.IsGeneratedVisual(child)
                    && child.GetComponent<CozyPanelSkin>() == null)
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

    /// <summary>
    /// Keeps a heavy page in the hierarchy and only toggles its own canvas.
    /// Deactivating the page would enable every row button again on the next click.
    /// The page must sit outside a scroll mask; masked lists use active roots instead.
    /// </summary>
    public static class CozyKeptPage
    {
        public static void Show(GameObject page, bool active)
        {
            if (page == null)
                return;
            if (!page.activeSelf)
                page.SetActive(true);
            var canvas = page.GetComponent<Canvas>();
            if (canvas == null)
            {
                var parent = page.GetComponentInParent<Canvas>();
                canvas = page.AddComponent<Canvas>();
                canvas.overrideSorting = false;
                if (parent != null)
                    canvas.additionalShaderChannels = parent.additionalShaderChannels;
                if (page.GetComponent<GraphicRaycaster>() == null)
                    page.AddComponent<GraphicRaycaster>();
            }
            canvas.overrideSorting = false;
            canvas.enabled = active;
            var raycaster = page.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = active;
        }

        public static RectTransform DuplicateList(RectTransform source, string name)
        {
            var copy = new GameObject(name, typeof(RectTransform));
            copy.transform.SetParent(source.parent, false);
            var rect = copy.GetComponent<RectTransform>();
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.anchoredPosition = source.anchoredPosition;
            rect.sizeDelta = source.sizeDelta;
            rect.offsetMin = source.offsetMin;
            rect.offsetMax = source.offsetMax;
            var layout = source.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                var vertical = copy.AddComponent<VerticalLayoutGroup>();
                vertical.padding = new RectOffset(layout.padding.left, layout.padding.right, layout.padding.top, layout.padding.bottom);
                vertical.spacing = layout.spacing;
                vertical.childAlignment = layout.childAlignment;
                vertical.childControlWidth = layout.childControlWidth;
                vertical.childControlHeight = layout.childControlHeight;
                vertical.childForceExpandWidth = layout.childForceExpandWidth;
                vertical.childForceExpandHeight = layout.childForceExpandHeight;
                vertical.childScaleWidth = layout.childScaleWidth;
                vertical.childScaleHeight = layout.childScaleHeight;
            }
            var grid = source.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                var cells = copy.AddComponent<GridLayoutGroup>();
                cells.padding = new RectOffset(grid.padding.left, grid.padding.right, grid.padding.top, grid.padding.bottom);
                cells.cellSize = grid.cellSize;
                cells.spacing = grid.spacing;
                cells.startCorner = grid.startCorner;
                cells.startAxis = grid.startAxis;
                cells.childAlignment = grid.childAlignment;
                cells.constraint = grid.constraint;
                cells.constraintCount = grid.constraintCount;
            }
            var fit = source.GetComponent<ContentSizeFitter>();
            if (fit != null)
            {
                var fitter = copy.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = fit.horizontalFit;
                fitter.verticalFit = fit.verticalFit;
            }
            copy.SetActive(false);
            return rect;
        }
    }

}
