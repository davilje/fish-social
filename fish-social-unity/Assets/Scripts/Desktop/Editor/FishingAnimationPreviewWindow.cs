#if UNITY_EDITOR
using FishSocial.Desktop.Pet;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Editor
{
    public sealed class FishingAnimationPreviewWindow : EditorWindow
    {
        enum FlowStage { None, Sit, Bait, Cast, Fishing, Hooked, Outcome }

        GameObject _previewRoot;
        SpriteFramePetRenderer _renderer;
        readonly bool[] _visibleLayers = { true, true, true, true, true, true, true, true, true };
        int _skinIndex;
        int _clipIndex;
        bool _playing;
        bool _emptyFlow;
        bool _showIndividualLayers = true;
        float _hookedLoopSeconds = 3f;
        float _stageSeconds;
        double _lastUpdate;
        FlowStage _flow;
        Vector2 _scroll;
        string _error;

        [MenuItem("Window/Fish Social/Fishing Animation Preview")]
        public static void Open()
        {
            GetWindow<FishingAnimationPreviewWindow>("Fishing Art Preview");
        }

        void OnEnable()
        {
            minSize = new Vector2(720f, 700f);
            _previewRoot = new GameObject("LocalFishingArtPreview", typeof(RectTransform), typeof(Image));
            _previewRoot.hideFlags = HideFlags.HideAndDontSave;
            _previewRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(640, 512);
            _renderer = _previewRoot.AddComponent<SpriteFramePetRenderer>();
            _renderer.IsPlaying = false;
            _renderer.Bind(_previewRoot.GetComponent<Image>());
            PlaySelected();
            _lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update += AdvancePreview;
        }

        void OnDisable()
        {
            EditorApplication.update -= AdvancePreview;
            if (_previewRoot != null)
                DestroyImmediate(_previewRoot);
            _previewRoot = null;
            _renderer = null;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("LOCAL ART PREVIEW — no backend phases, account changes, or fishing actions are sent.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            _skinIndex = EditorGUILayout.Popup("Source recolor skin", _skinIndex, FishingLayerArt.SkinIds);
            _clipIndex = EditorGUILayout.Popup("Clip", _clipIndex, FishingLayerArt.ClipNames);
            if (EditorGUI.EndChangeCheck())
                PlaySelected();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_playing ? "Pause" : "Play"))
                {
                    if (!_playing && _renderer != null && _renderer.ClipFinished)
                        PlaySelected();
                    _playing = !_playing;
                    _lastUpdate = EditorApplication.timeSinceStartup;
                }
                if (GUILayout.Button("Step frame") && _renderer != null)
                {
                    _playing = false;
                    _renderer.StepFrame();
                    AdvanceFlow(0f);
                }
                if (GUILayout.Button("Restart"))
                    PlaySelected();
            }

            _hookedLoopSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Hooked loop seconds", _hookedLoopSeconds));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview full success flow"))
                    StartFlow(false);
                if (GUILayout.Button("Preview full empty flow"))
                    StartFlow(true);
            }

            if (!string.IsNullOrEmpty(_error))
            {
                EditorGUILayout.HelpBox(_error, MessageType.Warning);
                return;
            }
            if (_renderer == null || _renderer.ActiveSession == null)
                return;

            var session = _renderer.ActiveSession;
            var frame = _renderer.FrameIndex;
            EditorGUILayout.LabelField("Active playback", _renderer.ActiveClip + " / " + session.Playback);
            EditorGUILayout.LabelField("Active source skin", _renderer.ActiveSkinId);
            EditorGUILayout.LabelField("Frame", (frame + 1) + " / " + session.FrameCount + " — " + session.DurationMs(frame) + " ms");
            EditorGUILayout.LabelField("Next clip", session.NextClip ?? "none (hold or authoritative change)");
            EditorGUILayout.LabelField("Layer order", string.Join(" → ", session.Order(frame)));
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (var layer = 0; layer < FishingLayerArt.LayerNames.Length; layer++)
                    _visibleLayers[layer] = GUILayout.Toggle(_visibleLayers[layer], FishingLayerArt.LayerNames[layer]);
            }
            var width = Mathf.Min(640f, Mathf.Max(100f, position.width - 35f));
            var composite = GUILayoutUtility.GetRect(width, width * 512f / 640f);
            EditorGUI.DrawRect(composite, new Color(0.16f, 0.18f, 0.21f));
            foreach (var layerName in session.Order(frame))
            {
                var layer = System.Array.IndexOf(FishingLayerArt.LayerNames, layerName);
                if (_visibleLayers[layer])
                    DrawLayer(composite, session.Frame(frame, layer));
            }

            _showIndividualLayers = EditorGUILayout.Toggle("Independent nine-layer view", _showIndividualLayers);
            if (_showIndividualLayers)
            {
                for (var row = 0; row < 3; row++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (var column = 0; column < 3; column++)
                        {
                            var layer = row * 3 + column;
                            using (new EditorGUILayout.VerticalScope())
                            {
                                GUILayout.Label(FishingLayerArt.LayerNames[layer]);
                                var rectangle = GUILayoutUtility.GetRect(width / 3f, width / 3f * 512f / 640f);
                                EditorGUI.DrawRect(rectangle, new Color(0.12f, 0.14f, 0.17f));
                                DrawLayer(rectangle, session.Frame(frame, layer));
                            }
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        static void DrawLayer(Rect rectangle, Sprite sprite)
        {
            if (sprite != null && sprite.texture != null)
                GUI.DrawTexture(rectangle, sprite.texture, ScaleMode.ScaleToFit, true);
        }

        void PlaySelected()
        {
            _flow = FlowStage.None;
            PlayLocal(FishingLayerArt.ClipNames[_clipIndex]);
        }

        bool PlayLocal(string clip)
        {
            var success = _renderer != null && _renderer.PlayClip(FishingLayerArt.SkinIds[_skinIndex], clip);
            _error = success ? null : "The selected skin/clip has no complete valid v2 pack. Put generated art under StreamingAssets/Fishing.";
            return success;
        }

        void StartFlow(bool empty)
        {
            _emptyFlow = empty;
            _stageSeconds = 0f;
            _flow = PlayLocal("sit") ? FlowStage.Sit : FlowStage.None;
            _playing = _flow != FlowStage.None;
            _lastUpdate = EditorApplication.timeSinceStartup;
        }

        void AdvancePreview()
        {
            var now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - _lastUpdate);
            _lastUpdate = now;
            if (!_playing || _renderer == null)
                return;
            _renderer.Tick(delta);
            AdvanceFlow(delta);
            Repaint();
        }

        void AdvanceFlow(float delta)
        {
            if (_flow == FlowStage.None || _renderer.ActiveSession == null)
                return;
            _stageSeconds += delta;
            switch (_flow)
            {
                case FlowStage.Sit:
                    if (_renderer.ActiveClip == "ready")
                        EnterStage(FlowStage.Bait, "bait");
                    break;
                case FlowStage.Bait:
                    if (_renderer.ClipFinished)
                        EnterStage(FlowStage.Cast, "cast");
                    break;
                case FlowStage.Cast:
                    if (_renderer.ClipFinished)
                        EnterStage(FlowStage.Fishing, "fishing");
                    break;
                case FlowStage.Fishing:
                    if (_stageSeconds >= 0.5f)
                        EnterStage(FlowStage.Hooked, "hooked");
                    break;
                case FlowStage.Hooked:
                    var introSeconds = 0f;
                    var session = _renderer.ActiveSession;
                    for (var frame = 0; frame < session.LoopStart; frame++)
                        introSeconds += session.DurationMs(frame) / 1000f;
                    if (_stageSeconds >= introSeconds + _hookedLoopSeconds)
                        EnterStage(FlowStage.Outcome, _emptyFlow ? "reel_empty" : "reel");
                    break;
                case FlowStage.Outcome:
                    if (_renderer.ActiveClip == "ready")
                    {
                        _flow = FlowStage.None;
                        _playing = false;
                    }
                    break;
            }
        }

        void EnterStage(FlowStage stage, string clip)
        {
            _stageSeconds = 0f;
            _flow = PlayLocal(clip) ? stage : FlowStage.None;
            if (_flow == FlowStage.None)
                _playing = false;
        }
    }
}
#endif
