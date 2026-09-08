using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BrugesWaffleMonster.Editor
{
    /// <summary>
    /// One-shot editor helper that drops a throwaway, art-free sandbox into the open scene
    /// so player movement can be exercised in Play mode: a placeholder monster, a floor, two
    /// climbable walls, and on-screen tap controls. Nothing here ships in a build.
    ///
    /// Run it from <b>Tools ▸ Bruges Waffle Monster ▸ Build Player Movement Sandbox</b>.
    /// It refuses to run twice (delete the "PlayerMovementSandbox" object to regenerate).
    /// </summary>
    public static class PlayerSandboxBuilder
    {
        private const string MenuPath = "Tools/Bruges Waffle Monster/Build Player Movement Sandbox";
        private const string RootName = "PlayerMovementSandbox";
        private const string ConfigDir = "Assets/ScriptableObjects/Player";
        private const string ConfigAssetPath = ConfigDir + "/PlayerMovementConfig.asset";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Leave Play mode before building the player movement sandbox.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("Open a scene before building the player movement sandbox.");
                return;
            }

            if (Object.FindFirstObjectByType<PlayerController>() != null)
            {
                Debug.LogWarning($"Scene already contains a {nameof(PlayerController)} — sandbox not rebuilt. " +
                                 $"Delete the '{RootName}' object to regenerate it.");
                return;
            }

            PlayerMovementConfig config = LoadOrCreateConfig();
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (sprite == null)
            {
                Debug.LogWarning("Built-in UI sprite not found; placeholder bodies will be invisible but still collide. " +
                                 "Select the objects to see their gizmos.");
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Player Movement Sandbox");

            TapPlayerInput input = BuildPlayer(root.transform, config, sprite);
            BuildGround(root.transform, sprite);
            BuildClimbWall(root.transform, "ClimbWall_Left", new Vector2(-6f, -0.5f), sprite);
            BuildClimbWall(root.transform, "ClimbWall_Right", new Vector2(6f, 0f), sprite);
            BuildControls(root.transform, input);
            EnsureEventSystem(root.transform);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Player movement sandbox built. Press Play — hold ◀ / ▶ to run, tap JUMP, " +
                      "walk into a translucent wall to auto-climb. Keyboard: A/D + Space also work.");
        }

        [MenuItem(MenuPath, isValidateFunction: true)]
        private static bool ValidateBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static TapPlayerInput BuildPlayer(Transform parent, PlayerMovementConfig config, Sprite sprite)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(0f, -1.5f, 0f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.gravityScale = config != null ? config.GravityScale : 3f;

            var col = go.AddComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(0.9f, 1.4f);

            AddVisual(go.transform, sprite, new Vector2(0.9f, 1.4f), new Color(0.95f, 0.75f, 0.35f), sortingOrder: 10);

            var feet = new GameObject("GroundCheck");
            feet.transform.SetParent(go.transform);
            feet.transform.localPosition = new Vector3(0f, -0.75f, 0f);

            var tapInput = go.AddComponent<TapPlayerInput>();
            var player = go.AddComponent<PlayerController>();

            var so = new SerializedObject(player);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("inputSourceBehaviour").objectReferenceValue = tapInput;
            so.FindProperty("groundCheck").objectReferenceValue = feet.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            return tapInput;
        }

        private static void BuildGround(Transform parent, Sprite sprite)
        {
            var go = new GameObject("Placeholder_Ground");
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(0f, -3f, 0f);

            go.AddComponent<BoxCollider2D>().size = new Vector2(40f, 1f);
            AddVisual(go.transform, sprite, new Vector2(40f, 1f), new Color(0.30f, 0.30f, 0.32f), sortingOrder: 0);
        }

        private static void BuildClimbWall(Transform parent, string name, Vector2 position, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = position;

            // Trigger (not solid) so the placeholder climb reads cleanly — the monster slides
            // straight up through the translucent wall instead of being shoved off it.
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 6f);
            col.isTrigger = true;

            go.AddComponent<ClimbableSurface>();
            AddVisual(go.transform, sprite, new Vector2(0.8f, 6f), new Color(0.35f, 0.55f, 0.95f, 0.5f), sortingOrder: 5);
        }

        private static void AddVisual(Transform parent, Sprite sprite, Vector2 worldSize, Color color, int sortingOrder)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = color;
            sr.sortingOrder = sortingOrder;

            if (sprite != null)
            {
                sr.sprite = sprite;
                Vector2 spriteSize = sprite.bounds.size;
                go.transform.localScale = new Vector3(
                    spriteSize.x > 0f ? worldSize.x / spriteSize.x : worldSize.x,
                    spriteSize.y > 0f ? worldSize.y / spriteSize.y : worldSize.y,
                    1f);
            }
        }

        private static void BuildControls(Transform parent, TapPlayerInput input)
        {
            var canvasGo = new GameObject("Sandbox_Controls", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent);

            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            Font font = AssetDatabase.GetBuiltinExtraResource<Font>("LegacyRuntime.ttf");
            Transform canvas = canvasGo.transform;

            CreateHoldButton(canvas, "Btn_Left", "◀", font, new Vector2(0f, 0f), new Vector2(60f, 60f),
                input.PressLeft, input.ReleaseLeft);
            CreateHoldButton(canvas, "Btn_Right", "▶", font, new Vector2(0f, 0f), new Vector2(360f, 60f),
                input.PressRight, input.ReleaseRight);
            CreateTapButton(canvas, "Btn_Jump", "JUMP", font, new Vector2(1f, 0f), new Vector2(-60f, 60f),
                input.PressJump);
        }

        private static void CreateHoldButton(Transform parent, string name, string label, Font font,
            Vector2 anchor, Vector2 anchoredPosition, UnityAction onPress, UnityAction onRelease)
        {
            RectTransform rt = MakePanel(parent, name, anchor, anchoredPosition, new Vector2(260f, 260f),
                new Color(1f, 1f, 1f, 0.25f));
            AddLabel(rt, label, font);

            var hold = rt.gameObject.AddComponent<HoldButton>();
            hold.OnPress = new UnityEvent();
            hold.OnRelease = new UnityEvent();
            UnityEventTools.AddPersistentListener(hold.OnPress, onPress);
            UnityEventTools.AddPersistentListener(hold.OnRelease, onRelease);
        }

        private static void CreateTapButton(Transform parent, string name, string label, Font font,
            Vector2 anchor, Vector2 anchoredPosition, UnityAction onClick)
        {
            RectTransform rt = MakePanel(parent, name, anchor, anchoredPosition, new Vector2(300f, 300f),
                new Color(1f, 1f, 1f, 0.30f));
            AddLabel(rt, label, font);

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = rt.GetComponent<Image>();
            UnityEventTools.AddPersistentListener(button.onClick, onClick);
        }

        private static RectTransform MakePanel(Transform parent, string name, Vector2 anchor,
            Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;

            go.GetComponent<Image>().color = color;
            return rt;
        }

        private static void AddLabel(Transform parent, string text, Font font)
        {
            if (font == null)
            {
                return;
            }

            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = font;
            label.fontSize = 96;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        private static void EnsureEventSystem(Transform root)
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            // Menu command so Unity attaches the input module matching the project's input backend.
            EditorApplication.ExecuteMenuItem("GameObject/UI/Event System");

            var created = Object.FindFirstObjectByType<EventSystem>();
            if (created != null && created.transform.parent == null)
            {
                created.transform.SetParent(root);
            }
        }

        private static PlayerMovementConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(ConfigAssetPath);
            if (config != null)
            {
                return config;
            }

            if (!AssetDatabase.IsValidFolder(ConfigDir))
            {
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Player");
            }

            config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created {ConfigAssetPath} (placeholder tuning — see Docs/GameDesign.md).");
            return config;
        }
    }
}
