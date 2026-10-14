#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Pet;

namespace FishSocial.Desktop.Editor
{
    public static class CozyPrefabRepair
    {
        public const string ThemeFolder = "Assets/Resources/Desktop/Theme/Cozy";

        public static void Ensure(string name, Type componentType)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                throw new ArgumentException("Invalid prefab name: " + name);
            var path = DesktopPrefabCatalog.AssetPath(name);
            ImportThemeSprites();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                throw new InvalidOperationException("Required complete serialized prefab is missing: " + path
                    + ". Supply its full layout; an empty runtime shell will not be generated.");
            Repair(path, componentType);
        }

        public static void ImportThemeSprites()
        {
            if (!AssetDatabase.IsValidFolder(ThemeFolder))
                throw new InvalidOperationException("Imported Cozy Sprite folder is missing: " + ThemeFolder);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ThemeFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("TextureImporter is missing: " + path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var border = ExpectedBorder(path, texture != null ? texture.width : 0,
                    texture != null ? texture.height : 0);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (importer.textureType == TextureImporterType.Sprite
                    && importer.spriteImportMode == SpriteImportMode.Single && importer.spriteBorder == border
                    && settings.spriteMeshType == SpriteMeshType.FullRect && importer.alphaIsTransparency
                    && !importer.mipmapEnabled && importer.wrapMode == TextureWrapMode.Clamp)
                    continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        public static Vector4 ExpectedBorder(string path, int width, int height)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.spriteBorder != Vector4.zero)
                return importer.spriteBorder;
            var border = path.StartsWith(ThemeFolder + "/buttons/plate-", StringComparison.Ordinal)
                ? CatTheme.PlateBorder : path.StartsWith(ThemeFolder + "/panels/", StringComparison.Ordinal)
                    ? CatTheme.PaperBorder : Vector4.zero;
            var horizontal = Mathf.Min(1f, Mathf.Max(0, width) / Mathf.Max(1f, border.x + border.z));
            var vertical = Mathf.Min(1f, Mathf.Max(0, height) / Mathf.Max(1f, border.y + border.w));
            return new Vector4(border.x * horizontal, border.y * vertical, border.z * horizontal, border.w * vertical);
        }

        public static int RepairSlicedImageModesAll()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath.StartsWith(DesktopPrefabCatalog.Folder + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Save and close the Desktop Prefab Stage before repairing saved Image modes.");
            var changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { DesktopPrefabCatalog.Folder }))
                changed += RepairSlicedImageModes(AssetDatabase.GUIDToAssetPath(guid));
            Debug.Log("[Cozy UI] Sliced 模式修复已保存并重新读取验证；修改 Image 数量：" + changed);
            return changed;
        }

        public static int RepairSlicedImageModes(string path)
        {
            CheckPrefabPath(path);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path)
                throw new InvalidOperationException("Close this Prefab Stage before repairing its saved Image modes: " + path);
            var root = PrefabUtility.LoadPrefabContents(path);
            var changed = 0;
            try
            {
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (!RequiresSlicedImageMode(image) || image.type == Image.Type.Sliced && !image.preserveAspect)
                        continue;
                    var previous = image.type;
                    var serialized = new SerializedObject(image);
                    serialized.FindProperty("m_Type").intValue = (int)Image.Type.Sliced;
                    serialized.FindProperty("m_PreserveAspect").boolValue = false;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (PrefabUtility.IsPartOfPrefabInstance(image))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(image);
                    EditorUtility.SetDirty(image);
                    changed++;
                    Debug.Log("[Cozy UI] " + path + " / " + AnimationUtility.CalculateTransformPath(image.transform, root.transform)
                        + " Image Type: " + previous + " -> Sliced", image);
                }
                if (changed > 0)
                {
                    bool success;
                    PrefabUtility.SaveAsPrefabAsset(root, path, out success);
                    if (!success)
                        throw new InvalidOperationException("Could not save Sliced Image modes: " + path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            var reloaded = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var image in reloaded.GetComponentsInChildren<Image>(true))
                    if (RequiresSlicedImageMode(image) && (image.type != Image.Type.Sliced || image.preserveAspect))
                        throw new InvalidOperationException(path + ": " + image.name + " did not persist Image.Type.Sliced.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(reloaded);
            }
            return changed;
        }

        static bool RequiresSlicedImageMode(Image image)
        {
            if (image.name == "CozyVisual")
                return true;
            var sprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
            return image.type != Image.Type.Filled && IsSlicedCozy(sprite);
        }

        public static void RepairAll()
        {
            ImportThemeSprites();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { DesktopPrefabCatalog.Folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var entry = DesktopPrefabCatalog.Find(Path.GetFileNameWithoutExtension(path));
                try
                {
                    Repair(path, entry != null ? entry.ComponentType : null);
                }
                catch (Exception exception)
                {
                    Debug.LogError("[Cozy UI] Repair refused: " + path + "\n" + exception.Message);
                }
            }
            AssetDatabase.SaveAssets();
        }

        public static void Repair(string path, Type componentType = null)
        {
            CheckPrefabPath(path);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path && stage.scene.isDirty)
                throw new InvalidOperationException("Save or close the dirty Prefab Stage before repair: " + path);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                throw new InvalidOperationException("Required complete serialized prefab is missing: " + path);
            ImportThemeSprites();
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var name = Path.GetFileNameWithoutExtension(path);
                RequireStructure(root, name, componentType);
                RepairContents(root);
                RequireValid(root, name, componentType);
                bool success;
                PrefabUtility.SaveAsPrefabAsset(root, path, out success);
                if (!success)
                    throw new InvalidOperationException("Prefab save failed: " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void RepairContents(GameObject root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                var effective = image.overrideSprite != null ? image.overrideSprite : image.sprite;
                image.sprite = PersistentSprite(effective);
                image.overrideSprite = null;
                CatTheme.ApplyLegacySurface(image);
                if (IsSlicedCozy(image.sprite) && image.type != Image.Type.Filled)
                    image.type = Image.Type.Sliced;
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (IsThemeButton(button))
                    RepairButton(button);
            }
        }

        static void RepairButton(Button button)
        {
            var visual = button.GetComponent<CozyButtonVisual>();
            if (visual == null)
                visual = button.gameObject.AddComponent<CozyButtonVisual>();
            var serialized = new SerializedObject(visual);
            var plate = serialized.FindProperty("plate").objectReferenceValue as Image;
            if (plate == null)
                plate = button.transform.Find("CozyVisual")?.GetComponent<Image>();
            if (plate == null)
            {
                if (button.transform.Find("CozyVisual") != null)
                    throw new InvalidOperationException("CozyVisual must have an Image: " + button.name);
                var node = new GameObject("CozyVisual", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                node.transform.SetParent(button.transform, false);
                node.transform.SetAsFirstSibling();
                plate = node.GetComponent<Image>();
                plate.rectTransform.anchorMin = Vector2.zero;
                plate.rectTransform.anchorMax = Vector2.one;
                plate.rectTransform.offsetMin = Vector2.zero;
                plate.rectTransform.offsetMax = Vector2.zero;
                node.GetComponent<LayoutElement>().ignoreLayout = true;
                plate.color = Color.white;
            }
            var icon = serialized.FindProperty("icon").objectReferenceValue as Image;
            if (icon == null)
                icon = button.transform.Find("Icon")?.GetComponent<Image>()
                    ?? button.transform.Find("CozyIcon")?.GetComponent<Image>();
            var hitImage = button.GetComponent<Image>();
            if (icon == null && hitImage != null && hitImage.preserveAspect
                && hitImage.type == Image.Type.Simple && hitImage.sprite != null)
            {
                if (button.transform.Find("Icon") != null || button.transform.Find("CozyIcon") != null)
                    throw new InvalidOperationException("Icon must have an Image: " + button.name);
                var node = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                node.transform.SetParent(button.transform, false);
                icon = node.GetComponent<Image>();
                icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchorMax = icon.rectTransform.anchorMin;
                icon.rectTransform.sizeDelta = new Vector2(24f, 24f);
                icon.sprite = hitImage.sprite;
                icon.preserveAspect = true;
                node.GetComponent<LayoutElement>().ignoreLayout = true;
            }
            var normal = StateSprite(serialized, "normalSprite", "default");
            var hover = StateSprite(serialized, "hoverSprite", "hover");
            var pressed = StateSprite(serialized, "pressedSprite", "pressed");
            serialized.FindProperty("plate").objectReferenceValue = plate;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("normalSprite").objectReferenceValue = normal;
            serialized.FindProperty("hoverSprite").objectReferenceValue = hover;
            serialized.FindProperty("pressedSprite").objectReferenceValue = pressed;
            if (!serialized.FindProperty("explicitTint").boolValue)
            {
                var authoredTint = plate.color;
                if (serialized.FindProperty("flat").boolValue && authoredTint.a == 0f)
                    authoredTint.a = 1f;
                serialized.FindProperty("tint").colorValue = authoredTint;
            }
            serialized.FindProperty("explicitTint").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            plate.sprite = normal;
            plate.overrideSprite = null;
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = false;
            if (serialized.FindProperty("flat").boolValue)
            {
                var plateTint = serialized.FindProperty("tint").colorValue;
                if (!serialized.FindProperty("selected").boolValue)
                    plateTint.a = 0f;
                plate.color = plateTint;
            }
            if (hitImage != null && hitImage != plate && hitImage != icon)
            {
                var hitColor = hitImage.color;
                hitColor.a = 0f;
                hitImage.color = hitColor;
                hitImage.raycastTarget = true;
                button.targetGraphic = hitImage;
            }
            if (icon != null)
                icon.raycastTarget = false;
            var states = button.spriteState;
            states.highlightedSprite = hover;
            states.selectedSprite = hover;
            states.pressedSprite = pressed;
            states.disabledSprite = normal;
            button.spriteState = states;
            button.transition = Selectable.Transition.None;
            foreach (var shadow in button.GetComponentsInChildren<Shadow>(true))
            {
                if (shadow.GetComponentInParent<Selectable>() == button)
                    shadow.enabled = false;
            }
        }

        static Sprite StateSprite(SerializedObject visual, string property, string state)
        {
            var stored = visual.FindProperty(property).objectReferenceValue as Sprite;
            var sprite = stored != null ? PersistentSprite(stored)
                : CatTheme.LoadCozy("buttons/plate-" + state + ".png", CatTheme.PlateBorder);
            if (sprite == null || !IsSlicedCozy(sprite))
                throw new InvalidOperationException("Imported bordered button Sprite is missing: " + state);
            return sprite;
        }

        static Sprite PersistentSprite(Sprite sprite)
        {
            if (sprite == null)
                return null;
            var path = AssetDatabase.GetAssetPath(sprite);
            if (EditorUtility.IsPersistent(sprite) && !string.IsNullOrEmpty(path))
            {
                var legacyPrefix = "Assets/StreamingAssets/DesktopTheme/Cozy/";
                if (!path.StartsWith(legacyPrefix, StringComparison.Ordinal))
                    return sprite;
                var migrated = CatTheme.LoadCozy(path.Substring(legacyPrefix.Length), sprite.border);
                if (migrated != null)
                    return migrated;
                throw new InvalidOperationException("Imported Resources Sprite is missing for " + path);
            }
            var imported = CatTheme.LoadCozy(sprite.name, sprite.border);
            if (imported == null || !EditorUtility.IsPersistent(imported))
                throw new InvalidOperationException("Transient Sprite cannot be saved into a prefab: " + sprite.name);
            return imported;
        }

        static bool IsSlicedCozy(Sprite sprite)
        {
            return sprite != null && sprite.border != Vector4.zero
                && AssetDatabase.GetAssetPath(sprite).StartsWith(ThemeFolder + "/", StringComparison.Ordinal);
        }

        static bool IsThemeButton(Button button)
        {
            if (button == null)
                return false;
            switch (button.name)
            {
                case "FeatureModalRoot":
                case "ProductContextMenu":
                case "PopupProductMenu":
                case "ModalPanel":
                case "Panel":
                case "Dim":
                case "MenuPanel":
                    return false;
            }
            var image = button.GetComponent<Image>();
            return button.GetComponentInParent<InputField>(true) == null && button.GetComponent<Mask>() == null
                && button.GetComponent<RectMask2D>() == null && button.GetComponent<ScrollRect>() == null
                && (image == null || image.type != Image.Type.Filled);
        }

        public static void RequireValid(GameObject root, string name, Type componentType = null)
        {
            var errors = ValidateContents(root, name, componentType);
            if (errors.Count != 0)
                throw new InvalidOperationException(string.Join("\n", errors));
        }

        public static void PrepareForSave(GameObject root, string name, Type componentType = null)
        {
            RequireStructure(root, name, componentType);
            ImportThemeSprites();
            RepairContents(root);
            RequireValid(root, name, componentType);
        }

        public static List<string> ValidateContents(GameObject root, string name, Type componentType = null)
        {
            var errors = new List<string>();
            if (root != null)
            {
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) > 0)
                        errors.Add(node.name + " has missing MonoBehaviour scripts; verify imported uGUI GUIDs before saving or building.");
            }
            try
            {
                RequireStructure(root, name, componentType);
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
            }
            if (root == null)
                return errors;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                ValidateSprite(image.sprite, image.name + ".sprite", errors);
                ValidateSprite(image.overrideSprite, image.name + ".overrideSprite", errors);
                if (image.name == "CozyVisual" && (image.type != Image.Type.Sliced || image.preserveAspect))
                    errors.Add(image.name + " Image component must persist Type = Sliced and Preserve Aspect = false; Sprite border alone is insufficient.");
                var activeSprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
                if (IsSlicedCozy(activeSprite) && image.type != Image.Type.Sliced && image.type != Image.Type.Filled)
                    errors.Add(image.name + " must save Image.Type.Sliced.");
            }
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                var states = selectable.spriteState;
                ValidateSprite(states.highlightedSprite, selectable.name + ".highlightedSprite", errors);
                ValidateSprite(states.pressedSprite, selectable.name + ".pressedSprite", errors);
                ValidateSprite(states.selectedSprite, selectable.name + ".selectedSprite", errors);
                ValidateSprite(states.disabledSprite, selectable.name + ".disabledSprite", errors);
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (!IsThemeButton(button))
                    continue;
                var visual = button.GetComponent<CozyButtonVisual>();
                if (visual == null)
                {
                    errors.Add(button.name + " has no serialized CozyButtonVisual.");
                    continue;
                }
                var serialized = new SerializedObject(visual);
                var plate = serialized.FindProperty("plate").objectReferenceValue as Image;
                if (plate == null || plate.transform.parent != button.transform || plate.name != "CozyVisual")
                    errors.Add(button.name + " requires its direct CozyVisual/Image binding.");
                var icon = serialized.FindProperty("icon").objectReferenceValue as Image;
                if (icon != null && (icon.transform.parent != button.transform
                    || (icon.name != "Icon" && icon.name != "CozyIcon")))
                    errors.Add(button.name + " requires its direct Icon/Image binding.");
                var plates = 0;
                var icons = 0;
                foreach (Transform child in button.transform)
                {
                    if (child.name == "CozyVisual")
                        plates++;
                    if (child.name == "Icon" || child.name == "CozyIcon")
                        icons++;
                }
                if (plates != 1 || icons > 1)
                    errors.Add(button.name + " contains duplicate or missing Cozy visual nodes; resolve them manually.");
                foreach (var property in new[] { "normalSprite", "hoverSprite", "pressedSprite" })
                {
                    var sprite = serialized.FindProperty(property).objectReferenceValue as Sprite;
                    ValidateSprite(sprite, button.name + "." + property, errors);
                    if (!IsSlicedCozy(sprite))
                        errors.Add(button.name + "." + property + " must reference an imported bordered Cozy Sprite.");
                }
                if (plate != null && (plate.sprite != serialized.FindProperty("normalSprite").objectReferenceValue
                    || plate.type != Image.Type.Sliced))
                    errors.Add(button.name + " must persist the normal plate and Sliced type.");
                if (button.transition != Selectable.Transition.None)
                    errors.Add(button.name + " must use CozyButtonVisual rather than a competing transition.");
                var states = button.spriteState;
                if (states.highlightedSprite != serialized.FindProperty("hoverSprite").objectReferenceValue
                    || states.selectedSprite != serialized.FindProperty("hoverSprite").objectReferenceValue
                    || states.pressedSprite != serialized.FindProperty("pressedSprite").objectReferenceValue
                    || states.disabledSprite != serialized.FindProperty("normalSprite").objectReferenceValue)
                    errors.Add(button.name + " must persist matching Selectable SpriteState references.");
                foreach (var shadow in button.GetComponentsInChildren<Shadow>(true))
                {
                    if (shadow.enabled && shadow.GetComponentInParent<Selectable>() == button)
                        errors.Add(button.name + " must not enable an external shadow.");
                }
            }
            if (name == "PanelHome")
            {
                var logo = root.transform.Find("MenuCard/Logo")?.GetComponent<Image>();
                if (logo == null || logo.sprite == null)
                    errors.Add(name + "/MenuCard/Logo requires its imported Sprite reference.");
            }
            return errors;
        }

        static void ValidateSprite(Sprite sprite, string owner, List<string> errors)
        {
            if (sprite != null && (!EditorUtility.IsPersistent(sprite) || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite))))
                errors.Add(owner + " references a transient Sprite.");
            if (sprite == null)
                return;
            var path = AssetDatabase.GetAssetPath(sprite);
            if (!CozySpriteImporter.IsSurfacePath(path))
                return;
            if (sprite.border == Vector4.zero)
                errors.Add(owner + " has no imported Sprite border; Sliced would render as a stretched quad. Reimport " + path + ".");
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                || settings.spriteMeshType != SpriteMeshType.FullRect)
                errors.Add(owner + " requires a Single Sprite with FullRect mesh for nine-slice rendering: " + path);
        }

        static void RequireStructure(GameObject root, string name, Type componentType)
        {
            if (root == null || root.GetComponent<RectTransform>() == null)
                throw new InvalidOperationException(name + " requires a serialized RectTransform root.");
            if (componentType != null && root.GetComponent(componentType) == null)
                throw new InvalidOperationException(name + " requires " + componentType.Name + ".");
            var hasContent = false;
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                hasContent |= graphic.transform != root.transform;
            if (!name.StartsWith("Panel", StringComparison.Ordinal) && name != "DesktopShell")
            {
                var label = root.GetComponent<Text>();
                var image = root.GetComponent<Image>();
                hasContent |= label != null || (image != null && image.sprite != null);
            }
            if (!hasContent)
                throw new InvalidOperationException(name + " is an empty shell; supply its complete serialized layout.");
            if (name == "DesktopShell")
            {
                if (root.GetComponent<Canvas>() == null || root.GetComponent<CanvasScaler>() == null
                    || root.GetComponent<GraphicRaycaster>() == null)
                    throw new InvalidOperationException(name + " requires its serialized Canvas, CanvasScaler and GraphicRaycaster.");
                RequireNode<Button>(root, "LoginRoot/SteamLogin");
                RequireNode<Text>(root, "LoginRoot/LoginHint");
                RequireNode<RectTransform>(root, "LoginRoot");
                var login = root.transform.Find("LoginRoot").GetComponent<RectTransform>();
                var center = new Vector2(0.5f, 0.5f);
                if (login.anchorMin != center || login.anchorMax != center || login.pivot != center
                    || login.anchoredPosition != Vector2.zero
                    || login.sizeDelta != new Vector2(WindowManager.LoginWidth, WindowManager.LoginHeight))
                    throw new InvalidOperationException(name + " LoginRoot must be centered with zero offset and match the "
                        + WindowManager.LoginWidth + "x" + WindowManager.LoginHeight + " login window.");
                RequireNode<RectTransform>(root, "MainRoot/Header");
                RequireNode<RectTransform>(root, "MainRoot/Nav");
                RequireNode<RectTransform>(root, "MainRoot/Content");
                var scaler = root.GetComponent<CanvasScaler>();
                if (scaler.referenceResolution != new Vector2(1672f, 941f))
                    throw new InvalidOperationException(name + " requires the 1672x941 reference resolution.");
                var header = "MainRoot/Header/";
                RequireNode<Image>(root, header + "ProfileCard/Avatar");
                foreach (var label in new[] { "Name", "Level", "XpLabel" })
                    RequireNode<Text>(root, header + "ProfileCard/" + label);
                RequireNode<Image>(root, header + "ProfileCard/XpBackground");
                RequireNode<Image>(root, header + "ProfileCard/XpFill");
                RequireNode<Text>(root, header + "FishCoins/Amount");
                RequireNode<Button>(root, header + "FishCoins/Plus");
                RequireNode<Text>(root, header + "CatchCount/Amount");
                foreach (var action in new[] { "Mail", "Settings", "Leaderboard", "Home" })
                    RequireNode<Button>(root, header + action);
                foreach (var label in new[] { "PetStatus", "LoginStatus", "ConnStatus", "PondStatus" })
                    RequireNode<Text>(root, header + "Status/" + label);
                if (root.transform.Find(header + "Status").gameObject.activeSelf)
                    throw new InvalidOperationException(name + " Header/Status must be hidden by default.");
                var nav = root.transform.Find("MainRoot/Nav");
                var dockRoutes = new[] { "CatchBag", "Equipment", "Shop", "Achievements", "Leaderboard", "Profile", "SocialFeed" };
                if (nav.GetComponentsInChildren<Button>(true).Length != dockRoutes.Length)
                    throw new InvalidOperationException(name + " requires exactly seven dock Buttons.");
                foreach (var route in dockRoutes)
                {
                    var path = "MainRoot/Nav/" + route;
                    RequireNode<Button>(root, path);
                    RequireNode<Image>(root, path + "/CozyVisual");
                    RequireNode<Image>(root, path + "/Icon");
                    RequireNode<Text>(root, path + "/Label");
                    RequireNode<CozyButtonVisual>(root, path);
                    var visual = new SerializedObject(root.transform.Find(path).GetComponent<CozyButtonVisual>());
                    if (!visual.FindProperty("flat").boolValue)
                        throw new InvalidOperationException(name + " requires flat dock Buttons: " + route);
                }
                foreach (ShellPanelId route in Enum.GetValues(typeof(ShellPanelId)))
                    RequireNode<RectTransform>(root, "MainRoot/Content/" + route);
                RequireNode<Text>(root, "Toast/Label");
            }
            else if (name == "PanelHome")
            {
                RequireNode<Image>(root, "HeroCat");
                RequireNode<SpriteFramePetRenderer>(root, "HeroCat");
                RequireNode<Image>(root, "MenuCard/Logo");
                foreach (var label in new[] { "Tagline", "SessionState", "CurrentPond" })
                    RequireNode<Text>(root, "MenuCard/" + label);
                foreach (var action in new[] { "StartFishing", "ExploreWorld", "DailyGift", "SelectPond" })
                    RequireNode<Button>(root, "MenuCard/" + action);
            }
            else if (name == "PanelPond")
            {
                RequireNode<Image>(root, "HeroCat");
                RequireNode<SpriteFramePetRenderer>(root, "HeroCat");
                RequireNode<Text>(root, "FishingControls/Title");
                RequireNode<Text>(root, "FishingControls/PondSession");
                foreach (var action in new[] { "EnterOverlay", "SelectPond", "ConnectPond", "TakeSpot", "StartFishing", "StopFishing", "CatchBag" })
                    RequireNode<Button>(root, "FishingControls/" + action);
            }
            else if (name == "PanelPondSelection")
            {
                RequireNode<Text>(root, "PondSelectionHeader/Title");
                RequireNode<Text>(root, "PondSelectionHeader/Description");
                RequireScroll(root, "PondCardScroll", "Cards");
            }
            else if (name == "PanelEquipment" || name == "PanelShop")
            {
                RequireNode<RectTransform>(root, "Header");
                foreach (var label in new[] { "Title", "Coins", "Equipped" })
                    RequireNode<Text>(root, "Header/" + label);
                RequireNode<RectTransform>(root, "Tabs");
                foreach (var action in new[] { "Bait", "Tackle", "Vessel" })
                    RequireNode<Button>(root, "Tabs/" + action);
                RequireScroll(root, "Items");
                RequireNode<RectTransform>(root, "Details");
                RequireNode<Text>(root, "Details/Description");
                RequireNode<Text>(root, "Status");
                foreach (var action in new[] { "Buy", "Equip", "Retry" })
                    RequireNode<Button>(root, "Details/" + action);
            }
            else if (name == "PanelWorldMap")
            {
                RequireNode<RectTransform>(root, "Viewport");
                RequireNode<RectTransform>(root, "Viewport/MapContent");
                RequireNode<Image>(root, "Viewport/MapContent/MapImage");
                RequireNode<RectTransform>(root, "Viewport/MapContent/MarkerLayer");
                RequireNode<Text>(root, "Details/DetailsText");
                RequireNode<Text>(root, "Details/StatusText");
                foreach (var action in new[] { "EnterPond", "ResetView", "ModeSell", "ModeAuto" })
                    RequireNode<Button>(root, "Details/" + action);
            }
            else if (name == "PanelAchievements" || name == "PanelProfileHub")
            {
                RequireNode<Text>(root, "Shell/Header/Title");
                RequireNode<Button>(root, "Shell/Header/Edit");
                RequireNode<Button>(root, "Shell/Header/Retry");
                RequireNode<Text>(root, "Shell/Status");
                foreach (var label in new[] { "资料", "展示柜", "图鉴", "相册", "成就" })
                    RequireNode<Button>(root, "Shell/Sidebar/Tab_" + label);
                RequireNode<Image>(root, "Shell/Content/Tab_资料/Avatar");
                RequireNode<Text>(root, "Shell/Content/Tab_资料/Body");
                RequireScroll(root, "Shell/Content/Tab_展示柜/ShowcaseGrid");
                RequireNode<Text>(root, "Shell/Content/Tab_展示柜/Detail");
                RequireScroll(root, "Shell/Content/Tab_图鉴/CodexGrid");
                RequireNode<Text>(root, "Shell/Content/Tab_图鉴/Detail");
                var album = "Shell/Content/Tab_相册";
                RequireNode<Image>(root, album + "/AlbumStage");
                foreach (var slot in new[] { "SlotL", "SlotR" })
                {
                    RequireNode<Image>(root, album + "/AlbumStage/" + slot);
                    RequireNode<Image>(root, album + "/AlbumStage/" + slot + "/Photo");
                    RequireNode<Text>(root, album + "/AlbumStage/" + slot + "/Body");
                }
                RequireNode<Button>(root, album + "/Prev");
                RequireNode<Button>(root, album + "/Next");
                RequireNode<Text>(root, album + "/Page");
                RequireScroll(root, "Shell/Content/Tab_成就/Achievements");
            }
            else if (name == "PanelSocialFeed")
            {
                RequireNode<Button>(root, "Header/Public");
                RequireNode<Button>(root, "Header/Friends");
                RequireNode<Button>(root, "Retry");
                RequireNode<Text>(root, "Status");
                RequireScroll(root, "Scroll");
            }
            else if (name == "FeatureModalRoot")
            {
                if (root.GetComponent<Button>() == null)
                    throw new InvalidOperationException(name + " requires its serialized dismiss Button.");
                RequireNode<Button>(root, "ModalPanel");
                RequireNode<Text>(root, "ModalPanel/Header/Title");
                RequireNode<Button>(root, "ModalPanel/Header/Close");
                RequireEmbeddedView<DesktopSocialModalView>(root, "SocialModal", "PanelSocial");
                RequireEmbeddedView<DesktopCatchBagModalView>(root, "CatchBagModal", "PanelCatch");
                RequireEmbeddedView<DesktopGalleryModalView>(root, "GalleryModal", "PanelGallery");
                RequireEmbeddedView<DesktopSettingsModalView>(root, "SettingsModal", "PanelSettings");
            }
            else if (name == "ProductContextMenu")
            {
                if (root.GetComponent<Button>() == null)
                    throw new InvalidOperationException(name + " requires its serialized dismiss Button.");
                RequireNode<RectTransform>(root, "MenuPanel");
                foreach (var action in new[] { "CurrentPond", "WorldMap", "Shop", "Friends", "CatchBag", "Profile",
                    "Settings", "SocialFeed", "Leaderboard", "HideToTray", "Quit" })
                {
                    RequireNode<Button>(root, "MenuPanel/" + action);
                    RequireNode<Text>(root, "MenuPanel/" + action + "/Label");
                }
            }
            else if (name == "PanelPondSettlement")
            {
                RequireNode<Text>(root, "Panel/Title");
                RequireNode<Text>(root, "Panel/Summary");
                RequireNode<Button>(root, "Panel/Close");
                RequireScroll(root, "Panel/ListScroll", "List");
                RequireNode<Text>(root, "Panel/ListScroll/Viewport/List/ListText");
            }
            else if (name == "PondSelectionCard")
            {
                foreach (var label in new[] { "Name", "Description", "Fee", "Level", "Access" })
                    RequireNode<Text>(root, label);
                RequireNode<Image>(root, "PondIcon");
                RequireNode<Button>(root, "FocusOnMap");
            }
            else if (name == "WorldMapPondMarker")
            {
                if (root.GetComponent<Button>() == null)
                    throw new InvalidOperationException(name + " requires its serialized marker Button.");
                RequireNode<Text>(root, "Label");
                RequireNode<Image>(root, "Icon");
            }
        }

        static void RequireEmbeddedView<T>(GameObject root, string child, string prefabName) where T : Component
        {
            var path = "ModalPanel/Body/" + child;
            RequireNode<T>(root, path);
            RequireStructure(root.transform.Find(path).gameObject, prefabName, typeof(T));
        }

        static void RequireScroll(GameObject root, string path, string contentName = "Content")
        {
            RequireNode<ScrollRect>(root, path);
            RequireNode<RectTransform>(root, path + "/Viewport");
            RequireNode<RectTransform>(root, path + "/Viewport/" + contentName);
            var scroll = root.transform.Find(path).GetComponent<ScrollRect>();
            var viewport = root.transform.Find(path + "/Viewport").GetComponent<RectTransform>();
            var content = root.transform.Find(path + "/Viewport/" + contentName).GetComponent<RectTransform>();
            if (scroll.viewport != viewport || scroll.content != content)
                throw new InvalidOperationException(root.name + " requires serialized " + path + " viewport/content bindings.");
        }

        static void RequireNode<T>(GameObject root, string path) where T : Component
        {
            var node = root.transform.Find(path);
            if (node == null || node.GetComponent<T>() == null)
                throw new InvalidOperationException(root.name + " requires " + path + "/" + typeof(T).Name + ".");
        }

        public static int ValidateAll()
        {
            var errors = 0;
            foreach (var entry in DesktopPrefabCatalog.All)
            {
                if (entry.Name == "OverlayPondLayout")
                    continue;
                var path = DesktopPrefabCatalog.AssetPath(entry.Name);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var error in ValidateContents(root, entry.Name, entry.ComponentType))
                {
                    errors++;
                    Debug.LogError("[Cozy UI] " + path + ": " + error);
                }
            }
            if (errors == 0)
                Debug.Log("[Cozy UI] Serialized prefab structure and Sprite references are valid.");
            return errors;
        }

        static void CheckPrefabPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(DesktopPrefabCatalog.Folder + "/", StringComparison.Ordinal)
                || !path.EndsWith(".prefab", StringComparison.Ordinal) || path.Contains("..") || path.Contains("\\"))
                throw new ArgumentException("Repair only accepts Desktop Resources prefab paths: " + path);
        }
    }
}
#endif
