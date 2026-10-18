#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using FishSocial.Desktop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyFishCatalogTests
    {
        [Test]
        public void EveryGameSpeciesHasOneDistinctIllustration()
        {
            var species = DesktopGameData.Species;
            var catalog = CozyItemArt.FishCatalog;
            Assert.IsNotEmpty(species);
            CollectionAssert.AreEquivalent(species.Select(entry => entry.speciesId), catalog.Select(entry => entry.speciesId));
            Assert.AreEqual(catalog.Count, catalog.Select(entry => entry.speciesId).Distinct().Count());
            var sprites = species.Select(entry => CozyItemArt.Fish(entry.speciesId)).ToArray();
            for (var index = 0; index < species.Length; index++)
            {
                Assert.NotNull(sprites[index], species[index].speciesId);
                Assert.AreEqual(Vector4.zero, sprites[index].border, species[index].speciesId);
                var entry = catalog.Single(item => item.speciesId == species[index].speciesId);
                Assert.AreEqual(species[index].name, entry.displayName);
                Assert.IsEmpty(entry.spriteName, "Production fish must use independent cartoon sprites");
                Assert.AreEqual("Desktop/Theme/Cozy/items/fish/" + entry.speciesId, entry.resourcePath);
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprites[index])) as TextureImporter;
                Assert.NotNull(importer);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
                Assert.IsTrue(importer.alphaIsTransparency);
            }
            Assert.AreEqual(species.Length, sprites.Distinct().Count(), "Different species must not share the same sprite");
        }

        [TestCase("bass", "black_bass")]
        [TestCase("perch", "black_bass")]
        [TestCase("largemouth_bass", "black_bass")]
        [TestCase("trout", "rainbow_trout")]
        [TestCase("sturgeon", "chinese_sturgeon")]
        public void LegacyNamesResolveToCanonicalIllustration(string legacy, string canonical)
        {
            Assert.NotNull(CozyItemArt.Fish(canonical));
            Assert.AreSame(CozyItemArt.Fish(canonical), CozyItemArt.Fish(legacy));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("unknown_species")]
        public void UnknownSpeciesDoesNotShowAnotherFish(string species)
        {
            Assert.IsNull(CozyItemArt.Fish(species));
        }
    }
}
#endif
