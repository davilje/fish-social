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


RETIRED_MATCH = re.search(r"HashSet<string> RetiredWidgets\s*=.*?\{(.*?)\};", SOURCE, re.S)
RETIRED = re.findall(r'"([^"]+)"', RETIRED_MATCH.group(1)) if RETIRED_MATCH else []
ROWS_MATCH = re.search(r"const int ChatLogRows = (\d+);", SOURCE)
CHAT_LOG_ROWS = int(ROWS_MATCH.group(1)) if ROWS_MATCH else 0
CHAT_BUTTON = 28


def refinement_model(document):
    document = copy.deepcopy(document)
    document["widgets"] = [widget for widget in document["widgets"] if widget["id"] not in RETIRED]
    components = document["components"]
    padding = components["input"]["padding"]
    padding[1] = max(0, min(4, padding[1]))
    padding[3] = max(0, min(4, padding[3]))
    widgets = {widget["id"]: widget for widget in document["widgets"]}

    baseline = 560 - 12
    exit_button = widgets["btn_exit_pond"]
    exit_button.update(parentId=None, w=88, h=36, label="退出鱼塘")
    exit_button["x"] = 960 - 16 - exit_button["w"]
    exit_button["y"] = baseline - exit_button["h"]
    cap = widgets["cap_status"]
    cap["x"] = exit_button["x"] - 8 - cap["w"]
    cap["y"] = baseline - cap["h"]
    debug = widgets["btn_debug_gameplay"]
    debug.update(w=88, h=36, x=exit_button["x"])
    debug["y"] = exit_button["y"] - 8 - debug["h"]

    fishing = widgets["dock_fishing"]
    x = 0
    for identifier in ("btn_fishing_toggle", "btn_groundbait", "btn_catch_leave"):
        widgets[identifier].update(x=x, y=0, w=88, h=36, fontSize=13, fontFile=None, contentAlign="center")
        x += 88 + 8
    fishing.update(w=x - 8, h=36, y=baseline - 36)
    chat = widgets["dock_chat"]
    left, right = chat["x"] + chat["w"] + 12, cap["x"] - 12
    fishing["x"] = round(left + (right - left - fishing["w"]) / 2)

    dock = widgets["dock_chat"]
    toggle_x = dock["w"] - 10 - CHAT_BUTTON
    log = widgets["chat_log"]
    row = round(log.get("fontSize", 12) * 1.34) + 4
    scroll_x = toggle_x + CHAT_BUTTON - 6
    log.update(x=12, y=12, w=max(8, scroll_x - 4 - 12), h=CHAT_LOG_ROWS * row + 2)
    widgets["chat_scroll"].update(x=scroll_x, y=12, w=6, h=log["h"])
    input_widget = widgets["chat_input"]
    placeholder = widgets["chat_placeholder"]
    font_size = placeholder.get("fontSize", 12)
    send_x = toggle_x - 6 - CHAT_BUTTON
    input_widget.update(x=12, y=log["y"] + log["h"] + 8, w=max(8, send_x - 6 - 12))
    input_widget["h"] = max(input_widget["h"], MIN_INPUT_HEIGHT,
                            math.ceil(font_size * 1.5) + padding[1] + padding[3])
    placeholder.update(x=input_widget["x"] + padding[0], y=input_widget["y"] + padding[1],
                       w=max(8, input_widget["w"] - padding[0] - padding[2]),
                       h=input_widget["h"] - padding[1] - padding[3])
    send = widgets["chat_send"]
    send.update(x=send_x, w=CHAT_BUTTON, h=CHAT_BUTTON)
    send["y"] = input_widget["y"] + (input_widget["h"] - send["h"]) / 2
    toggle = widgets["chat_toggle"]
    toggle.update(x=toggle_x, w=CHAT_BUTTON, h=CHAT_BUTTON, y=max(0, (dock["h"] - CHAT_BUTTON) / 2))
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

    def test_menu_rail_is_retired_and_main_window_moves_to_context_menu(self):
        for identifier in ("menu_rail", "btn_menu_toggle", "btn_open_main", "btn_menu_settings"):
            self.assertIn(identifier, RETIRED)
        exported = {widget["id"] for widget in self.document["widgets"]}
        self.assertFalse(exported & set(RETIRED))
        self.assertIn("RetiredWidgets.Contains", method_body(SOURCE, "ApplyCozyResourceMapping"))
        menu = method_body((ROOT / "MainWindow.xaml.cs").read_text(encoding="utf-8"), "BuildProductContextMenu")
        self.assertIn('CreateProductMenuItem("打开主界面", OpenMain_OnClick)', menu)

    def test_bottom_right_cluster_keeps_exit_status_and_debug_apart(self):
        source = method_body(SOURCE, "FitBottomRightCluster")
        for formula in ("exit.x = CanvasWidth - margin - exit.w", "cap.x = exit.x - gap - cap.w",
                        'StyleBottomButton(exit, 88, "退出鱼塘")', "debug.y = top - gap - debug.h"):
            self.assertIn(formula, source)
        _, widgets = refinement_model(self.document)
        exit_button, cap, debug = widgets["btn_exit_pond"], widgets["cap_status"], widgets["btn_debug_gameplay"]
        fishing = widgets["dock_fishing"]
        self.assertEqual((856, 512, 88, 36), (exit_button["x"], exit_button["y"], exit_button["w"], exit_button["h"]))
        self.assertLessEqual(cap["x"] + cap["w"], exit_button["x"])
        self.assertEqual(exit_button["x"], debug["x"])
        self.assertLessEqual(debug["y"] + debug["h"], exit_button["y"])
        self.assertLessEqual(fishing["x"] + fishing["w"], cap["x"])
        self.assertLessEqual(widgets["dock_chat"]["x"] + widgets["dock_chat"]["w"], fishing["x"])
        bottoms = {exit_button["y"] + exit_button["h"], cap["y"] + cap["h"], fishing["y"] + fishing["h"],
                   widgets["dock_chat"]["y"] + widgets["dock_chat"]["h"]}
        self.assertEqual({548}, bottoms)

    def test_fishing_dock_buttons_share_size_font_and_centered_content(self):
        source = method_body(SOURCE, "StyleBottomButton")
        for statement in ("button.fontFile = null", 'button.contentAlign = "center"', "button.h = BottomButtonHeight"):
            self.assertIn(statement, source)
        _, widgets = refinement_model(self.document)
        buttons = [widgets[i] for i in ("btn_fishing_toggle", "btn_groundbait", "btn_catch_leave")]
        self.assertEqual({(88, 36)}, {(b["w"], b["h"]) for b in buttons})
        self.assertEqual([0, 96, 192], [b["x"] for b in buttons])
        theme = (ROOT / "CozyTheme.xaml").read_text(encoding="utf-8")
        self.assertNotIn('Margin="1,1,0,0"', theme)

    def test_chat_dock_expands_to_ten_rows_and_toggle_follows_send_row(self):
        self.assertEqual(10, CHAT_LOG_ROWS)
        metrics = method_body(SOURCE, "BuildChatDockMetrics")
        self.assertIn('"chat_send"', metrics)
        _, widgets = refinement_model(self.document)
        dock, toggle = widgets["dock_chat"], widgets["chat_toggle"]
        self.assertEqual(320, dock["w"])
        self.assertEqual(dock["h"] / 2, toggle["y"] + toggle["h"] / 2)
        send = widgets["chat_send"]
        self.assertEqual(toggle["x"] - 6 - send["w"], send["x"])
        self.assertEqual(toggle["x"] + toggle["w"], widgets["chat_scroll"]["x"] + widgets["chat_scroll"]["w"])
        self.assertLessEqual(widgets["chat_log"]["x"] + widgets["chat_log"]["w"], widgets["chat_scroll"]["x"])

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
        geometry = method_body(SOURCE, "FitChatDock")
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
