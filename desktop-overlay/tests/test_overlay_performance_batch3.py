"""Production integration guards supplement portable math and Windows native tests."""
from pathlib import Path
import unittest
from test_cozy_ui_source import method_body
ROOT = Path(__file__).resolve().parents[1]

class Batch3Tests(unittest.TestCase):
    def test_pointer_probe_never_decodes_or_copies_pixels(self):
        source = (ROOT / 'OverlayClickThrough.cs').read_text()
        for work in ('CopyPixels', 'FormatConvertedBitmap', 'File.', '_cachedBgra', 'EnsureAlphaCache'):
            self.assertNotIn(work, source)
        self.assertIn('OverlayStaticArt.TryGetAlpha', source)
        self.assertIn('pixelAlpha * mask >= AlphaThreshold', source)

    def test_static_publish_is_guarded_and_uses_prepared_alpha(self):
        source = (ROOT / 'PondScenePresenter.cs').read_text()
        for name in ('TryLoadPondBackground', 'PlaceSpotMarker', 'ApplyLayoutSprites'):
            body = method_body(source,name)
            self.assertIn('await ',body)
            self.assertIn('version != _artVersion',body)
        dispose = method_body(source,'Dispose')
        self.assertIn('_artLoad.Cancel()',dispose)
        load = method_body(source,'TryLoadPondBackground')
        self.assertIn('LoadAsync(path, true, token)',load)
        self.assertNotIn('LoadBitmap(',source)
        self.assertNotIn('.TryLoad(pondId, layoutSpot',source)
        self.assertNotIn('.CreateSpriteImage(sprite',source)

    def test_cache_payloads_have_independent_limits(self):
        static = (ROOT / 'OverlayStaticArt.cs').read_text()
        nine = (ROOT / 'OverlayNineSlice.cs').read_text()
        self.assertIn('48L * 1024 * 1024',static)
        self.assertIn('16L * 1024 * 1024',nine)
        self.assertIn('ConditionalWeakTable<BitmapSource, OverlayAlphaMap>',static)
        self.assertNotIn('RuntimeHelpers.GetHashCode(source)',nine)
        self.assertNotIn('BrushCache',nine)
        self.assertNotIn('RenderCache',nine)

    def test_diagnostics_and_combined_mask_are_opt_in_and_disposed(self):
        diag = (ROOT / 'OverlayRenderDiagnostics.cs').read_text()
        self.assertIn('FISH_SOCIAL_OVERLAY_PROFILE',diag)
        self.assertIn('visible && _enabled',diag)
        self.assertIn('CompositionTarget.Rendering -= OnRendering',method_body(diag,'Dispose'))
        fade = (ROOT / 'OverlayEdgeVignette.cs').read_text()
        self.assertIn('FISH_SOCIAL_OVERLAY_COMBINED_MASK',fade)
        self.assertIn('if (!_combined) return;',method_body(fade,'ApplySize'))
        self.assertIn('version != _version',method_body(fade,'ApplySize'))
        closed = method_body((ROOT/'MainWindow.xaml.cs').read_text(),'OnClosing')
        self.assertIn('_edgeVignette?.Dispose()',closed)
        self.assertIn('_renderDiagnostics?.Dispose()',closed)

    def test_existing_audio_cues_keep_single_routed_delivery(self):
        # The audio feature is committed; protect its contract when refreshing the
        # three stale pre-audio hashes, rather than silently dropping those guards.
        app = (ROOT/'App.xaml.cs').read_text()
        for method in ('OnButtonActivated','OnMenuItemActivated'):
            self.assertIn('ReferenceEquals(sender, e.Source)',method_body(app,method))
        self.assertIn('!item.HasItems',method_body(app,'OnMenuItemActivated'))
        window = (ROOT/'MainWindow.xaml.cs').read_text()
        audio = method_body(window,'PlayInteractionAudio')
        self.assertIn('Command = "ui_audio"',audio)
        self.assertIn('OverlayUiAudioPolicy.Allowed(cue)', audio)
        self.assertNotIn('SendCommand(',audio)
        right = method_body(window,'PondScene_PreviewMouseRightButtonUp')
        self.assertIn('pet.TryOpenPlayerContextMenu()',right)
        self.assertIn('else if (product)',right)
