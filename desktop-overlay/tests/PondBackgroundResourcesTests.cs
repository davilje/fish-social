#if POND_ART_TESTS
using System;
using System.IO;
using FishSocialOverlay;

public static class PondBackgroundResourcesTests
{
    public static void Main()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pond-art-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(temp, "resources");
        Directory.CreateDirectory(Path.Combine(root, "ponds", "maps"));
        try
        {
            var shared = Path.Combine(root, "ponds", "maps", "map-lake.png");
            var perPond = Path.Combine(root, "ponds", "pond-mist.png");
            var fallback = Path.Combine(root, "ponds", "_default.png");
            var legacy = Path.Combine(root, "pond.png");
            var outside = Path.Combine(temp, "outside.png");
            foreach (var path in new[] { shared, perPond, fallback, legacy, outside }) File.WriteAllText(path, "image");
            Equal(shared, PondBackgroundResources.Resolve(root, "pond-mist", "ponds/maps/map-lake.png"));
            Equal(shared, PondBackgroundResources.Resolve(root, "pond-mist", "ponds\\maps\\map-lake.png"));
            Equal(perPond, PondBackgroundResources.Resolve(root, "pond-mist", "ponds/maps/missing.png"));
            Equal(perPond, PondBackgroundResources.Resolve(root, "pond-mist", "../outside.png"));
            Equal(perPond, PondBackgroundResources.Resolve(root, "pond-mist", outside));
            File.Delete(perPond);
            Equal(fallback, PondBackgroundResources.Resolve(root, "pond-mist", null));
            File.Delete(fallback);
            Equal(legacy, PondBackgroundResources.Resolve(root, null, null));
            File.Delete(legacy);
            Equal(null, PondBackgroundResources.Resolve(root, "unknown", null));
            Console.WriteLine("PASS: 8 shared-background and fallback cases");
        }
        finally { Directory.Delete(temp, true); }
    }

    static void Equal(string expected, string actual)
    {
        if (expected != actual) throw new Exception("Expected " + expected + ", got " + actual);
    }
}

#endif
