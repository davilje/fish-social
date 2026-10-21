#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Tests
{
    public sealed class CozyJournalLayoutOwnershipTests
    {
        const string Folder="Assets/Resources/Desktop/Prefabs/";
        [TestCase("PanelShop","Items/Viewport/Content",3)]
        [TestCase("PanelEquipment","Items/Viewport/Content",3)]
        [TestCase("PanelCatch","Grid/Viewport/Slots",4)]
        [TestCase("PanelPondSelection","PondCardScroll/Viewport/Cards",2)]
        public void DynamicGridGrowsWithoutMovingFixedPageRegions(string prefab,string contentPath,int columns)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+prefab+".prefab"));
            try
            {
                var header=(RectTransform)go.transform.Find("DesignHeader");
                var headerPosition=header.anchoredPosition;
                var headerSize=header.sizeDelta;
                var content=(RectTransform)go.transform.Find(contentPath);
                var grid=content.GetComponent<GridLayoutGroup>();
                Assert.IsTrue(grid.enabled);
                Assert.AreEqual(columns,grid.constraintCount);
                Assert.AreEqual(1,content.GetComponents<LayoutGroup>().Length);
                var fitter=content.GetComponent<ContentSizeFitter>();
                Assert.IsTrue(fitter.enabled);
                Assert.AreEqual(ContentSizeFitter.FitMode.Unconstrained,fitter.horizontalFit);
                for(var i=0;i<columns*4;i++)new GameObject("dynamic-row",typeof(RectTransform)).transform.SetParent(content,false);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)go.transform);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                Assert.Greater(content.rect.height,grid.cellSize.y*3,"content must grow vertically to expose overflow to ScrollRect");
                Assert.AreEqual(headerPosition,header.anchoredPosition);
                Assert.AreEqual(headerSize,header.sizeDelta);
                foreach(var group in go.GetComponentsInChildren<LayoutGroup>(true))Assert.IsTrue(group.enabled,group.name+" retains an obsolete disabled layout");
                foreach(var sizeFitter in go.GetComponentsInChildren<ContentSizeFitter>(true))Assert.IsTrue(sizeFitter.enabled,sizeFitter.name+" retains an obsolete disabled fitter");
            }
            finally {Object.DestroyImmediate(go);}
        }
    }
}
#endif
