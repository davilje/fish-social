import copy
import hashlib
import json
import math
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
WPF = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
XAML = "{http://schemas.microsoft.com/winfx/2006/xaml}"
SOURCE = (ROOT / "OverlayHudLayout.cs").read_text()
ORDER_MATCH = re.search(r"string\[\] MenuRailOrder\s*=\s*\{(.*?)\};", SOURCE, re.S)
MENU_ORDER = re.findall(r'"([^"]+)"', ORDER_MATCH.group(1)) if ORDER_MATCH else []
MIN_HEIGHT_MATCH = re.search(r"const double ChatInputMinHeight = (\d+);", SOURCE)
MIN_INPUT_HEIGHT = float(MIN_HEIGHT_MATCH.group(1)) if MIN_HEIGHT_MATCH else 0


def method_body(source, name):
    match = re.search(r"\b" + re.escape(name) + r"\s*\([^;{}]*\)\s*\{", source)
    if match is None:
        raise AssertionError("Missing method: " + name)
    start = match.end() - 1
    depth = 0
    for offset in range(start, len(source)):
        if source[offset] == "{":
            depth += 1
        elif source[offset] == "}":
            depth -= 1
        if depth == 0:
            return source[start:offset + 1]
    raise AssertionError("Unclosed method: " + name)


def refinement_model(document):
    document = copy.deepcopy(document)
    components = document["components"]
    padding = components["input"]["padding"]
    padding[1] = max(0, min(4, padding[1]))
    padding[3] = max(0, min(4, padding[3]))
    widgets = {widget["id"]: widget for widget in document["widgets"]}
    rail = widgets["menu_rail"]
    buttons = [widgets[identifier] for identifier in MENU_ORDER if identifier in widgets]
    slot_height = max(button["h"] for button in buttons)
    bottom = rail["y"] + rail["h"]
    rail["h"] = max(rail["h"], len(buttons) * slot_height)
    rail["y"] = bottom - rail["h"]
    for index, button in enumerate(buttons):
        button["x"] = (rail["w"] - button["w"]) / 2
        button["y"] = index * slot_height + (slot_height - button["h"]) / 2
    input_widget = widgets["chat_input"]
    placeholder = widgets["chat_placeholder"]
    font_size = placeholder.get("fontSize", 12)
    input_widget["h"] = max(input_widget["h"], MIN_INPUT_HEIGHT,
                            math.ceil(font_size * 1.5) + padding[1] + padding[3])
    placeholder.update(x=input_widget["x"] + padding[0], y=input_widget["y"] + padding[1],
                       w=max(8, input_widget["w"] - padding[0] - padding[2]),
                       h=input_widget["h"] - padding[1] - padding[3])
    send = widgets["chat_send"]
    send["h"] = max(send["h"], MIN_INPUT_HEIGHT)
    send["y"] = input_widget["y"] + (input_widget["h"] - send["h"]) / 2
    return document, widgets


class OverlayLayoutRefinementTests(unittest.TestCase):
    def setUp(self):
        self.document = json.loads((ROOT / "OverlayResources/hud/overlay-hud.json").read_text())

    def test_runtime_and_export_share_refinement_before_position_resolution(self):
        mapping = method_body(SOURCE, "ApplyCozyResourceMapping")
        self.assertIn("ApplyCozyLayoutRefinement(document)", mapping)
        load = method_body(SOURCE, "TryApply")
        self.assertLess(load.index("ApplyCozyResourceMapping(document)"), load.index("BuildAbsolutePositions(byId)"))
        export = method_body(SOURCE, "ExportCozyJson")
        self.assertLess(export.index("ApplyCozyResourceMapping(document)"), export.index("File.Create(destination)"))
        self.assertIn("InvalidOperationException", export)
        relayout = method_body(SOURCE, "Relayout")
        self.assertLess(relayout.index("RefineCompactMenuRailFrames"), relayout.index("foreach (var pair in frames)"))

    def test_known_icon_only_widgets_cannot_reintroduce_font_glyph_labels(self):
        mapping = method_body(SOURCE, "ApplyCozyResourceMapping")
        for statement in ('CozyTheme.IconPath(icon)', 'MenuRailOrder.Contains(widget.id)',
                          'widget.id == "chat_toggle"', 'widget.id == "chat_send"',
                          'widget.label = string.Empty', 'widget.contentAlign = "center"',
                          'widget.contentVAlign = "center"'):
            self.assertIn(statement, mapping)
        for icon in ("settings", "home", "lock", "map", "shop", "social", "bag", "trophy", "check"):
            data = (ROOT / f"OverlayResources/hud/cozy/icons/{icon}.png").read_bytes()
            self.assertTrue(data.startswith(b"\x89PNG\r\n\x1a\n"))
        renderer = method_body((ROOT / "CozyTheme.cs").read_text(), "LoadInkIcon")
        self.assertIn('path.Replace("cozy/icons-light/", "cozy/icons/")', renderer)
        self.assertIn("return source", renderer)
        self.assertNotIn("pixels[offset]", renderer)

    def test_menu_slots_are_unique_centered_and_keep_collapsed_entry_positions(self):
        self.assertEqual(9, len(MENU_ORDER))
        self.assertEqual(9, len(set(MENU_ORDER)))
        self.assertEqual(["btn_menu_toggle", "btn_open_main", "btn_exit_pond"], MENU_ORDER[:3])
        source = method_body(SOURCE, "ApplyCozyLayoutRefinement")
        for formula in ("button.x = (rail.w - button.w) / 2", "index * slotHeight + (slotHeight - button.h) / 2",
                        "rail.y = bottom - rail.h"):
            self.assertIn(formula, source)
        _, widgets = refinement_model(self.document)
        rail = widgets["menu_rail"]
        previous_bottom = 0
        for identifier in MENU_ORDER:
            button = widgets[identifier]
            self.assertEqual(rail["w"] / 2, button["x"] + button["w"] / 2)
            self.assertGreaterEqual(button["y"], previous_bottom)
            previous_bottom = button["y"] + button["h"]
        self.assertLessEqual(previous_bottom, rail["h"])
        for identifier, expected_y in zip(MENU_ORDER[:3], (0, 40, 80)):
            self.assertEqual(expected_y, widgets[identifier]["y"])

    def test_all_menu_slots_fit_each_supported_viewport(self):
        _, widgets = refinement_model(self.document)
        rail = widgets["menu_rail"]
        compact = method_body(SOURCE, "RefineCompactMenuRailFrames")
        for formula in ("availableHeight / buttons.Length", "Math.Min(button.h, slotHeight)",
                        "frame.Y + frame.H - slotHeight * buttons.Length"):
            self.assertIn(formula, compact)
        bottom_margin = 560 - rail["y"] - rail["h"]
        for view_height in (560, 400, 300):
            with self.subTest(view_height=view_height):
                available = view_height - bottom_margin - 8
                slot = 40 if available >= rail["h"] else max(24, available / len(MENU_ORDER))
                top = view_height - bottom_margin - slot * len(MENU_ORDER)
                self.assertGreaterEqual(top, 8)
                previous_bottom = top
                for index, identifier in enumerate(MENU_ORDER):
                    height = min(widgets[identifier]["h"], slot)
                    row_top = top + index * slot + (slot - height) / 2
                    self.assertGreaterEqual(row_top + 1e-9, previous_bottom)
                    previous_bottom = row_top + height
                self.assertLessEqual(previous_bottom, view_height - bottom_margin + 1e-9)

    def test_content_host_uses_one_padding_layer_and_real_vertical_alignment(self):
        theme = ET.parse(ROOT / "CozyTheme.xaml").getroot()
        host = next(element for element in theme.iter() if element.get(XAML + "Name") == "PART_ContentHost")
        self.assertIsNone(host.get("Margin"))
        self.assertEqual("{TemplateBinding Padding}", host.get("Padding"))
        self.assertEqual("{TemplateBinding VerticalContentAlignment}", host.get("VerticalAlignment"))
        self.assertEqual("Disabled", host.get("VerticalScrollBarVisibility"))
        self.assertEqual("False", host.get("Focusable"))
        padding = next(element for element in theme.iter() if element.get(XAML + "Key") == "CozyInputPadding")
        self.assertEqual("8,4,8,4", padding.text)

    def test_fallback_input_keeps_single_line_without_maxlines_height_cap(self):
        window = ET.parse(ROOT / "MainWindow.xaml").getroot()
        named = {element.get(XAML + "Name"): element for element in window.iter()}
        input_box = named["ChatInputBox"]
        self.assertEqual("36", input_box.get("Height"))
        self.assertEqual("36", input_box.get("MinHeight"))
        self.assertEqual("8,4", input_box.get("Padding"))
        self.assertIsNone(input_box.get("MaxLines"))
        self.assertEqual("False", input_box.get("AcceptsReturn"))
        self.assertEqual("NoWrap", input_box.get("TextWrapping"))
        self.assertEqual("200", input_box.get("MaxLength"))
        for name in ("Map", "Shop", "Friends", "Catch", "Leaderboard", "Settings"):
            self.assertTrue(named[f"Menu{name}Button"].get("ToolTip"))

    def test_runtime_padding_is_applied_after_normalize_in_both_host_paths(self):
        source = method_body(SOURCE, "ApplyChatInputChrome")
        self.assertLess(source.index("NormalizeForCanvas(textBox)"), source.index("textBox.Padding = ComponentPadding"))
        for statement in ("panel.Padding = new Thickness(0)", "textBox.MinHeight = inputWidget.h",
                          "textBox.Height = double.NaN", "textBox.AcceptsReturn = false",
                          "textBox.VerticalContentAlignment = VerticalAlignment.Center",
                          "placeholderBlock.FontFamily.LineSpacing",
                          "Math.Max(0, (placeholderWidget.h - lineHeight) / 2)"):
            self.assertIn(statement, source)

    def test_legacy_input_padding_and_rows_have_sufficient_modeled_line_space(self):
        complete = method_body(SOURCE, "CompleteCozyComponents")
        for statement in ("Math.Min(4, components.input.padding[1])", "Math.Min(4, components.input.padding[3])"):
            self.assertIn(statement, complete)
        geometry = method_body(SOURCE, "ApplyCozyLayoutRefinement")
        for formula in ("Math.Ceiling(fontSize * 1.5) + padding.Top + padding.Bottom",
                        "placeholder.y = input.y + padding.Top", "input.h - padding.Top - padding.Bottom",
                        "send.y = input.y + (input.h - send.h) / 2"):
            self.assertIn(formula, geometry)
        _, widgets = refinement_model(self.document)
        input_widget = widgets["chat_input"]
        placeholder = widgets["chat_placeholder"]
        send = widgets["chat_send"]
        self.assertEqual(36, input_widget["h"])
        self.assertEqual(28, placeholder["h"])
        center = input_widget["y"] + input_widget["h"] / 2
        self.assertEqual(center, placeholder["y"] + placeholder["h"] / 2)
        self.assertEqual(center, send["y"] + send["h"] / 2)
        self.assertLessEqual(widgets["chat_log"]["y"] + widgets["chat_log"]["h"], input_widget["y"])

    def test_larger_font_and_repeated_export_model_are_safe(self):
        for widget in self.document["widgets"]:
            if widget["id"] == "chat_placeholder":
                widget["fontSize"] = 24
        refined, widgets = refinement_model(self.document)
        self.assertGreaterEqual(widgets["chat_placeholder"]["h"], 36)
        repeated, _ = refinement_model(refined)
        self.assertEqual(refined, repeated)
        self.assertEqual([widget["id"] for widget in self.document["widgets"]],
                         [widget["id"] for widget in refined["widgets"]])

    def test_original_viewport_and_animation_method_hashes_are_preserved(self):
        contract = json.loads((ROOT / "tests/cozy_source_contract.json").read_text())
        for name, expected in contract["preserved_method_sha256"]["OverlayHudLayout.cs"].items():
            with self.subTest(method=name):
                self.assertEqual(expected, hashlib.sha256(method_body(SOURCE, name).encode()).hexdigest())


if __name__ == "__main__":
    unittest.main()
