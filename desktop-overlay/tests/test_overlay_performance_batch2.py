"""Integration guards; behavioral scheduling runs against the production C# core."""
from pathlib import Path
import unittest
from test_overlay_layout_refinement import method_body

ROOT = Path(__file__).resolve().parents[1]

class Batch2Tests(unittest.TestCase):
    def test_ui_frame_path_only_requests_async_images(self):
        actor = (ROOT / 'OverlayPetActor.cs').read_text()
        self.assertNotIn('BitmapFor(', actor)
        render = method_body(actor, 'RenderLatestFrame')
        self.assertIn('Task.WhenAll', render)
        self.assertLess(render.index('version != _renderVersion'), render.index('_layerImages[i].Source ='))
        self.assertLess(render.index('if (image == null)'), render.index('_layerImages[i].Source ='))
        self.assertIn('_stateVersion++', method_body(actor, 'Dispose'))
        self.assertIn('AfterTick -= RenderLatestFrame', method_body(actor, 'Dispose'))

    def test_actor_and_chat_do_not_force_wpf_layout(self):
        for name in ('OverlayPetActor.cs', 'OverlayChatPresenter.cs'):
            self.assertNotIn('UpdateLayout()', (ROOT / name).read_text())
        chat = (ROOT / 'OverlayChatPresenter.cs').read_text()
        self.assertNotIn('_logList.Children.Clear()', chat)
        self.assertIn('if (_expanded)', method_body(chat, 'UpdateMessages'))
        window = (ROOT / 'MainWindow.xaml.cs').read_text()
        self.assertIn('_chat?.SetExpanded(_chatDockExpanded)', method_body(window, 'ChatDockToggle_OnClick'))
        self.assertIn('_chat?.SetExpanded(expanded)', method_body(window, 'ApplyHudChatDockLayout'))

    def test_manifest_validation_is_separated_from_dispatcher_cache(self):
        source = (ROOT / 'OverlayFishingLayers.cs').read_text()
        self.assertNotIn('File.Exists', method_body(source, 'TryOpen'))
        read = method_body(source, 'ReadPack')
        self.assertIn('File.Exists', read)
        self.assertNotIn('TextureScales[', read)
        decode = method_body(source, 'DecodeBitmap')
        self.assertIn('image.Freeze()', decode)
        self.assertNotIn('Bitmaps.', decode)
        self.assertIn('DecodeQueue.TryRun', method_body(source, 'BitmapAsync'))

    def test_shared_clock_and_visibility_are_connected(self):
        actor = (ROOT / 'OverlayPetActor.cs').read_text()
        self.assertIn('OverlayAnimationClock.Create(125)', actor)
        self.assertIn('_frameTimer.Suspended = !visible', method_body(actor, 'SetPresentationVisible'))
        clock = (ROOT / 'OverlayAnimationClock.cs').read_text()
        self.assertIn('Stopwatch.StartNew()', clock)
        window = (ROOT / 'MainWindow.xaml.cs').read_text()
        self.assertIn('IsVisibleChanged +=', window)
        self.assertIn('ViewportChanged += RefreshPresentationViewport', window)
        self.assertIn('OverlayAnimationClock.SetVisible(visible)', method_body(window, 'RefreshPresentationViewport'))
