#if OVERLAY_CACHE_TESTS
using System;
using FishSocialOverlay;

public static class OverlayResourceCacheTests
{
    static int passed;
    public static void Main()
    {
        Run("release keeps warm frames", WarmReuse);
        Run("mixed species share props until both release", SharedResources);
        Run("same species users protect the same resource", MultipleUsers);
        Run("eviction follows last access", LeastRecentlyUsed);
        Run("live resources survive budget pressure", ActiveOverBudget);
        Run("size variants coexist", SizeVariants);
        Run("replacement updates accounting", Replacement);
        Run("repeated scene cycles stay within budget", SceneCycles);
        Console.WriteLine("PASS: " + passed + "/" + passed + " cache behavior tests");
    }

    static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static bool Contains(OverlayResourceCache<object> cache, string key) { return cache.TryGet(key, out _); }

    static void WarmReuse()
    {
        var c = new OverlayResourceCache<object>(100);
        var frame = new object();
        c.Retain(new[] { "orange/body/catch/0" });
        c.Add("128|orange/body/catch/0", "orange/body/catch/0", frame, 40);
        c.Release(new[] { "orange/body/catch/0" });
        c.Retain(new[] { "orange/body/catch/0" });
        Require(c.TryGet("128|orange/body/catch/0", out var actual) && ReferenceEquals(frame, actual), "hot frame was replaced");
        c.Release(new[] { "orange/body/catch/0" });
        Require(c.ReferencedResources == 0 && c.ResidentBytes == 40, "warm entry should remain unpinned");
    }

    static void SharedResources()
    {
        var c = new OverlayResourceCache<object>(20);
        var orange = new[] { "orange/hooked/body", "shared/hooked/rod" };
        var calico = new[] { "calico/hooked/body", "shared/hooked/rod" };
        c.Retain(orange); c.Retain(calico);
        foreach (var path in new[] { orange[0], orange[1], calico[0] }) c.Add(path, path, new object(), 10);
        c.Release(orange);
        Require(!Contains(c, orange[0]), "inactive species should be eligible for eviction");
        Require(Contains(c, calico[0]) && Contains(c, orange[1]), "other species or shared prop was evicted");
        c.Release(calico);
        Require(c.ReferencedResources == 0, "shared reference did not balance");
    }

    static void MultipleUsers()
    {
        var c = new OverlayResourceCache<object>(0);
        c.Retain(new[] { "body" }); c.Retain(new[] { "body" });
        c.Add("size|body", "body", new object(), 30);
        c.Release(new[] { "body" });
        Require(Contains(c, "size|body"), "first user released the second user's frame");
        c.Release(new[] { "body" }); c.Release(new[] { "body" });
        Require(c.Count == 0 && c.ReferencedResources == 0, "last release should trim");
    }

    static void LeastRecentlyUsed()
    {
        var c = new OverlayResourceCache<object>(20);
        c.Add("a", "a", new object(), 10); c.Add("b", "b", new object(), 10);
        Contains(c, "a"); c.Add("c", "c", new object(), 10);
        Require(Contains(c, "a") && Contains(c, "c") && !Contains(c, "b"), "wrong LRU victim");
    }

    static void ActiveOverBudget()
    {
        var c = new OverlayResourceCache<object>(10);
        c.Retain(new[] { "active" }); c.Add("active", "active", new object(), 40);
        c.Add("warm", "warm", new object(), 5);
        Require(Contains(c, "active") && !Contains(c, "warm") && c.ResidentBytes == 40, "live frame must stay resident");
        c.Release(new[] { "active" });
        Require(c.ResidentBytes == 0, "released over-budget frame must be evicted");
    }

    static void SizeVariants()
    {
        var c = new OverlayResourceCache<object>(100);
        var small = new object(); var large = new object();
        c.Add("64|body", "body", small, 10); c.Add("128|body", "body", large, 40);
        Require(c.TryGet("64|body", out var a) && ReferenceEquals(a, small), "new size flushed previous size");
        Require(c.TryGet("128|body", out var b) && ReferenceEquals(b, large), "size key collision");
    }

    static void Replacement()
    {
        var c = new OverlayResourceCache<object>(100);
        c.Add("k", "r", new object(), 50); c.Add("k", "r", new object(), 20);
        Require(c.Count == 1 && c.ResidentBytes == 20, "replacement double-counted bytes");
    }

    static void SceneCycles()
    {
        var c = new OverlayResourceCache<object>(64);
        for (var scene = 0; scene < 30; scene++)
        {
            var paths = new[] { "body-" + scene, "shared/rod" };
            c.Retain(paths);
            foreach (var path in paths) c.Add("128|" + path, path, new object(), 20);
            c.Release(paths);
            Require(c.ReferencedResources == 0 && c.ResidentBytes <= 64, "scene release leaked references or budget");
        }
    }
}
#endif
