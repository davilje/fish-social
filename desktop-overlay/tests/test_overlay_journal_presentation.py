"""Presentation contract: desktop assets, no outcome modal/status, preserved fishing commands."""
import hashlib
import json
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET
from test_overlay_layout_refinement import method_body
ROOT=Path(__file__).resolve().parents[1]
class JournalPresentationTests(unittest.TestCase):
    def test_desktop_sources_and_overlay_assets_are_identical(self):
        manifest=json.loads((ROOT/'OverlayResources/hud/journal-assets.json').read_text())
        self.assertEqual(39,len(manifest['assets']))
        for asset in manifest['assets']:
            a=(ROOT.parent/asset['source']).read_bytes(); b=(ROOT/'OverlayResources/hud'/asset['file']).read_bytes()
            self.assertEqual(a,b,asset['file']);self.assertEqual(asset['sha256'],hashlib.sha256(b).hexdigest())
        theme=(ROOT/'CozyTheme.cs').read_text()
        self.assertIn('new TransformedBitmap(image, new ScaleTransform(scale, scale))',theme)
        self.assertIn('return CozyTheme.LoadImage(spriteFile)',(ROOT/'OverlayHudLayout.cs').read_text())
    def test_status_is_absent_even_when_json_is_missing_or_legacy(self):
        markup=(ROOT/'MainWindow.xaml').read_text();source=(ROOT/'MainWindow.xaml.cs').read_text()
        for name in ['StatusCapsule','StateText','PondText','SpotText']:
            self.assertNotIn('x:Name="'+name+'"',markup);self.assertNotIn(name+'.Text',source)
        self.assertIn('x:Name="ErrorText"',markup)
        self.assertNotIn('StatusCapsule',(ROOT/'OverlayClickThrough.cs').read_text())
        self.assertNotIn('"cap_status"',(ROOT/'OverlayHudWidgets.cs').read_text())
    def test_outcomes_are_suppressed_without_ack_or_gameplay_command(self):
        source=(ROOT/'MainWindow.xaml.cs').read_text();body=method_body(source,'ApplyOverlayPrompt')
        self.assertIn('string.IsNullOrWhiteSpace(kind) || IsHookSettlePrompt(kind)',body)
        hide=body[:body.index('OverlayPromptTitle.Text')]
        self.assertIn('Visibility.Collapsed',hide);self.assertIn('_promptTimer.Stop()',hide)
        self.assertNotIn('SendCommand',hide)
        for kind in ['hook_keep','hook_return','hook_escape']:
            self.assertIn('"'+kind+'"',method_body(source[source.index('static bool IsHookSettlePrompt'):],'IsHookSettlePrompt'))
        self.assertIn('SendOwnAction("leave_spot")',method_body(source,'CatchLeave_OnClick'))
        self.assertIn('SendCommand("exit_pond")',method_body(source,'ExitPond_OnClick'))
    def test_ui_fades_have_no_scale_bounce(self):
        for file in ['OverlayPetActor.cs','OverlayChatBubblePresenter.cs']:
            body=method_body((ROOT/file).read_text(),'RunPopIn')
            self.assertIn('TimeSpan.FromSeconds(.12)',body);self.assertNotIn('ScaleTransform',body)
        self.assertNotIn('HudButtonPressScale_OnClick',(ROOT/'OverlayHudLayout.cs').read_text())
        for file in ['MainWindow.xaml','CozyTheme.xaml']:ET.parse(ROOT/file)
if __name__=='__main__': unittest.main()
