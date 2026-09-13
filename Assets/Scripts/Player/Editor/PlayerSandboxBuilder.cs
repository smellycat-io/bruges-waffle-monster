using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BrugesWaffleMonster.Editor
{
    /// <summary>
    /// One-shot editor helper that drops a throwaway, art-free sandbox into the open scene
    /// so player movement can be exercised in Play mode: a placeholder monster (with the
    /// tap-zone run/jump driver and the drag-to-climb driver), a floor, and two climbable
    /// walls. Nothing here ships in a build.
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

            if (Object.FindAnyObjectByType<PlayerController>() != null)
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

            BuildPlayer(root.transform, config, sprite);
            BuildGround(root.transform, sprite);
            BuildClimbWall(root.transform, "ClimbWall_Left", new Vector2(-6f, -0.5f), sprite);
            BuildClimbWall(root.transform, "ClimbWall_Right", new Vector2(6f, 0f), sprite);
            BuildZoneOverlay(root.transform);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Player movement sandbox built. Press Play — hold the LEFT / RIGHT half of the screen " +
                      "(or A/D) to run, quick-tap either half (or Space) to jump. Touch a translucent wall to " +
                      "grab it, then drag up/down (or Up/Down arrows) to climb; release to hang. Quick-tap while " +
                      "climbing to wall-jump off.");
        }

        [MenuItem(MenuPath, isValidateFunction: true)]
        private static bool ValidateBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void BuildPlayer(Transform parent, PlayerMovementConfig config, Sprite sprite)
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
            var zoneInput = go.AddComponent<ScreenTapZoneInput>();
            var climbInput = go.AddComponent<ClimbDragInput>();
            var player = go.AddComponent<PlayerController>();

            var zoneSo = new SerializedObject(zoneInput);
            zoneSo.FindProperty("target").objectReferenceValue = tapInput;
            zoneSo.ApplyModifiedPropertiesWithoutUndo();

            var climbSo = new SerializedObject(climbInput);
            climbSo.FindProperty("target").objectReferenceValue = tapInput;
            climbSo.ApplyModifiedPropertiesWithoutUndo();

            var playerSo = new SerializedObject(player);
            playerSo.FindProperty("config").objectReferenceValue = config;
            playerSo.FindProperty("inputSourceBehaviour").objectReferenceValue = tapInput;
            playerSo.FindProperty("groundCheck").objectReferenceValue = feet.transform;
            playerSo.ApplyModifiedPropertiesWithoutUndo();
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

        /// <summary>
        /// Subtle, non-interactive tint marking the left/right tap halves. Purely a debug aid —
        /// the actual input comes from <see cref="ScreenTapZoneInput"/> reading raw pointers,
        /// not from this UI, so everything here has raycastTarget off.
        /// </summary>
        private static void BuildZoneOverlay(Transform parent)
        {
            var canvasGo = new GameObject("Sandbox_ZoneOverlay", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(parent);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;

            AddOverlayRect(canvasGo.transform, "LeftZone", new Vector2(0f, 0f), new Vector2(0.5f, 1f),
                new Color(0.3f, 0.6f, 1f, 0.06f));
            AddOverlayRect(canvasGo.transform, "RightZone", new Vector2(0.5f, 0f), new Vector2(1f, 1f),
                new Color(1f, 0.6f, 0.3f, 0.06f));
            AddOverlayRect(canvasGo.transform, "SplitLine", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Color(1f, 1f, 1f, 0.15f));
        }

        private static void AddOverlayRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            if (Mathf.Approximately(anchorMin.x, anchorMax.x))
            {
                rt.sizeDelta = new Vector2(2f, 0f); // zero-width anchors -> a visible hairline
            }

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
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
