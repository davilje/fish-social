"""Integration guards for the portable cache and Windows lifecycle regression suite."""
import json
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET
from test_overlay_layout_refinement import method_body

ROOT = Path(__file__).resolve().parents[1]


class OverlayPerformanceLifecycleTests(unittest.TestCase):
    def test_all_removal_paths_dispose_the_actor(self):
        source = (ROOT / 'PondScenePresenter.cs').read_text()
        for name in ('SyncOthers', 'ClearOthers'):
            body = method_body(source, name)
            self.assertIn('.Dispose()', body)
            self.assertNotIn('.ReleaseFishingLayers()', body)
        closed = method_body((ROOT / 'MainWindow.xaml.cs').read_text(), 'OnClosing')
        self.assertIn('_scene?.Dispose()', closed)
        self.assertLess(closed.index('_chatBubbles?.HideAll()'), closed.index('_scene?.Dispose()'))

    def test_disposal_is_idempotent_and_unhooks_every_actor_timer(self):
        body = method_body((ROOT / 'OverlayPetActor.cs').read_text(), 'Dispose')
        self.assertIn('if (_disposed) return;', body)
        for timer, handler in (('_frameTimer', 'OnFrameTick'), ('_refreshTimer', 'OnRefreshTick'),
                               ('_tooltipTimer', 'OnTooltipTimerTick')):
            self.assertIn(timer + '.Stop()', body)
            self.assertIn(timer + '.Tick -= ' + handler, body)
        self.assertIn('StopSpeechFade()', body)
        self.assertIn('ReleaseFishingLayers()', body)
        self.assertIn('DiscardSocialMenu()', body)

    def test_layout_is_known_before_first_warmup_and_frame(self):
        scene = (ROOT / 'PondScenePresenter.cs').read_text()
        own = method_body(scene, 'ApplyOwnActor')
        self.assertLess(own.index('.ApplyScenePlacement('), own.index('.WarmupActionClips('))
        self.assertLess(own.index('.WarmupActionClips('), own.index('_ownActor.Apply('))
        others = method_body(scene, 'SyncOthers')
        self.assertLess(others.index('actor.ApplyScenePlacement('), others.index('actor.Apply('))
        actor = (ROOT / 'OverlayPetActor.cs').read_text()
        warmup = method_body(actor, 'WarmupActionClips')
        self.assertIn('id, _body.Width, _body.Height', warmup)
        self.assertIn('drawW == _warmedDrawW && drawH == _warmedDrawH', warmup)
        self.assertNotIn('SetDecodeSize(', actor)

    def test_clip_references_use_shared_paths_and_never_drop_other_species(self):
        source = (ROOT / 'OverlayFishingLayers.cs').read_text()
        self.assertIn('Bitmaps.Retain(resources)', method_body(source, 'Acquire'))
        self.assertIn('LayerPath(petId, clip, frame, layer, pack.BodyLayers)', method_body(source, 'Acquire'))
        self.assertIn('Bitmaps.Release(resources)', method_body(source, 'Release'))
        self.assertNotIn('DropClip(', source)
        self.assertNotIn('Bitmaps.Clear()', source)
        self.assertIn('64L * 1024 * 1024', source)
        self.assertIn('stride * image.PixelHeight', source)

    def test_production_build_excludes_test_sources_without_changing_runtime(self):
        root = ET.parse(ROOT / 'FishSocialOverlay.csproj').getroot()
        self.assertEqual('netcoreapp3.1', root.findtext('./PropertyGroup/TargetFramework'))
        self.assertTrue(any('tests' in item.get('Remove', '') for item in root.iter('Compile')))
        for name in ('FishSocialOverlay.CacheTests.csproj', 'FishSocialOverlay.PerformanceTests.csproj'):
            ET.parse(ROOT / 'tests' / name)


if __name__ == '__main__':
    unittest.main()
