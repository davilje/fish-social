#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.IO;
using System.Linq;
using FishSocial.Desktop;
using FishSocial.Desktop.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishSocial.Tests.Editor
{
    public sealed class ExpandedPondSceneryTests
    {
        const string Art = "Assets/Desktop/OverlayArt/Ponds/";

        [Test]
        public void AllDestinationsHaveSavedArtMotionPhotosAndGroundedActors()
        {
            var catalog = JObject.Parse(File.ReadAllText(Art + "catalog.json"));
            var bindings = catalog["bindings"].ToArray();
            CollectionAssert.AreEquivalent(DesktopGameData.Ponds.Select(pond => pond.pondId), bindings.Select(row => (string)row["pondId"]));
            Assert.AreEqual(30, bindings.Select(row => (string)row["layerLayout"])
                .Where(path => path.Contains("pastel-scenes-v1/")).Distinct().Count());
            foreach (var binding in bindings)
            {
                var id = (string)binding["pondId"];
                var sourceLayout = Art + (string)binding["layerLayout"];
                var scene = JObject.Parse(File.ReadAllText(sourceLayout));
                var motion = Path.Combine(Path.GetDirectoryName(sourceLayout), (string)scene["motion"]);
                Assert.IsTrue(File.Exists(motion), id + " motion source");
                var exported = JObject.Parse(File.ReadAllText(Path.Combine(OverlayLayoutExporter.LayoutOutputDir(), id + ".json")));
                var background = exported["objects"].Single(row => (string)row["id"] == "pond-bg");
                var resource = Path.Combine(OverlayLayoutExporter.LayoutOutputDir(), "..", (string)background["sprite"]);
                Assert.IsTrue(File.Exists(resource), id + " exported background");
                Assert.IsTrue(File.Exists(Path.Combine(Path.GetDirectoryName(resource), "motion.json")), id + " shipped motion");
                Assert.NotNull(Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/ponds/" + id), id + " selection photo");
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPondLayoutBaker.PrefabPath(id));
                Assert.NotNull(asset, id + " prefab");
                var canvas = asset.GetComponent<RectTransform>();
                var spec = catalog["seats"].Single(seat => (string)seat["name"] == (string)binding["seat"]);
                var parts = asset.GetComponentsInChildren<DesktopOverlayLayoutObject>(true);
                Assert.AreEqual(20, parts.Count(part => part.kind == "spot"), id);
                foreach (var seat in parts.Where(part => part.kind == "actor-seat"))
                {
                    var rect = seat.GetComponent<RectTransform>();
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, rect);
                    var ground = canvas.rect.height - bounds.min.y - rect.rect.height * (1 - (float)spec["groundY"]);
                    Assert.AreEqual((float)scene["groundY"], ground, .15f, id + "/" + seat.name + " must touch the bank");
                }
                Assert.Greater((float)exported["pond"]["waterlineY"], (float)scene["groundY"], id + " water must be below actors");
            }
        }

        [Test]
        public void MissingLayoutFallbackKeepsAllSeatsOnOneBank()
        {
            for (var i = 0; i < 20; i++)
            {
                OverlayPondLayoutBaker.DefaultSpotPosition(i, 20, out var x, out var y);
                Assert.AreEqual(430, y);
                Assert.That(x, Is.InRange(84, 876));
            }
        }
    }
}
#endif
