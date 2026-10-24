using System;
using System.IO;
using System.Linq;
using FishSocial.Desktop;
using FishSocial.Desktop.Pet;

public static class SangbiaoIntegrationCheck
{
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Main(string[] arguments)
    {
        var avatar = "/image/profile/cat_avatar_sangbiao.png";
        var photo = "/image/fishing_photos/cat_fishing_sangbiao.png";
        Require(DesktopDefaultAvatars.IsDefaultPath(avatar), "Default avatar catalog");
        Require(DesktopDefaultAvatars.ResolvePetId(avatar, null) == "sangbiao", "Avatar identity");
        Require(DesktopDefaultAvatars.ResolvePetId(photo, null) == "sangbiao", "Photo identity");
        Require(DesktopDefaultAvatars.ResolveFishingPhotoPath(avatar, null) == photo, "Fishing photo route");
        Require(DesktopDefaultAvatars.NormalizeSkinId("sangbiao") == "sangbiao", "Skin normalization");
        Require(FishingLayerArt.SkinIds.Contains("sangbiao"), "Fishing skin catalog");
        Require(DesktopDefaultAvatars.NormalizeSkinId("unrecognized") == "orange", "Unknown identity fallback");
        var manifest = File.ReadAllText(arguments[0]);
        string error;
        Require(FishingLayerArt.ValidateManifest(manifest, out error), "Manifest rejected: " + error);
        var broken = manifest.Replace("\"canvas\": [\n    640,", "\"canvas\": [\n    639,");
        Require(broken != manifest, "Malformed canvas fixture");
        Require(!FishingLayerArt.ValidateManifest(broken, out error), "Invalid canvas accepted");
        Console.WriteLine("Sangbiao C# identity routes and actual fishing manifest parser passed; Unity Player not launched.");
    }
}
