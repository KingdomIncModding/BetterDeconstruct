using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Assets.Behaviour.UI;
using HarmonyLib;
using KingdomFoundation.Api;
using UnityEngine;

namespace KingdomBetterDeconstruct
{
    [BepInPlugin(Id, "Better Deconstruct", Version)]
    [BepInDependency("urakka.kingdominc.foundation", "0.11.0")]
    [NexusMod(24)]
    public sealed class BetterDeconstructPlugin : BaseUnityPlugin
    {
        public const string Id = "urakka.kingdominc.betterdeconstruct";
        public const string Version = "1.0.0";

        internal static ManualLogSource LogSource;

        internal static ConfigEntry<bool> DefaultIncludeBuildings;
        internal static ConfigEntry<bool> DefaultIncludeContainers;
        internal static ConfigEntry<bool> DefaultIncludeLabels;
        internal static ConfigEntry<bool> DefaultIncludeStickers;
        internal static ConfigEntry<bool> IgnoreLockedDecorations;
        internal static ConfigEntry<bool> StayInDeconstructMode;

        private Harmony _harmony;
        private FoundationModContext _context;
        private GameObject _boxObject;
        private GameObject _modalObject;

        private void Awake()
        {
            LogSource = Logger;
            _context = FoundationApi.ForMod(Id);

            DefaultIncludeBuildings = Config.Bind(
                "Categories",
                "DefaultIncludeBuildings",
                true,
                "Include buildings by default when opening the box-deconstruct confirmation popup.");

            DefaultIncludeContainers = Config.Bind(
                "Categories",
                "DefaultIncludeContainers",
                true,
                "Include containers by default when opening the box-deconstruct confirmation popup.");

            DefaultIncludeLabels = Config.Bind(
                "Categories",
                "DefaultIncludeLabels",
                true,
                "Include labels by default when opening the box-deconstruct confirmation popup.");

            DefaultIncludeStickers = Config.Bind(
                "Categories",
                "DefaultIncludeStickers",
                true,
                "Include stickers by default when opening the box-deconstruct confirmation popup.");

            IgnoreLockedDecorations = Config.Bind(
                "Behavior",
                "IgnoreLockedDecorations",
                true,
                "Skip locked containers, labels, and stickers when box-selecting in Deconstruct mode.");

            StayInDeconstructMode = Config.Bind(
                "Behavior",
                "StayInDeconstructMode",
                true,
                "Keep Deconstruct mode active after confirming a batch deconstruction.");

            IModConfigSection settings = _context.Settings(
                "Better Deconstruct",
                "Box-select deconstruction with a confirmation popup, building type counts, and category filters.");
            settings.AddToggle(DefaultIncludeBuildings, "Include Buildings by Default");
            settings.AddToggle(DefaultIncludeContainers, "Include Containers by Default");
            settings.AddToggle(DefaultIncludeLabels, "Include Labels by Default");
            settings.AddToggle(DefaultIncludeStickers, "Include Stickers by Default");
            settings.AddToggle(IgnoreLockedDecorations, "Ignore Locked Decorations");
            settings.AddToggle(StayInDeconstructMode, "Stay in Deconstruct Mode After Confirming");

            _harmony = new Harmony(Id);
            _harmony.PatchAll(typeof(BetterDeconstructPlugin).Assembly);

            Logger.LogInfo("Better Deconstruct initialized.");
        }

        private void Update()
        {
            EnsureUiInitialized();
        }

        internal static void EnsureUiInitialized()
        {
            if (_instance == null || !GameUI.Instance)
            {
                return;
            }

            _instance.AttachToGameUi(GameUI.Instance);
        }

        private static BetterDeconstructPlugin _instance;

        private void OnEnable()
        {
            _instance = this;
        }

        private void AttachToGameUi(GameUI ui)
        {
            if (_boxObject == null)
            {
                _boxObject = new GameObject("BetterDeconstruct_SelectionBox", typeof(RectTransform));
                var boxController = _boxObject.AddComponent<DeconstructBoxController>();
                boxController.Initialize(ui);
            }

            if (_modalObject == null)
            {
                Canvas parentCanvas = ui.GetComponentInParent<Canvas>();
                Transform canvasTransform = parentCanvas != null ? parentCanvas.transform : ui.transform;

                _modalObject = new GameObject("BetterDeconstruct_ConfirmModal", typeof(RectTransform));
                var modal = _modalObject.AddComponent<DeconstructConfirmModal>();
                modal.Initialize(ui, canvasTransform);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            _harmony?.UnpatchSelf();
            _context?.Dispose();

            if (_boxObject != null)
            {
                Destroy(_boxObject);
                _boxObject = null;
            }

            if (_modalObject != null)
            {
                Destroy(_modalObject);
                _modalObject = null;
            }
        }
    }
}
