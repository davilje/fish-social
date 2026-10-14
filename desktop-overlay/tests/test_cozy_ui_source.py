import hashlib
import json
from pathlib import Path
import re
import struct
import unittest
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
WPF = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
XAML = "{http://schemas.microsoft.com/winfx/2006/xaml}"
COZY = "{clr-namespace:FishSocialOverlay}"
CONTRACT = json.loads((ROOT / "tests/cozy_source_contract.json").read_text())


def read_source(filename):
    return (ROOT / filename).read_text()


def method_body(source, name):
    match = re.search(r"\b" + re.escape(name) + r"\s*\([^;{}]*\)\s*(\{|=>)", source)
    if match is None:
        raise AssertionError("Missing method: " + name)
    if match.group(1) == "=>":
        return source[match.end() - 2:source.index(";", match.end()) + 1]
    start = match.end() - 1
    depth = 0
    for offset in range(start, len(source)):
        if source[offset] == "{":
            depth += 1
        if source[offset] == "}":
            depth -= 1
        if depth == 0:
            return source[start:offset + 1]
    raise AssertionError("Unclosed method: " + name)


class CozyUiSourceTests(unittest.TestCase):
    def test_xaml_is_well_formed_and_theme_is_loaded(self):
        for filename in ("App.xaml", "MainWindow.xaml", "CozyTheme.xaml"):
            with self.subTest(filename=filename):
                ET.parse(ROOT / filename)
        self.assertIn('Source="CozyTheme.xaml"', read_source("App.xaml"))

    def test_all_existing_named_elements_and_handlers_survive(self):
        markup = read_source("MainWindow.xaml")
        theme = read_source("CozyTheme.xaml")
        names = set(re.findall(r'x:Name="([^"]+)"', markup + theme))
        self.assertTrue(set(CONTRACT["named_elements"]).issubset(names))
        source = read_source("MainWindow.xaml.cs")
        for handler in CONTRACT["xaml_handlers"]:
            with self.subTest(handler=handler):
                self.assertIn('="' + handler + '"', markup)
                method_body(source, handler)
        method_body(source, "ExportHudJson_OnClick")

    def test_business_callbacks_and_viewport_operations_are_unchanged(self):
        source = read_source("MainWindow.xaml.cs")
        for name, expected in CONTRACT["business_method_sha256"].items():
            with self.subTest(method=name):
                actual = hashlib.sha256(method_body(source, name).encode()).hexdigest()
                self.assertEqual(expected, actual)

    def test_ipc_seats_scene_hit_testing_animation_and_nine_slice_are_untouched(self):
        for filename, expected in CONTRACT["protected_file_sha256"].items():
            with self.subTest(filename=filename):
                actual = hashlib.sha256((ROOT / filename).read_bytes()).hexdigest()
                self.assertEqual(expected, actual)

    def test_actor_frames_bubble_animations_and_card_anchors_are_unchanged(self):
        for filename, methods in CONTRACT["preserved_method_sha256"].items():
            source = read_source(filename)
            for name, expected in methods.items():
                with self.subTest(filename=filename, method=name):
                    self.assertEqual(expected, hashlib.sha256(method_body(source, name).encode()).hexdigest())

    def test_all_73_png_assets_are_unchanged(self):
        self.assertEqual(73, len(CONTRACT["png_sha256"]))
        for filename, expected in CONTRACT["png_sha256"].items():
            with self.subTest(filename=filename):
                self.assertEqual(expected, hashlib.sha256((ROOT / filename).read_bytes()).hexdigest())

    def test_three_distinct_407_by_400_plates_and_64_pixel_borders(self):
        signatures = set()
        for state in ("default", "hover", "pressed"):
            path = ROOT / ("OverlayResources/hud/cozy/buttons/plate-" + state + ".png")
            data = path.read_bytes()
            self.assertEqual(b"\x89PNG\r\n\x1a\n", data[:8])
            self.assertEqual((407, 400), struct.unpack(">II", data[16:24]))
            signatures.add(hashlib.sha256(data).hexdigest())
            metadata = Path(str(path) + ".meta").read_text()
            border = re.search(r"spriteBorder:\s*x: (\d+)\s*y: (\d+)\s*z: (\d+)\s*w: (\d+)", metadata)
            self.assertIsNotNone(border)
            self.assertEqual(("64",) * 4, border.groups())
        self.assertEqual(3, len(signatures))
        self.assertIn("SpriteBorder = { 64, 64, 64, 64 }", read_source("CozyTheme.cs"))

    def test_real_three_state_template_moves_visual_only_by_three_pixels(self):
        root = ET.parse(ROOT / "CozyTheme.xaml").getroot()
        style = next(element for element in root if element.get(XAML + "Key") == "CozyButton")
        triggers = list(style.iter(WPF + "Trigger"))
        pressed = next(element for element in triggers if element.get("Property") == "IsPressed")
        self.assertIsNotNone(next(pressed.iter(WPF + "TranslateTransform"), None))
        self.assertEqual("{DynamicResource CozyPressedOffset}", next(pressed.iter(WPF + "TranslateTransform")).get("Y"))
        offset = next(element for element in root if element.get(XAML + "Key") == "CozyPressedOffset")
        self.assertEqual("3", offset.text)
        self.assertTrue(any(element.get("TargetName") == "Pressed" and element.get("Value") == "Visible"
                            for element in pressed.iter(WPF + "Setter")))
        self.assertTrue(any(element.get("Property") == "IsMouseOver" for element in triggers))
        surfaces = list(style.iter(COZY + "CozySurface"))
        self.assertEqual(3, len(surfaces))
        self.assertTrue(all(element.get("IsHitTestVisible") == "False" for element in surfaces))
        self.assertIn('Property="IsEnabled" Value="False"', read_source("CozyTheme.xaml"))

    def test_json_buttons_share_theme_including_nested_chat_branch(self):
        source = read_source("OverlayHudLayout.cs")
        self.assertEqual(2, source.count("ApplyCozyButtonVisual(element, widget);"))
        nested = source[source.index("if (DockChatChildIds.Contains(widget.id))"):source.index("if (!absolute.TryGetValue")]
        self.assertIn("ApplyCozyButtonVisual(element, widget);", nested)
        visual = method_body(source, "ApplyCozyButtonVisual")
        for setter in ("SetNormalSprite", "SetHoverSprite", "SetPressedSprite", "SetIcon"):
            self.assertIn(setter, visual)
        self.assertNotIn("HudFlatButtonStyle", source)
        self.assertIn('FindResource("CozyButton")', visual)
        for field in ("spriteHover", "spritePressed", "iconSprite"):
            self.assertIn('[DataMember(Name = "' + field + '"', source)
        self.assertIn("ApplyCozyResourceMapping(document);", source)
        self.assertIn("HudButtonPressScale_OnClick", visual)

    def test_png_icons_and_paper_are_reused_not_svg_or_symbol_substitutes(self):
        for filename in ("MainWindow.xaml", "CozyTheme.xaml"):
            source = read_source(filename)
            self.assertNotIn(".svg", source.lower())
            for symbol in ("◀", "▶", "▲", "▼", "≡"):
                self.assertNotIn(symbol, source)
            for path in re.findall(r'cozy/[a-z\-/]+\.png', source):
                self.assertTrue((ROOT / "OverlayResources/hud" / path).is_file(), path)
        self.assertIn("LoadInkIcon", read_source("CozyTheme.cs"))
        self.assertIn("OverlayNineSlice.TryGetBrush", read_source("CozyTheme.cs"))
        self.assertIn("slice.IsHitTestVisible = false", read_source("OverlayHudLayout.cs"))

    def test_dark_chrome_is_gone_and_cards_keep_noninteractive_behavior(self):
        for filename in ("MainWindow.xaml", "OverlayHoverPresenter.cs", "OverlayChatBubblePresenter.cs", "OverlayPetActor.cs"):
            source = read_source(filename)
            for old_color in ("#DD0F1820", "#F2141E28", "#FF0F1720", "Color.FromArgb(230, 15, 24, 32)", "Color.FromArgb(235, 27, 58, 74)", "Foreground = Brushes.White"):
                self.assertNotIn(old_color, source, filename)
        for filename in ("OverlayHoverPresenter.cs", "OverlayChatBubblePresenter.cs", "OverlayPetActor.cs"):
            self.assertIn("CozyTheme.SetIsPaper", read_source(filename))
            self.assertIn("IsHitTestVisible = false", read_source(filename))

    def test_menu_submenus_checks_and_input_host_remain_functional(self):
        theme = read_source("CozyTheme.xaml")
        for part in ('x:Name="PART_Popup"', 'Property="IsChecked"', 'Property="HasItems"', 'Property="local:CozyTheme.IsMenuPressed"', 'x:Name="PART_ContentHost"', 'x:Name="PART_Track"'):
            self.assertIn(part, theme)
        source = read_source("CozyTheme.cs")
        self.assertIn("MenuPressReset", source)
        self.assertNotIn("args.Handled", source)

    def test_transparency_and_960_by_560_design_canvas_are_preserved(self):
        window = ET.parse(ROOT / "MainWindow.xaml").getroot()
        for attribute, expected in (("Width", "960"), ("Height", "560"), ("AllowsTransparency", "True"), ("Background", "Transparent"), ("ShowActivated", "False")):
            self.assertEqual(expected, window.get(attribute))
        named = {element.get(XAML + "Name"): element for element in window.iter()}
        self.assertEqual("1", named["GameplayDebugDetailFlyout"].get("Grid.Column"))
        self.assertEqual("320", named["ChatDockChrome"].get("Width"))

    def test_export_is_local_presentation_only_and_does_not_overwrite_source(self):
        export = method_body(read_source("MainWindow.xaml.cs"), "ExportHudJson_OnClick")
        self.assertNotIn("SendCommand", export)
        self.assertNotIn("SendGameplayDebug", export)
        self.assertIn("ExportCozyJson", export)
        export_source = method_body(read_source("OverlayHudLayout.cs"), "ExportCozyJson")
        self.assertIn("Path.GetFullPath(destination)", export_source)
        self.assertIn("InvalidOperationException", export_source)
        self.assertIn("ApplyCozyResourceMapping(document)", export_source)

    def test_final_json_has_real_shared_components_and_preserves_widget_ids(self):
        path = ROOT / "OverlayResources/hud/overlay-hud.json"
        self.assertEqual(CONTRACT["final_hud_json_sha256"], hashlib.sha256(path.read_bytes()).hexdigest())
        document = json.loads(path.read_text())
        self.assertEqual(CONTRACT["hud_widget_ids"], [widget["id"] for widget in document["widgets"]])
        component = document["components"]["button"]
        self.assertEqual(3, component["pressedOffsetY"])
        self.assertEqual([64] * 4, component["spriteSlice"])
        for widget in document["widgets"]:
            with self.subTest(widget=widget["id"]):
                if widget["kind"] == "button":
                    for key in ("sprite", "spriteHover", "spritePressed", "spriteSlice"):
                        self.assertEqual(component[key], widget[key])
                    if not widget["id"].startswith("btn_pan_"):
                        self.assertTrue((ROOT / "OverlayResources/hud" / widget["iconSprite"]).is_file())
                if "fontColor" in widget:
                    self.assertEqual("#FF75472E", widget["fontColor"])
        source = read_source("OverlayHudLayout.cs")
        self.assertIn("ApplyCozyComponentParameters(_components)", source)
        self.assertIn('resources["CozyPressedOffset"] = components.button.pressedOffsetY', source)
        self.assertIn("ComponentPadding(_components.input.padding)", source)

    def test_final_chat_rows_and_reserved_hud_bands_do_not_overlap(self):
        widgets = {widget["id"]: widget for widget in json.loads(read_source("OverlayResources/hud/overlay-hud.json"))["widgets"]}
        log = widgets["chat_log"]
        input_panel = widgets["chat_input"]
        self.assertLessEqual(log["y"] + log["h"], input_panel["y"])
        for left, right in (("dock_chat", "dock_fishing"), ("dock_fishing", "cap_status"), ("cap_status", "menu_rail")):
            self.assertLessEqual(widgets[left]["x"] + widgets[left]["w"] + 8, widgets[right]["x"])
        self.assertLessEqual(widgets["btn_pan_right"]["x"] + widgets["btn_pan_right"]["w"], widgets["menu_rail"]["x"])


if __name__ == "__main__":
    unittest.main()
