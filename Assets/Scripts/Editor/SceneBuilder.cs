using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SlotGame.Audio;
using SlotGame.Data;
using SlotGame.Game;
using SlotGame.Reels;
using SlotGame.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SlotGame.EditorTools
{
    /// <summary>
    /// Generates the whole game from the raw art: sprite import settings, symbol and config
    /// assets, animation clips, prefabs and the playable scene.
    ///
    /// Why generate instead of hand-placing? The layout is reproducible, reviewable as code,
    /// and can be rebuilt from the command line:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod SlotGame.EditorTools.SceneBuilder.Build
    ///
    /// The builder is idempotent. Running it again regenerates the scene and prefabs, but keeps
    /// the existing Symbol and SlotConfig assets, so values tuned in the Inspector survive.
    /// </summary>
    public static class SceneBuilder
    {
        // ---------- Paths ----------
        private const string MachineArtFolder = "Assets/Art/Machine";
        private const string SymbolArtFolder = "Assets/Art/Symbols";
        private const string UISpriteFolder = "Assets/UI/Sprites";
        private const string SoundsFolder = "Assets/Sounds";
        private const string AnimationsFolder = "Assets/Animations";
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string DataFolder = "Assets/Data";
        private const string SymbolDataFolder = "Assets/Data/Symbols";
        private const string ScenePath = "Assets/Scenes/SlotGame.unity";
        private const string ConfigPath = DataFolder + "/SlotConfig.asset";
        private const string CustomFontPath = "Assets/UI/Fonts/LuckiestGuy-Regular.ttf";
        private const string FxFolder = UISpriteFolder + "/FX";

        // ---------- Machine art geometry (pixels in the 816x624 machine images) ----------
        // Measured from the alpha channel of slot-machine5.png (the reel windows).
        private static readonly Vector2 MachineSize = new Vector2(816f, 624f);
        private static readonly float[] ReelCenterX = { 282.5f, 412.5f, 542.5f };
        private const float ReelCenterY = 350.5f;
        private static readonly Vector2 ReelWindowSize = new Vector2(108f, 210f);
        private const float CellHeight = 84f;
        private const float SymbolSize = 80f;

        // Where the cabinet sits on the 1920x1080 canvas, and how much it is scaled up.
        private static readonly Vector2 MachinePosition = new Vector2(-40f, 20f);
        private const float MachineScale = 1.28f;

        // Gold trim of the JACKPOT arch, sampled from slot-machine4.png (machine pixels).
        // Bulbs are spread evenly along this curve, then along the bottom edge of the marquee.
        private static readonly Vector2[] MarqueeArch =
        {
            new Vector2(152f, 160f), new Vector2(180f, 105f), new Vector2(220f, 68f), new Vector2(260f, 44f),
            new Vector2(300f, 28f), new Vector2(340f, 18f), new Vector2(380f, 13f), new Vector2(420f, 11f),
            new Vector2(460f, 14f), new Vector2(500f, 22f), new Vector2(540f, 34f), new Vector2(580f, 53f),
            new Vector2(620f, 81f), new Vector2(660f, 131f), new Vector2(672f, 160f)
        };
        private const int ArchBulbCount = 24;
        private const int BottomBulbCount = 14;
        private const float MarqueeBottomY = 168f;

        // ---------- Look ----------
        private static readonly Color Gold = new Color(1f, 0.82f, 0.28f);
        private static readonly Color LabelBlue = new Color(0.68f, 0.74f, 1f);
        private static readonly Color TextShadow = new Color(0f, 0f, 0f, 0.85f);

        private static Font font;
        private static bool usingCustomFont;
        private static RuntimeAnimatorController pulseController;

        [MenuItem("Tools/Slot Game/Rebuild Scene")]
        public static void Build()
        {
            try
            {
                // Use the project's display font if present, otherwise Unity's built-in font.
                font = AssetDatabase.LoadAssetAtPath<Font>(CustomFontPath);
                usingCustomFont = font != null;
                if (!usingCustomFont)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                EnsureFolders();
                ConfigureTextures();

                // Create the scene BEFORE creating assets we hold references to: opening a new
                // scene unloads unused assets, which would turn those C# references into
                // destroyed objects that serialize as "None".
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                SlotConfig config = CreateOrUpdateData();
                AnimationSet animations = CreateAnimations();
                BuildScene(scene, config, animations);

                AssetDatabase.SaveAssets();
                Debug.Log("[SceneBuilder] Slot game scene built successfully: " + ScenePath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        // =====================================================================
        // Assets
        // =====================================================================

        private static void EnsureFolders()
        {
            foreach (string folder in new[] { AnimationsFolder, PrefabsFolder, DataFolder, SymbolDataFolder, "Assets/Scenes" })
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                    AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
                }
            }
        }

        /// <summary>
        /// Most art should be imported as one full-size sprite. Unity auto-sliced several of these
        /// files (e.g. the BAR symbol into three pieces), which breaks layering, so force Single.
        /// The button sheets stay Multiple: each slice is one button state.
        /// </summary>
        private static void ConfigureTextures()
        {
            var singleSprites = new List<string>
            {
                MachineArtFolder + "/bg_gradient.png",
                MachineArtFolder + "/slot-machine1.png",
                MachineArtFolder + "/slot-machine2.png",
                MachineArtFolder + "/slot-machine3.png",
                MachineArtFolder + "/slot-machine4.png",
                MachineArtFolder + "/slot-machine5.png",
                SymbolArtFolder + "/slot-symbol1.png",
                SymbolArtFolder + "/slot-symbol2.png",
                SymbolArtFolder + "/slot-symbol3.png",
                SymbolArtFolder + "/slot-symbol4.png",
                UISpriteFolder + "/popup.png",
                UISpriteFolder + "/slot_machine_Middle_box.png",
                FxFolder + "/fx_glow.png",
                FxFolder + "/fx_bulb.png",
                FxFolder + "/fx_coin.png",
                FxFolder + "/fx_rays.png",
                FxFolder + "/fx_star.png",
                FxFolder + "/ui_round_button.png",
            };

            foreach (string path in singleSprites)
            {
                var importer = GetImporter(path);
                bool changed = importer.textureType != TextureImporterType.Sprite
                               || importer.spriteImportMode != SpriteImportMode.Single
                               || importer.mipmapEnabled;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;

                // 9-slice borders (left, bottom, right, top) so panels and buttons stretch cleanly.
                Vector4 border = Vector4.zero;
                if (path.EndsWith("popup.png")) border = new Vector4(152f, 134f, 153f, 142f);
                if (path.EndsWith("slot_machine_Middle_box.png")) border = new Vector4(86f, 73f, 86f, 78f);
                if (importer.spriteBorder != border)
                {
                    importer.spriteBorder = border;
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static TextureImporter GetImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException("Texture not found or not a texture: " + path);
            }

            return importer;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
            {
                throw new FileNotFoundException("No sprite at " + path);
            }

            return sprite;
        }

        /// <summary>Loads the slices of a sprite sheet ordered by their numeric suffix (_0, _1, ...).</summary>
        private static Sprite[] LoadSlices(string path, int expectedCount)
        {
            Sprite[] slices = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(sprite => SliceIndex(sprite.name))
                .ToArray();

            if (slices.Length < expectedCount)
            {
                throw new InvalidOperationException(
                    $"{path} has {slices.Length} sprite slices, expected {expectedCount}. " +
                    "Open it in the Sprite Editor and slice it (Grid by cell count).");
            }

            return slices;
        }

        private static int SliceIndex(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            return underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out int index) ? index : 0;
        }

        /// <summary>
        /// Creates the symbol and config assets the first time. On later runs the existing assets
        /// are kept as they are, so Inspector tuning is never overwritten.
        /// </summary>
        private static SlotConfig CreateOrUpdateData()
        {
            // weight = how common; payout = multiplier of the bet for three in a row.
            // Weights 1/2/3/4 (total 10) give a line RTP of 89%, plus the progressive jackpot.
            SymbolData seven = LoadOrCreateSymbol("Seven", "Sevens", SymbolArtFolder + "/slot-symbol1.png", 1, 100, true);
            SymbolData cherry = LoadOrCreateSymbol("Cherry", "Cherries", SymbolArtFolder + "/slot-symbol2.png", 4, 5, false);
            SymbolData bell = LoadOrCreateSymbol("Bell", "Bells", SymbolArtFolder + "/slot-symbol3.png", 3, 10, false);
            SymbolData bar = LoadOrCreateSymbol("Bar", "BARs", SymbolArtFolder + "/slot-symbol4.png", 2, 25, false);

            var config = AssetDatabase.LoadAssetAtPath<SlotConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SlotConfig>();
                config.SetSymbols(new[] { seven, cherry, bell, bar });
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            return config;
        }

        private static SymbolData LoadOrCreateSymbol(string id, string displayName, string spritePath, int weight, int payout, bool jackpot)
        {
            string assetPath = $"{SymbolDataFolder}/{id}.asset";
            var symbol = AssetDatabase.LoadAssetAtPath<SymbolData>(assetPath);
            Sprite sprite = LoadSprite(spritePath);

            if (symbol == null)
            {
                symbol = SymbolData.Create(id, displayName, weight, payout, jackpot, sprite);
                AssetDatabase.CreateAsset(symbol, assetPath);
            }
            else if (symbol.Sprite == null)
            {
                // Re-link the sprite if the texture was re-imported, keeping tuned numbers.
                symbol.Configure(symbol.SymbolId, symbol.DisplayName, symbol.Weight,
                    symbol.PayoutMultiplier, symbol.AwardsJackpot, sprite);
                EditorUtility.SetDirty(symbol);
            }

            return symbol;
        }

        // =====================================================================
        // Animations
        // =====================================================================

        private sealed class AnimationSet
        {
            public RuntimeAnimatorController PopIn;
            public RuntimeAnimatorController Lever;
            public RuntimeAnimatorController JackpotPulse;
        }

        private static AnimationSet CreateAnimations()
        {
            // Popup pop-in: overshoot scale. Plays automatically whenever a popup is shown,
            // because an Animator resets to its default state when re-enabled.
            var popIn = new AnimationClip { name = "PopIn" };
            SetScaleCurve(popIn, "", new Keyframe(0f, 0.4f), new Keyframe(0.16f, 1.08f), new Keyframe(0.28f, 1f));
            SaveClip(popIn, "PopIn");

            // Lever pull: swap the "up" sprite for the "down" sprite, then back.
            var leverPull = new AnimationClip { name = "LeverPull" };
            SetSteppedCurve(leverPull, "LeverUp", typeof(Image), "m_Enabled", (0f, 1f), (0.04f, 0f), (0.45f, 1f));
            SetSteppedCurve(leverPull, "LeverDown", typeof(Image), "m_Enabled", (0f, 0f), (0.04f, 1f), (0.45f, 0f));
            SaveClip(leverPull, "LeverPull");

            // Jackpot meter: slow looping pulse so the progressive jackpot attracts the eye.
            var pulse = new AnimationClip { name = "JackpotPulse" };
            SetScaleCurve(pulse, "", new Keyframe(0f, 1f), new Keyframe(0.6f, 1.07f), new Keyframe(1.2f, 1f));
            AnimationClipSettings pulseSettings = AnimationUtility.GetAnimationClipSettings(pulse);
            pulseSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(pulse, pulseSettings);
            SaveClip(pulse, "JackpotPulse");

            return new AnimationSet
            {
                PopIn = CreateSingleStateController("PopIn", popIn),
                JackpotPulse = CreateSingleStateController("JackpotPulse", pulse),
                Lever = CreateLeverController(leverPull)
            };
        }

        private static void SetScaleCurve(AnimationClip clip, string path, params Keyframe[] keys)
        {
            foreach (string axis in new[] { "x", "y" })
            {
                var curve = new AnimationCurve(keys);
                clip.SetCurve(path, typeof(Transform), "m_LocalScale." + axis, curve);
            }

            clip.SetCurve(path, typeof(Transform), "m_LocalScale.z", AnimationCurve.Constant(0f, keys[keys.Length - 1].time, 1f));
        }

        private static void SetSteppedCurve(AnimationClip clip, string path, Type type, string property, params (float time, float value)[] keys)
        {
            var curve = new AnimationCurve(keys.Select(key => new Keyframe(key.time, key.value)).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
        }

        private static void SaveClip(AnimationClip clip, string fileName)
        {
            string path = $"{AnimationsFolder}/{fileName}.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
        }

        private static RuntimeAnimatorController CreateSingleStateController(string fileName, AnimationClip clip)
        {
            string path = $"{AnimationsFolder}/{fileName}.controller";
            AssetDatabase.DeleteAsset(path);
            return AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
        }

        private static RuntimeAnimatorController CreateLeverController(AnimationClip pullClip)
        {
            string path = $"{AnimationsFolder}/Lever.controller";
            AssetDatabase.DeleteAsset(path);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Pull", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idle = stateMachine.AddState("Idle");
            AnimatorState pull = stateMachine.AddState("Pull");
            pull.motion = pullClip;
            stateMachine.defaultState = idle;

            AnimatorStateTransition toPull = idle.AddTransition(pull);
            toPull.AddCondition(AnimatorConditionMode.If, 0f, "Pull");
            toPull.hasExitTime = false;
            toPull.duration = 0f;

            AnimatorStateTransition backToIdle = pull.AddTransition(idle);
            backToIdle.hasExitTime = true;
            backToIdle.exitTime = 1f;
            backToIdle.duration = 0f;

            return controller;
        }

        // =====================================================================
        // Scene
        // =====================================================================

        private static void BuildScene(Scene scene, SlotConfig config, AnimationSet animations)
        {
            pulseController = animations.JackpotPulse;

            CreateCamera();
            CreateEventSystem();
            Canvas canvas = CreateCanvas();
            RectTransform root = (RectTransform)canvas.transform;

            // Sprites
            Sprite background = LoadSprite(MachineArtFolder + "/bg_gradient.png");
            Sprite popupPanel = LoadSprite(UISpriteFolder + "/popup.png");
            Sprite middleBox = LoadSprite(UISpriteFolder + "/slot_machine_Middle_box.png");
            Sprite[] closeButton = LoadSlices(UISpriteFolder + "/slot_machine_buttons-02.png", 4);
            Sprite[] arrowRight = LoadSlices(UISpriteFolder + "/slot_machine_buttons-03.png", 4);
            Sprite[] arrowLeft = LoadSlices(UISpriteFolder + "/slot_machine_buttons-04.png", 4);
            Sprite[] yesNo = LoadSlices(UISpriteFolder + "/Yes_No_Btn.png", 6);

            // Background
            Image backgroundImage = CreateImage("Background", root, background);
            Stretch(backgroundImage.rectTransform);
            backgroundImage.raycastTarget = false;

            // Soft coloured glows tie the cabinet and the HUD together and add depth.
            Sprite glow = LoadSprite(FxFolder + "/fx_glow.png");
            CreateGlow("MachineGlow", root, glow, MachinePosition, new Vector2(1500f, 1250f), new Color(0.85f, 0.35f, 1f, 0.45f));
            CreateGlow("LeftGlow", root, glow, new Vector2(-690f, 50f), new Vector2(800f, 900f), new Color(0.35f, 0.6f, 1f, 0.22f));
            CreateGlow("RightGlow", root, glow, new Vector2(690f, 50f), new Vector2(800f, 900f), new Color(0.35f, 0.6f, 1f, 0.22f));

            // Slowly rotating sunburst behind the cabinet, plus twinkling stars, for a showroom feel.
            Sprite rays = LoadSprite(FxFolder + "/fx_rays.png");
            Image machineRays = CreateImage("MachineRays", root, rays);
            machineRays.rectTransform.anchoredPosition = MachinePosition + new Vector2(0f, 60f);
            machineRays.rectTransform.sizeDelta = new Vector2(1800f, 1800f);
            machineRays.color = new Color(1f, 0.82f, 0.45f, 0.16f);
            machineRays.raycastTarget = false;
            machineRays.gameObject.AddComponent<UIRotator>();

            BuildSparkles(root, LoadSprite(FxFolder + "/fx_star.png"));

            // Machine + reels
            var machineObject = new GameObject("SlotMachine", typeof(SlotMachine));
            SlotMachine machine = machineObject.GetComponent<SlotMachine>();

            MachineView machineView = BuildMachine(root, config, machine, animations, arrowLeft[0], arrowRight[0]);

            SetObject(machine, "config", config);
            SetObjectArray(machine, "reels", machineView.Reels);


            // Right-hand HUD
            Hud hud = BuildHud(root, config, popupPanel, LoadSprite(FxFolder + "/ui_round_button.png"), arrowLeft, arrowRight);

            // Coins, payline flash and shake for wins. Above the HUD, below the popups.
            BuildWinEffects(root, machine, machineView);

            // Popups (built last so they draw on top)
            WinPopup winPopup = BuildWinPopup(root, popupPanel, closeButton, animations.PopIn);
            ConfirmPopup confirmPopup = BuildConfirmPopup(root, popupPanel, yesNo, animations.PopIn);

            // UI controller
            var uiObject = new GameObject("SlotUI", typeof(SlotUIController));
            SlotUIController ui = uiObject.GetComponent<SlotUIController>();
            SetObject(ui, "machine", machine);
            SetObject(ui, "creditsMeter", hud.Credits);
            SetObject(ui, "betMeter", hud.Bet);
            SetObject(ui, "winMeter", hud.Win);
            SetObject(ui, "jackpotMeter", machineView.JackpotMeter);
            SetObject(ui, "messageText", hud.Message);
            SetObject(ui, "spinButton", hud.SpinButton);
            SetObject(ui, "spinButtonLabel", hud.SpinLabel);
            SetObject(ui, "betUpButton", hud.BetUp);
            SetObject(ui, "betDownButton", hud.BetDown);
            SetObject(ui, "autoSpinButton", hud.AutoButton);
            SetObject(ui, "autoSpinLabel", hud.AutoLabel);
            SetObject(ui, "winPopup", winPopup);
            SetObject(ui, "confirmPopup", confirmPopup);

            // Lever
            LeverView lever = machineView.Lever;
            SetObject(lever, "machine", machine);

            // Audio + music toggle (top-left corner)
            AudioManager audioManager = BuildAudio(machine);
            Button musicButton = CreateBoxButton("MusicButton", root, middleBox, new Vector2(810f, 492f), new Vector2(220f, 56f), 2f,
                "MUSIC: ON", 26, out Text musicLabel);
            MusicToggle musicToggle = musicButton.gameObject.AddComponent<MusicToggle>();
            SetObject(musicToggle, "audioManager", audioManager);
            SetObject(musicToggle, "label", musicLabel);
            // Keep the button under the popups so their dimmed background covers it.
            musicButton.transform.SetSiblingIndex(winPopup.transform.GetSiblingIndex());

            // Popups start hidden.
            winPopup.gameObject.SetActive(false);
            confirmPopup.gameObject.SetActive(false);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.04f, 0.12f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateEventSystem()
        {
            // The project uses the new Input System only, so the UI module must be the Input System one.
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        // ---------- Machine ----------

        private sealed class MachineView
        {
            public ReelController[] Reels;
            public CountUpText JackpotMeter;
            public LeverView Lever;
            public Image Payline;
            public RectTransform Root;
        }

        private static MachineView BuildMachine(RectTransform parent, SlotConfig config, SlotMachine machine,
            AnimationSet animations, Sprite markerLeft, Sprite markerRight)
        {
            // All machine layers are 816x624 images that line up pixel for pixel, so each layer
            // simply stretches over the same parent rect.
            RectTransform machineRoot = CreateRect("Machine", parent, MachinePosition, MachineSize);
            machineRoot.localScale = new Vector3(MachineScale, MachineScale, 1f);

            Image reelBackground = CreateImage("ReelBackground", machineRoot, LoadSprite(MachineArtFolder + "/slot-machine5.png"));
            Stretch(reelBackground.rectTransform);
            reelBackground.raycastTarget = false;

            // Reels
            RectTransform reelsRoot = CreateRect("Reels", machineRoot, Vector2.zero, MachineSize);
            GameObject reelPrefab = CreateReelPrefab(config);
            var reels = new ReelController[ReelCenterX.Length];

            for (int i = 0; i < reels.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(reelPrefab, reelsRoot);
                instance.name = "Reel" + (i + 1);
                var rect = (RectTransform)instance.transform;
                rect.anchoredPosition = MachineToLocal(ReelCenterX[i], ReelCenterY);
                reels[i] = instance.GetComponent<ReelController>();
            }

            // Payline: thin line across the middle row so it's clear what counts.
            float paylineLeft = ReelCenterX[0] - ReelWindowSize.x * 0.5f;
            float paylineRight = ReelCenterX[ReelCenterX.Length - 1] + ReelWindowSize.x * 0.5f;
            Image payline = CreateImage("Payline", machineRoot, null);
            payline.color = new Color(1f, 0.25f, 0.2f, 0.55f);
            payline.raycastTarget = false;
            payline.rectTransform.anchoredPosition = MachineToLocal((paylineLeft + paylineRight) * 0.5f, ReelCenterY);
            payline.rectTransform.sizeDelta = new Vector2(paylineRight - paylineLeft, 3f);

            // Frame sits on top of the reels, hiding everything outside the windows.
            Image frame = CreateImage("Frame", machineRoot, LoadSprite(MachineArtFolder + "/slot-machine4.png"));
            Stretch(frame.rectTransform);
            frame.raycastTarget = false;

            // Payline arrows on the frame.
            Image leftMarker = CreateImage("PaylineMarkerLeft", machineRoot, markerRight);
            leftMarker.rectTransform.anchoredPosition = MachineToLocal(paylineLeft - 14f, ReelCenterY);
            leftMarker.rectTransform.sizeDelta = new Vector2(22f, 22f);
            leftMarker.raycastTarget = false;

            Image rightMarker = CreateImage("PaylineMarkerRight", machineRoot, markerLeft);
            rightMarker.rectTransform.anchoredPosition = MachineToLocal(paylineRight + 14f, ReelCenterY);
            rightMarker.rectTransform.sizeDelta = new Vector2(22f, 22f);
            rightMarker.raycastTarget = false;

            // Chase lights around the JACKPOT marquee.
            BuildMarqueeLights(machineRoot, machine, LoadSprite(FxFolder + "/fx_bulb.png"));

            // Lever: two sprite states swapped by the Lever animator, plus an invisible click area.
            RectTransform leverRoot = CreateRect("Lever", machineRoot, Vector2.zero, MachineSize);
            Stretch(leverRoot);
            Animator leverAnimator = leverRoot.gameObject.AddComponent<Animator>();
            leverAnimator.runtimeAnimatorController = animations.Lever;

            Image leverUp = CreateImage("LeverUp", leverRoot, LoadSprite(MachineArtFolder + "/slot-machine2.png"));
            Stretch(leverUp.rectTransform);
            leverUp.raycastTarget = false;

            Image leverDown = CreateImage("LeverDown", leverRoot, LoadSprite(MachineArtFolder + "/slot-machine3.png"));
            Stretch(leverDown.rectTransform);
            leverDown.raycastTarget = false;
            leverDown.enabled = false;

            Image leverHit = CreateImage("LeverHitArea", leverRoot, null);
            leverHit.color = new Color(1f, 1f, 1f, 0f);
            leverHit.rectTransform.anchoredPosition = MachineToLocal(718.5f, 459.5f);
            leverHit.rectTransform.sizeDelta = new Vector2(100f, 330f);
            Button leverButton = leverHit.gameObject.AddComponent<Button>();
            leverButton.transition = Selectable.Transition.None;

            LeverView lever = leverRoot.gameObject.AddComponent<LeverView>();
            SetObject(lever, "animator", leverAnimator);
            SetObject(lever, "hitArea", leverButton);

            // Progressive jackpot meter in the empty panel at the bottom of the cabinet.
            Text jackpotText = CreateText("JackpotMeter", machineRoot, "JACKPOT 0", 30, Gold,
                MachineToLocal(414.5f, 599.5f), new Vector2(400f, 48f), FontStyle.Bold);
            CountUpText jackpotMeter = jackpotText.gameObject.AddComponent<CountUpText>();
            SetString(jackpotMeter, "prefix", "JACKPOT  ");
            jackpotText.gameObject.AddComponent<Animator>().runtimeAnimatorController = animations.JackpotPulse;

            return new MachineView
            {
                Reels = reels,
                JackpotMeter = jackpotMeter,
                Lever = lever,
                Payline = payline,
                Root = machineRoot
            };
        }

        private static void BuildMarqueeLights(RectTransform machineRoot, SlotMachine machine, Sprite bulbSprite)
        {
            RectTransform lightsRoot = CreateRect("MarqueeLights", machineRoot, Vector2.zero, MachineSize);
            Stretch(lightsRoot);

            // Arch left to right, then the bottom edge right to left, so the chase runs in one loop.
            var positions = new List<Vector2>(SampleAlongPolyline(MarqueeArch, ArchBulbCount));
            float bottomLeft = MarqueeArch[0].x + 16f;
            float bottomRight = MarqueeArch[MarqueeArch.Length - 1].x - 16f;
            for (int i = 0; i < BottomBulbCount; i++)
            {
                float x = Mathf.Lerp(bottomRight, bottomLeft, (i + 0.5f) / BottomBulbCount);
                positions.Add(new Vector2(x, MarqueeBottomY));
            }

            var bulbs = new Image[positions.Count];
            for (int i = 0; i < bulbs.Length; i++)
            {
                Image bulb = CreateImage("Bulb" + i, lightsRoot, bulbSprite);
                bulb.rectTransform.anchoredPosition = MachineToLocal(positions[i].x, positions[i].y);
                bulb.rectTransform.sizeDelta = new Vector2(20f, 20f);
                bulb.raycastTarget = false;
                bulbs[i] = bulb;
            }

            MarqueeLights lights = lightsRoot.gameObject.AddComponent<MarqueeLights>();
            SetObject(lights, "machine", machine);
            SetObjectArray(lights, "bulbs", bulbs);
        }

        /// <summary>Returns <paramref name="count"/> points evenly spaced by distance along a polyline.</summary>
        private static List<Vector2> SampleAlongPolyline(IReadOnlyList<Vector2> points, int count)
        {
            var cumulative = new float[points.Count];
            for (int i = 1; i < points.Count; i++)
            {
                cumulative[i] = cumulative[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            }

            float total = cumulative[points.Count - 1];
            var result = new List<Vector2>(count);
            int segment = 1;

            for (int i = 0; i < count; i++)
            {
                float target = total * i / (count - 1);
                while (segment < points.Count - 1 && cumulative[segment] < target)
                {
                    segment++;
                }

                float segmentLength = cumulative[segment] - cumulative[segment - 1];
                float t = segmentLength > 0f ? (target - cumulative[segment - 1]) / segmentLength : 0f;
                result.Add(Vector2.Lerp(points[segment - 1], points[segment], t));
            }

            return result;
        }

        private static void BuildWinEffects(RectTransform canvasRoot, SlotMachine machine, MachineView machineView)
        {
            RectTransform layer = CreateRect("WinEffects", canvasRoot, Vector2.zero, Vector2.zero);
            Stretch(layer);

            Image coinTemplate = CreateImage("CoinTemplate", layer, LoadSprite(FxFolder + "/fx_coin.png"));
            coinTemplate.rectTransform.sizeDelta = new Vector2(56f, 56f);
            coinTemplate.raycastTarget = false;

            // Coins burst from the centre of the reel windows, converted to canvas space.
            Vector2 reelsCentre = MachinePosition + MachineToLocal(412.5f, ReelCenterY) * MachineScale;

            WinEffects effects = layer.gameObject.AddComponent<WinEffects>();
            SetObject(effects, "machine", machine);
            SetObject(effects, "payline", machineView.Payline);
            SetObject(effects, "coinLayer", layer);
            SetObject(effects, "coinTemplate", coinTemplate);
            SetVector2(effects, "burstOrigin", reelsCentre);
            SetObject(effects, "shakeTarget", machineView.Root);
        }

        private static void BuildSparkles(RectTransform canvasRoot, Sprite starSprite)
        {
            RectTransform field = CreateRect("Sparkles", canvasRoot, Vector2.zero, Vector2.zero);
            Stretch(field);

            // Fixed seed so the star layout is the same on every rebuild.
            var random = new System.Random(7);
            var stars = new Image[45];
            for (int i = 0; i < stars.Length; i++)
            {
                Image star = CreateImage("Star" + i, field, starSprite);
                star.rectTransform.anchoredPosition = new Vector2(random.Next(-940, 940), random.Next(-520, 520));
                float size = random.Next(18, 44);
                star.rectTransform.sizeDelta = new Vector2(size, size);
                star.color = new Color(1f, 0.95f, 0.8f, 0f);
                star.raycastTarget = false;
                stars[i] = star;
            }

            SparkleField sparkleField = field.gameObject.AddComponent<SparkleField>();
            SetObjectArray(sparkleField, "sparkles", stars);
        }

        private static void CreateGlow(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color)
        {
            Image glow = CreateImage(name, parent, sprite);
            glow.rectTransform.anchoredPosition = position;
            glow.rectTransform.sizeDelta = size;
            glow.color = color;
            glow.raycastTarget = false;
        }

        /// <summary>Converts a pixel in the 816x624 machine art (origin top-left) to local UI space (origin centre, y up).</summary>
        private static Vector2 MachineToLocal(float x, float y)
        {
            return new Vector2(x - MachineSize.x * 0.5f, MachineSize.y * 0.5f - y);
        }

        private static GameObject CreateReelPrefab(SlotConfig config)
        {
            var reelObject = new GameObject("Reel", typeof(RectTransform), typeof(RectMask2D), typeof(ReelController));
            var reelRect = (RectTransform)reelObject.transform;
            reelRect.sizeDelta = ReelWindowSize;

            // Five cells cover the 2.5 visible rows plus one buffer row above and below.
            var cells = new Image[5];
            for (int i = 0; i < cells.Length; i++)
            {
                Image cell = CreateImage("Cell" + i, reelRect, config.Symbols[i % config.Symbols.Count].Sprite);
                cell.rectTransform.sizeDelta = new Vector2(SymbolSize, SymbolSize);
                cell.rectTransform.anchoredPosition = new Vector2(0f, (i - 2) * CellHeight);
                cell.preserveAspect = true;
                cell.raycastTarget = false;
                cells[i] = cell;
            }

            ReelController controller = reelObject.GetComponent<ReelController>();
            SetObjectArray(controller, "cells", cells);
            SetFloat(controller, "cellHeight", CellHeight);

            return SavePrefab(reelObject, "Reel");
        }

        // ---------- HUD ----------

        private sealed class Hud
        {
            public CountUpText Credits;
            public CountUpText Bet;
            public CountUpText Win;
            public Button SpinButton;
            public Text SpinLabel;
            public Button BetUp;
            public Button BetDown;
            public Button AutoButton;
            public Text AutoLabel;
            public Text Message;
        }

        /// <summary>
        /// HUD laid out like a modern video slot: title on top, paytable on the left,
        /// win meter and messages on the right, and a bottom bar with balance, bet,
        /// auto and a big round SPIN button.
        /// </summary>
        private static Hud BuildHud(RectTransform parent, SlotConfig config, Sprite panelSprite, Sprite roundButton,
            Sprite[] arrowLeft, Sprite[] arrowRight)
        {
            var hud = new Hud();

            // ---- Title ----
            Text title = CreateText("Title", parent, "LUCKY SLOTS", 84, Gold, new Vector2(0f, 488f), new Vector2(900f, 100f), FontStyle.Bold);
            Shadow titleDepth = title.gameObject.AddComponent<Shadow>();
            titleDepth.effectColor = new Color(0.35f, 0.08f, 0.45f, 1f);
            titleDepth.effectDistance = new Vector2(0f, -7f);
            title.gameObject.AddComponent<Animator>().runtimeAnimatorController = pulseController;

            // ---- Left: paytable ----
            RectTransform paytable = CreatePanel("PaytablePanel", parent, panelSprite, new Vector2(-690f, 50f), new Vector2(420f, 440f));
            CreateText("PaytableHeader", paytable, "PAYTABLE", 34, Gold, new Vector2(0f, 172f), new Vector2(400f, 44f), FontStyle.Bold);
            CreateText("PaytableRule", paytable, "3 in a row on the line", 20, LabelBlue, new Vector2(0f, 134f), new Vector2(400f, 28f));

            RectTransform grid = CreateRect("Rows", paytable, new Vector2(0f, -45f), new Vector2(380f, 300f));
            VerticalLayoutGroup rows = grid.gameObject.AddComponent<VerticalLayoutGroup>();
            rows.spacing = 6f;
            rows.childAlignment = TextAnchor.MiddleCenter;
            rows.childControlWidth = false;
            rows.childControlHeight = false;
            rows.childForceExpandWidth = false;
            rows.childForceExpandHeight = false;

            PaytableView paytableView = paytable.gameObject.AddComponent<PaytableView>();
            SetObject(paytableView, "config", config);
            SetObject(paytableView, "rowPrefab", CreatePaytableRowPrefab(config).GetComponent<PaytableRowView>());
            SetObject(paytableView, "rowContainer", grid);

            // ---- Right: win meter + status message ----
            RectTransform winPanel = CreatePanel("WinPanel", parent, panelSprite, new Vector2(690f, 50f), new Vector2(420f, 440f));
            CreateText("WinLabel", winPanel, "WIN", 34, LabelBlue, new Vector2(0f, 150f), new Vector2(380f, 44f));
            hud.Win = CreateMeter("WinValue", winPanel, 80, Gold, new Vector2(0f, 75f));

            Image divider = CreateImage("Divider", winPanel, null);
            divider.color = new Color(1f, 0.82f, 0.28f, 0.35f);
            divider.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            divider.rectTransform.sizeDelta = new Vector2(340f, 3f);
            divider.raycastTarget = false;

            hud.Message = CreateText("MessageText", winPanel, "", 28, Color.white, new Vector2(0f, -95f), new Vector2(370f, 180f));
            hud.Message.horizontalOverflow = HorizontalWrapMode.Wrap;
            hud.Message.lineSpacing = 1.1f;

            // ---- Bottom bar ----
            Image bar = CreateImage("BottomBar", parent, null);
            bar.color = new Color(0.04f, 0.02f, 0.12f, 0.82f);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(0f, 150f);
            barRect.anchoredPosition = Vector2.zero;

            Image barEdge = CreateImage("TopEdge", barRect, null);
            barEdge.color = new Color(1f, 0.82f, 0.28f, 0.8f);
            barEdge.rectTransform.anchorMin = new Vector2(0f, 1f);
            barEdge.rectTransform.anchorMax = new Vector2(1f, 1f);
            barEdge.rectTransform.sizeDelta = new Vector2(0f, 3f);
            barEdge.rectTransform.anchoredPosition = Vector2.zero;
            barEdge.raycastTarget = false;

            // Balance
            CreateText("BalanceLabel", barRect, "BALANCE", 24, LabelBlue, new Vector2(-700f, 28f), new Vector2(320f, 32f));
            hud.Credits = CreateMeter("BalanceValue", barRect, 50, Color.white, new Vector2(-700f, -18f));

            // Bet with up / down arrows (the arrow art points sideways, so rotate it).
            CreateText("BetLabel", barRect, "TOTAL BET", 24, LabelBlue, new Vector2(-280f, 28f), new Vector2(320f, 32f));
            hud.Bet = CreateMeter("BetValue", barRect, 50, Gold, new Vector2(-280f, -18f));
            hud.BetUp = CreateSpriteButton("BetUpButton", barRect, arrowRight, new Vector2(-110f, 28f), new Vector2(52f, 52f));
            hud.BetUp.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            hud.BetDown = CreateSpriteButton("BetDownButton", barRect, arrowRight, new Vector2(-110f, -30f), new Vector2(52f, 52f));
            hud.BetDown.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

            // Auto + Spin round buttons on the right, like a modern slot.
            hud.AutoButton = CreateRoundButton("AutoSpinButton", barRect, roundButton, new Vector2(560f, 0f), 112f,
                "AUTO\nOFF", 24, out hud.AutoLabel);
            hud.SpinButton = CreateRoundButton("SpinButton", barRect, roundButton, new Vector2(760f, 20f), 190f,
                "SPIN", 52, out hud.SpinLabel);

            return hud;
        }

        private static GameObject CreatePaytableRowPrefab(SlotConfig config)
        {
            var row = new GameObject("PaytableRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(PaytableRowView));
            ((RectTransform)row.transform).sizeDelta = new Vector2(380f, 66f);

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var icons = new Image[3];
            for (int i = 0; i < icons.Length; i++)
            {
                icons[i] = CreateImage("Icon" + i, (RectTransform)row.transform, config.Symbols[0].Sprite);
                icons[i].rectTransform.sizeDelta = new Vector2(54f, 54f);
                icons[i].preserveAspect = true;
                icons[i].raycastTarget = false;
            }

            Text payout = CreateText("Payout", (RectTransform)row.transform, "x0", 26, Gold, Vector2.zero, new Vector2(205f, 60f), FontStyle.Bold);
            payout.alignment = TextAnchor.MiddleLeft;

            PaytableRowView view = row.GetComponent<PaytableRowView>();
            SetObjectArray(view, "icons", icons);
            SetObject(view, "payoutText", payout);

            return SavePrefab(row, "PaytableRow");
        }

        private static CountUpText CreateMeter(string name, RectTransform parent, int size, Color color, Vector2 position)
        {
            Text text = CreateText(name, parent, "0", size, color, position, new Vector2(320f, 64f), FontStyle.Bold);
            return text.gameObject.AddComponent<CountUpText>();
        }

        // ---------- Popups ----------

        private static WinPopup BuildWinPopup(RectTransform parent, Sprite panelSprite, Sprite[] closeSprites, RuntimeAnimatorController popIn)
        {
            RectTransform root = CreateRect("WinPopup", parent, Vector2.zero, Vector2.zero);
            Stretch(root);

            Button dim = CreateDim(root);

            // Spinning golden rays behind the win panel.
            Image popupRays = CreateImage("Rays", root, LoadSprite(FxFolder + "/fx_rays.png"));
            popupRays.rectTransform.sizeDelta = new Vector2(1500f, 1500f);
            popupRays.color = new Color(1f, 0.85f, 0.35f, 0.55f);
            popupRays.raycastTarget = false;
            SetFloat(popupRays.gameObject.AddComponent<UIRotator>(), "degreesPerSecond", 30f);

            RectTransform panel = CreatePanel("Panel", root, panelSprite, Vector2.zero, new Vector2(1000f, 500f));
            panel.gameObject.AddComponent<Animator>().runtimeAnimatorController = popIn;
            // Let clicks pass through the panel to the dim background, so clicking anywhere closes it.
            panel.GetComponent<Image>().raycastTarget = false;

            Text title = CreateText("Title", panel, "BIG WIN!", 96, Gold, new Vector2(0f, 110f), new Vector2(900f, 120f), FontStyle.Bold);
            Text amountText = CreateText("Amount", panel, "0", 110, Color.white, new Vector2(0f, -40f), new Vector2(900f, 140f), FontStyle.Bold);
            CountUpText amount = amountText.gameObject.AddComponent<CountUpText>();
            CreateText("Hint", panel, "Click anywhere to continue", 26, LabelBlue, new Vector2(0f, -170f), new Vector2(900f, 40f));

            Button close = CreateSpriteButton("CloseButton", panel, closeSprites, new Vector2(440f, 190f), new Vector2(84f, 84f));

            WinPopup popup = root.gameObject.AddComponent<WinPopup>();
            SetObject(popup, "titleText", title);
            SetObject(popup, "amountText", amount);
            SetObject(popup, "closeButton", close);
            SetObject(popup, "backgroundButton", dim);

            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, $"{PrefabsFolder}/WinPopup.prefab", InteractionMode.AutomatedAction);
            return popup;
        }

        private static ConfirmPopup BuildConfirmPopup(RectTransform parent, Sprite panelSprite, Sprite[] yesNo, RuntimeAnimatorController popIn)
        {
            RectTransform root = CreateRect("ConfirmPopup", parent, Vector2.zero, Vector2.zero);
            Stretch(root);
            CreateDim(root);

            RectTransform panel = CreatePanel("Panel", root, panelSprite, Vector2.zero, new Vector2(900f, 440f));
            panel.gameObject.AddComponent<Animator>().runtimeAnimatorController = popIn;

            Text message = CreateText("Message", panel, "Are you sure?", 44, Color.white, new Vector2(0f, 60f), new Vector2(820f, 200f));

            // Yes_No_Btn slices: 0 = YES, 1 = NO, then hover (2,3) and pressed (4,5) versions.
            Button yes = CreateSpriteButton("YesButton", panel, new[] { yesNo[0], yesNo[2], yesNo[4], yesNo[0] },
                new Vector2(-170f, -120f), new Vector2(280f, 108f));
            Button no = CreateSpriteButton("NoButton", panel, new[] { yesNo[1], yesNo[3], yesNo[5], yesNo[1] },
                new Vector2(170f, -120f), new Vector2(280f, 108f));

            ConfirmPopup popup = root.gameObject.AddComponent<ConfirmPopup>();
            SetObject(popup, "messageText", message);
            SetObject(popup, "yesButton", yes);
            SetObject(popup, "noButton", no);

            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, $"{PrefabsFolder}/ConfirmPopup.prefab", InteractionMode.AutomatedAction);
            return popup;
        }

        private static Button CreateDim(RectTransform parent)
        {
            Image dim = CreateImage("Dim", parent, null);
            dim.color = new Color(0f, 0f, 0f, 0.65f);
            Stretch(dim.rectTransform);
            Button button = dim.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            return button;
        }

        // ---------- Audio ----------

        private static AudioManager BuildAudio(SlotMachine machine)
        {
            var audioObject = new GameObject("Audio", typeof(AudioManager));
            AudioSource effects = audioObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;

            AudioSource loop = audioObject.AddComponent<AudioSource>();
            loop.playOnAwake = false;
            loop.loop = true;
            loop.volume = 0.5f;

            AudioSource music = audioObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;

            AudioManager manager = audioObject.GetComponent<AudioManager>();
            SetObject(manager, "machine", machine);
            SetObject(manager, "effectsSource", effects);
            SetObject(manager, "loopSource", loop);
            SetObject(manager, "buttonClick", LoadClip("ui_click"));
            SetObject(manager, "leverPull", LoadClip("lever_pull"));
            SetObject(manager, "reelSpinLoop", LoadClip("reel_spin_loop"));
            SetObject(manager, "reelStop", LoadClip("reel_stop"));
            SetObject(manager, "smallWin", LoadClip("win_small"));
            SetObject(manager, "bigWin", LoadClip("win_big"));
            SetObject(manager, "musicSource", music);
            SetObject(manager, "backgroundMusic", LoadClip("bgm_casino_loop"));
            return manager;
        }

        private static AudioClip LoadClip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundsFolder}/{name}.wav");
            if (clip == null)
            {
                Debug.LogWarning($"[SceneBuilder] Missing sound {SoundsFolder}/{name}.wav. The game will run silently for that event.");
            }

            return clip;
        }

        // =====================================================================
        // UI helpers
        // =====================================================================

        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, sprite != null ? sprite.rect.size : new Vector2(100f, 100f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            return image;
        }

        /// <summary>
        /// Panel using the 9-sliced popup sprite. The art has transparent margins
        /// (about 82px each side at full size), so the rect is enlarged to keep the visible
        /// panel at <paramref name="visibleSize"/>.
        /// </summary>
        private static RectTransform CreatePanel(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 visibleSize)
        {
            const float multiplier = 2f;
            Vector2 margin = new Vector2(83f, 68f) / multiplier;

            Image image = CreateImage(name, parent, sprite);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = visibleSize + margin * 2f;
            return image.rectTransform;
        }

        private static Button CreateBoxButton(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 visibleSize,
            float multiplier, string label, int fontSize, out Text labelText)
        {
            Vector2 margin = new Vector2(46f, 36f) / multiplier;

            Image image = CreateImage(name, parent, sprite);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = visibleSize + margin * 2f;

            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.92f, 0.75f);
            colors.pressedColor = new Color(0.75f, 0.65f, 0.55f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            button.colors = colors;
            image.gameObject.AddComponent<UIButtonSound>();

            labelText = CreateText("Label", image.rectTransform, label, fontSize, Color.white, Vector2.zero, visibleSize, FontStyle.Bold);
            return button;
        }

        private static Button CreateRoundButton(string name, Transform parent, Sprite sprite, Vector2 position, float diameter,
            string label, int fontSize, out Text labelText)
        {
            Image image = CreateImage(name, parent, sprite);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            image.preserveAspect = true;

            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.9f, 0.8f);
            colors.pressedColor = new Color(0.7f, 0.6f, 0.6f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.85f);
            button.colors = colors;
            image.gameObject.AddComponent<UIButtonSound>();

            labelText = CreateText("Label", image.rectTransform, label, fontSize, Color.white, Vector2.zero,
                new Vector2(diameter, diameter), FontStyle.Bold);
            labelText.lineSpacing = 0.9f;
            return button;
        }

        /// <param name="states">normal, highlighted, pressed, disabled.</param>
        private static Button CreateSpriteButton(string name, Transform parent, Sprite[] states, Vector2 position, Vector2 size)
        {
            Image image = CreateImage(name, parent, states[0]);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            image.preserveAspect = true;

            Button button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = states[1],
                selectedSprite = states[0],
                pressedSprite = states[2],
                disabledSprite = states[3]
            };
            image.gameObject.AddComponent<UIButtonSound>();
            return button;
        }

        private static Text CreateText(string name, Transform parent, string content, int size, Color color,
            Vector2 position, Vector2 rectSize, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = CreateRect(name, parent, position, rectSize);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            // Synthetic bold looks smeared on a display font, so only use it with the built-in font.
            text.fontStyle = usingCustomFont ? FontStyle.Normal : style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = TextShadow;
            outline.effectDistance = usingCustomFont ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
            return text;
        }

        private static GameObject SavePrefab(GameObject source, string fileName)
        {
            string path = $"{PrefabsFolder}/{fileName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            Object.DestroyImmediate(source);
            return prefab;
        }

        // =====================================================================
        // Serialized field helpers
        // (Components keep fields private with [SerializeField], so the builder sets them
        //  through SerializedObject, the same way the Inspector does.)
        // =====================================================================

        private static SerializedProperty FindProperty(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
            }

            return property;
        }

        private static void SetObject(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            FindProperty(serializedObject, propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(Object target, string propertyName, IReadOnlyList<Object> values)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = FindProperty(serializedObject, propertyName);
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            var serializedObject = new SerializedObject(target);
            FindProperty(serializedObject, propertyName).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector2(Object target, string propertyName, Vector2 value)
        {
            var serializedObject = new SerializedObject(target);
            FindProperty(serializedObject, propertyName).vector2Value = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string propertyName, string value)
        {
            var serializedObject = new SerializedObject(target);
            FindProperty(serializedObject, propertyName).stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
