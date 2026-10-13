using System.Text;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// 离塘结算弹窗：展示本局鱼获、回鱼收入、扣费与盈亏。
    /// Prefab：Resources/Desktop/Prefabs/PanelPondSettlement
    /// </summary>
    public sealed class DesktopPondSettlementModalView : MonoBehaviour
    {
        const string PrefabPath = "Desktop/Prefabs/PanelPondSettlement";

        Text _title;
        Text _summary;
        Text _list;
        Button _closeButton;
        GameObject _root;

        public static DesktopPondSettlementModalView Show(Transform canvas, PondSessionSummaryDto data)
        {
            if (canvas == null || data == null)
                return null;
            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[DesktopUI] Required prefab is missing: " + PrefabPath, canvas);
                return null;
            }
            var go = Object.Instantiate(prefab, canvas, false);
            go.name = "PondSettlementModal";
            var view = go.GetComponent<DesktopPondSettlementModalView>();
            if (view == null)
            {
                Debug.LogError("[DesktopUI] PanelPondSettlement requires DesktopPondSettlementModalView.", go);
                go.SetActive(false);
                Object.Destroy(go);
                return null;
            }
            view.BindRefs(go.transform);
            if (view._title == null || view._summary == null || view._list == null || view._closeButton == null)
            {
                Debug.LogError("[DesktopUI] PanelPondSettlement requires Panel/{Title,Summary,Close,ListScroll/Viewport/List/ListText}.", go);
                go.SetActive(false);
                Object.Destroy(go);
                return null;
            }
            foreach (var existing in canvas.GetComponentsInChildren<DesktopPondSettlementModalView>(true))
                if (existing != view)
                    Object.Destroy(existing.gameObject);
            DesktopModalUi.StyleTree(go.transform);
            view.Render(data);
            go.SetActive(true);
            go.transform.SetAsLastSibling();
            return view;
        }

        /// <summary>Editor-only prefab layout generation.</summary>
        public static void BuildLayout(Transform root)
        {
#if !UNITY_EDITOR
            return;
#else
            if (Application.isPlaying || root == null)
                return;

            ClearChildren(root);

            var dim = DesktopModalUi.Panel("Dim", root, new Color(0f, 0f, 0f, 0.5f));
            DesktopModalUi.Stretch(dim);

            var panel = DesktopModalUi.Panel("Panel", root, DesktopModalUi.PanelColor);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.08f, 0.1f);
            rt.anchorMax = new Vector2(0.92f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var title = DesktopModalUi.Label(panel.transform, "Title", "离塘结算", 22, TextAnchor.MiddleLeft);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(-24f, 40f);
            titleRt.anchoredPosition = new Vector2(0f, -8f);

            var summary = DesktopModalUi.Label(panel.transform, "Summary", "", 16, TextAnchor.UpperLeft);
            var summaryRt = summary.rectTransform;
            summaryRt.anchorMin = new Vector2(0f, 1f);
            summaryRt.anchorMax = new Vector2(1f, 1f);
            summaryRt.pivot = new Vector2(0.5f, 1f);
            summaryRt.sizeDelta = new Vector2(-24f, 120f);
            summaryRt.anchoredPosition = new Vector2(0f, -52f);
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            summary.verticalOverflow = VerticalWrapMode.Overflow;

            var scrollGo = new GameObject("ListScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0.12f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(12f, 12f);
            scrollRt.offsetMax = new Vector2(-12f, -180f);
            scrollGo.GetComponent<Image>().color = DesktopModalUi.Slot;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            DesktopModalUi.Stretch(viewport);
            var content = new GameObject("List", typeof(RectTransform), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
            content.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var list = DesktopModalUi.Label(content.transform, "ListText", "", 14, TextAnchor.UpperLeft);
            var listRt = list.rectTransform;
            listRt.anchorMin = new Vector2(0f, 1f);
            listRt.anchorMax = new Vector2(1f, 1f);
            listRt.pivot = new Vector2(0.5f, 1f);
            listRt.sizeDelta = new Vector2(-16f, 0f);
            listRt.anchoredPosition = Vector2.zero;
            list.horizontalOverflow = HorizontalWrapMode.Wrap;
            list.verticalOverflow = VerticalWrapMode.Overflow;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;

            DesktopModalUi.MakeButton(panel.transform, "Close", "知道了", null);
            var close = DesktopModalUi.FindComponent<Button>(panel.transform, "Close");
            if (close != null)
            {
                var closeRt = close.GetComponent<RectTransform>();
                closeRt.anchorMin = new Vector2(0.35f, 0f);
                closeRt.anchorMax = new Vector2(0.65f, 0f);
                closeRt.offsetMin = new Vector2(0f, 12f);
                closeRt.offsetMax = new Vector2(0f, 48f);
            }
#endif
        }

        public void BuildEditorLayout()
        {
            BuildLayout(transform);
        }

        static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.GetChild(i).gameObject);
        }

        void BindRefs(Transform root)
        {
            _root = root.gameObject;
            _title = DesktopModalUi.FindComponent<Text>(root, "Panel/Title");
            _summary = DesktopModalUi.FindComponent<Text>(root, "Panel/Summary");
            _list = DesktopModalUi.FindComponent<Text>(root, "Panel/ListScroll/Viewport/List/ListText");
            _closeButton = DesktopModalUi.FindComponent<Button>(root, "Panel/Close");
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveAllListeners();
                _closeButton.onClick.AddListener(Close);
            }
            else
                DesktopModalUi.BindButton(root, "Close", Close);
        }

        void Render(PondSessionSummaryDto data)
        {
            if (_title != null)
                _title.text = data.pondName + " · 本局结算";
            if (_summary != null)
                _summary.text = BuildSummaryLine(data);
            if (_list != null)
                _list.text = BuildCatchList(data);
        }

        static string BuildSummaryLine(PondSessionSummaryDto data)
        {
            var mode = data.returnFeeMode == "auto_return" ? "回鱼档" : "出售档";
            var playerXp = data.totalCatchPlayerXp + data.totalReturnPlayerXp;
            var pondXp = data.totalCatchPondXp + data.totalReturnPondXp;
            var profit = data.netProfit;
            var profitLabel = profit >= 0 ? "盈利" : "亏损";
            return mode + "\n" +
                   "回鱼收入：" + data.totalReturnGold + " 金币\n" +
                   "入场扣费：-" + data.feesPaid + " 金币\n" +
                   "玩家经验：+" + playerXp + "（塘经验 +" + pondXp + "）\n" +
                   "本局" + profitLabel + "：" + (profit >= 0 ? "+" : "") + profit + " 金币";
        }

        static string BuildCatchList(PondSessionSummaryDto data)
        {
            var catches = data.catches;
            if (catches == null || catches.Length == 0)
                return "本局无鱼获记录。";

            var sb = new StringBuilder();
            for (var i = 0; i < catches.Length; i++)
            {
                var c = catches[i];
                if (c == null)
                    continue;
                var name = DesktopFishCatalog.SpeciesName(c.speciesId);
                var q = DesktopFishCatalog.QualityName(c.quality);
                sb.Append(i + 1).Append(". ").Append(name)
                    .Append(" · ").Append(q)
                    .Append(" · ").Append(c.sizeM.ToString("0.00")).Append("m");
                if (c.outcome == "returned")
                    sb.Append(" → 已回塘 +").Append(c.returnGold).Append(" 金");
                else
                    sb.Append(" → 已入包");
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd();
        }

        public void Close()
        {
            if (_root != null)
                Destroy(_root);
            else
                Destroy(gameObject);
        }
    }
}
