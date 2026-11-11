using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Behaviour.Flow;
using Assets.Behaviour.UI;
using Assets.Behaviour.UI.HUD;
using Assets.Behaviour.UI.Shared;
using Assets.Source.Player;
using Assets.Source.Util;
using HarmonyLib;
using KingdomFoundation.Api;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KingdomBetterDeconstruct
{
    internal sealed class DeconstructConfirmModal : UIBlocksZoom
    {
        private const float PanelWidth = 560f;
        private const float PanelHeight = 560f;

        private static readonly AccessTools.FieldRef<DeconstructOverlay, CurrencyDisplay> OverlayRefundField =
            AccessTools.FieldRefAccess<DeconstructOverlay, CurrencyDisplay>("_refund");

        private static readonly AccessTools.FieldRef<CurrencyDisplay, Sprite> CopperIconField =
            AccessTools.FieldRefAccess<CurrencyDisplay, Sprite>("_copperIcon");

        private static readonly AccessTools.FieldRef<CurrencyDisplay, Sprite> SilverIconField =
            AccessTools.FieldRefAccess<CurrencyDisplay, Sprite>("_silverIcon");

        public static DeconstructConfirmModal Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.gameObject.activeSelf;

        private DeconstructUiTheme theme;
        private TMP_FontAsset font;
        private RectTransform panel;
        private TMP_Text subtitleLabel;
        private RectTransform listContent;
        private RectTransform refundContent;
        private Button confirmButton;
        private TMP_Text confirmButtonLabel;

        private Button buildingsToggleBtn;
        private TMP_Text buildingsToggleText;
        private Button containersToggleBtn;
        private TMP_Text containersToggleText;
        private Button labelsToggleBtn;
        private TMP_Text labelsToggleText;
        private Button stickersToggleBtn;
        private TMP_Text stickersToggleText;

        private DeconstructSelection currentSelection;
        private bool includeBuildings = true;
        private bool includeContainers = true;
        private bool includeLabels = true;
        private bool includeStickers = true;

        private Sprite copperIcon;
        private Sprite silverIcon;

        public void Initialize(GameUI ui, Transform parentCanvasTransform)
        {
            Instance = this;
            theme = new DeconstructUiTheme();
            font = ui.GetComponentsInChildren<TMP_Text>(true).Select(t => t.font).FirstOrDefault(f => f != null);
            CacheCurrencySprites(ui);

            transform.SetParent(parentCanvasTransform, false);
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            Image backdrop = gameObject.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.65f);
            Button backdropButton = gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.onClick.AddListener(Cancel);

            Canvas overlayCanvas = gameObject.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 100;
            gameObject.AddComponent<GraphicRaycaster>();

            panel = CreateBox(root, "ConfirmDeconstructPanel", 0f, 0f, PanelWidth, PanelHeight, DeconstructUiTheme.Wood);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            // Swallow clicks inside the dialog panel so they don't trigger backdrop cancel.
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            // Header icon + title
            Sprite headerSprite = FoundationApi.Presentation.FindSprite("Icons_31") ??
                                  FoundationApi.Machines.Catalog.FirstOrDefault()?.Icon;
            if (headerSprite != null)
            {
                CreateIcon(panel, headerSprite, 22f, 18f, 40f);
            }

            CreateLabel(panel, "CONFIRM DECONSTRUCT", 72f, 16f, 410f, 28f, 22f, DeconstructUiTheme.Paper, FontStyles.Bold);
            subtitleLabel = CreateLabel(panel, "", 72f, 44f, 410f, 22f, 15f, DeconstructUiTheme.MutedPaper);

            Button closeBtn = CreateButton(panel, "X", PanelWidth - 56f, 18f, 36f, 36f, Cancel, out _);
            theme.StyleButton(closeBtn);

            // Category Filter Toggles
            CreateLabel(panel, "INCLUDE CATEGORIES", 22f, 76f, 516f, 18f, 13f, DeconstructUiTheme.Gold, FontStyles.Bold);
            float toggleWidth = 123f;
            float toggleGap = 8f;
            buildingsToggleBtn = CreateButton(panel, "", 22f + (toggleWidth + toggleGap) * 0, 98f, toggleWidth, 34f,
                () => ToggleCategory(DeconstructCategory.Buildings), out buildingsToggleText);
            containersToggleBtn = CreateButton(panel, "", 22f + (toggleWidth + toggleGap) * 1, 98f, toggleWidth, 34f,
                () => ToggleCategory(DeconstructCategory.Containers), out containersToggleText);
            labelsToggleBtn = CreateButton(panel, "", 22f + (toggleWidth + toggleGap) * 2, 98f, toggleWidth, 34f,
                () => ToggleCategory(DeconstructCategory.Labels), out labelsToggleText);
            stickersToggleBtn = CreateButton(panel, "", 22f + (toggleWidth + toggleGap) * 3, 98f, toggleWidth, 34f,
                () => ToggleCategory(DeconstructCategory.Stickers), out stickersToggleText);

            // Building Type Counts Scroll List
            RectTransform listBox = CreateBox(panel, "BuildingCountsBox", 22f, 142f, PanelWidth - 44f, 272f, DeconstructUiTheme.Ink);
            CreateLabel(listBox, "BUILDING & ITEM COUNTS", 14f, 10f, 488f, 20f, 14f, DeconstructUiTheme.Gold, FontStyles.Bold);
            listContent = CreateScrollList(listBox, "BuildingRowsScroll", 8f, 34f, PanelWidth - 60f, 230f);

            // Total Refund Box
            RectTransform refundBox = CreateBox(panel, "RefundBox", 22f, 424f, PanelWidth - 44f, 64f, DeconstructUiTheme.Ink);
            CreateLabel(refundBox, "TOTAL REFUND", 14f, 8f, 488f, 18f, 13f, DeconstructUiTheme.Gold, FontStyles.Bold);
            refundContent = CreateRect(refundBox, "RefundItems", 14f, 28f, PanelWidth - 72f, 30f);
            HorizontalLayoutGroup refundLayout = refundContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            refundLayout.spacing = 14f;
            refundLayout.childAlignment = TextAnchor.MiddleLeft;
            refundLayout.childControlWidth = true;
            refundLayout.childControlHeight = true;
            refundLayout.childForceExpandWidth = false;
            refundLayout.childForceExpandHeight = false;

            // Confirm & Cancel Buttons
            confirmButton = CreateButton(panel, "Deconstruct", 22f, 502f, 250f, 40f, Confirm, out confirmButtonLabel);
            theme.StyleButton(confirmButton, accent: false, danger: true);

            Button cancelBtn = CreateButton(panel, "Cancel", PanelWidth - 272f, 502f, 250f, 40f, Cancel, out _);
            theme.StyleButton(cancelBtn);

            gameObject.SetActive(false);
        }

        public void Show(DeconstructSelection selection)
        {
            if (selection == null || !selection.HasAnyItems)
            {
                return;
            }

            currentSelection = selection;
            includeBuildings = BetterDeconstructPlugin.DefaultIncludeBuildings.Value;
            includeContainers = BetterDeconstructPlugin.DefaultIncludeContainers.Value;
            includeLabels = BetterDeconstructPlugin.DefaultIncludeLabels.Value;
            includeStickers = BetterDeconstructPlugin.DefaultIncludeStickers.Value;

            // If the user's default filters exclude everything that was actually selected,
            // enable the categories that are present so the modal isn't blank on open.
            if (selection.GetSelectedCount(includeBuildings, includeContainers, includeLabels, includeStickers) == 0)
            {
                includeBuildings = selection.TotalBuildingNodes > 0;
                includeContainers = selection.TotalContainers > 0;
                includeLabels = selection.TotalLabels > 0;
                includeStickers = selection.TotalStickers > 0;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UISounds.WindowOpen();
            RefreshView();
        }

        public void Cancel()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            currentSelection = null;
            base.OnPointerExit(null);
            gameObject.SetActive(false);
            UISounds.WindowClose();
        }

        private void Confirm()
        {
            if (currentSelection == null)
            {
                Cancel();
                return;
            }

            DeconstructSelection toExecute = currentSelection;
            currentSelection = null;
            base.OnPointerExit(null);
            gameObject.SetActive(false);

            toExecute.ExecuteDeconstruct(includeBuildings, includeContainers, includeLabels, includeStickers);

            if (!BetterDeconstructPlugin.StayInDeconstructMode.Value && GameUI.Instance &&
                GameUI.Instance.IsActive(GameUIType.Deconstruct))
            {
                GameUI.Instance.ToggleDeconstructMode(false);
            }
        }

        private void Update()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            if (!WorldFlowManager.Visible || !GameUI.Instance || GameUI.MenuVisible ||
                !GameUI.Instance.IsActive(GameUIType.Deconstruct))
            {
                currentSelection = null;
                base.OnPointerExit(null);
                gameObject.SetActive(false);
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            {
                if (confirmButton && confirmButton.interactable)
                {
                    Confirm();
                }
            }
        }

        private void OnDisable()
        {
            base.OnPointerExit(null);
        }

        private void ToggleCategory(DeconstructCategory category)
        {
            UISounds.CraftStep();
            switch (category)
            {
                case DeconstructCategory.Buildings:
                    includeBuildings = !includeBuildings;
                    break;
                case DeconstructCategory.Containers:
                    includeContainers = !includeContainers;
                    break;
                case DeconstructCategory.Labels:
                    includeLabels = !includeLabels;
                    break;
                case DeconstructCategory.Stickers:
                    includeStickers = !includeStickers;
                    break;
            }

            RefreshView();
        }

        private void RefreshView()
        {
            if (currentSelection == null)
            {
                return;
            }

            // Update Subtitle
            var parts = new List<string>();
            if (currentSelection.TotalBuildingUnits > 0)
            {
                string bText = currentSelection.TotalBuildingUnits == currentSelection.TotalBuildingNodes
                    ? $"{currentSelection.TotalBuildingUnits} building{(currentSelection.TotalBuildingUnits == 1 ? "" : "s")}"
                    : $"{currentSelection.TotalBuildingUnits} buildings ({currentSelection.TotalBuildingNodes} nodes)";
                parts.Add(bText);
            }
            if (currentSelection.TotalContainers > 0)
            {
                parts.Add($"{currentSelection.TotalContainers} container{(currentSelection.TotalContainers == 1 ? "" : "s")}");
            }
            if (currentSelection.TotalLabels > 0)
            {
                parts.Add($"{currentSelection.TotalLabels} label{(currentSelection.TotalLabels == 1 ? "" : "s")}");
            }
            if (currentSelection.TotalStickers > 0)
            {
                parts.Add($"{currentSelection.TotalStickers} sticker{(currentSelection.TotalStickers == 1 ? "" : "s")}");
            }
            subtitleLabel.text = parts.Count > 0 ? string.Join(" · ", parts) : "No items in selection";

            // Update Category Toggle Buttons
            UpdateCategoryButton(buildingsToggleBtn, buildingsToggleText, "Buildings",
                currentSelection.TotalBuildingUnits, includeBuildings);
            UpdateCategoryButton(containersToggleBtn, containersToggleText, "Containers",
                currentSelection.TotalContainers, includeContainers);
            UpdateCategoryButton(labelsToggleBtn, labelsToggleText, "Labels",
                currentSelection.TotalLabels, includeLabels);
            UpdateCategoryButton(stickersToggleBtn, stickersToggleText, "Stickers",
                currentSelection.TotalStickers, includeStickers);

            // Rebuild Building & Decoration Type Rows
            ClearChildren(listContent);
            List<DeconstructGroupRow> rows = currentSelection.BuildRows(
                includeBuildings, includeContainers, includeLabels, includeStickers);

            if (rows.Count == 0)
            {
                RectTransform emptyRow = CreateRect(listContent, "EmptyRow", 0f, 0f, 480f, 44f);
                LayoutElement le = emptyRow.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 44f;
                le.preferredHeight = 44f;
                TMP_Text emptyText = CreateLabel(emptyRow, "No categories selected for deconstruction.",
                    12f, 8f, 456f, 28f, 15f, DeconstructUiTheme.MutedPaper);
                emptyText.alignment = TextAlignmentOptions.Midline;
            }
            else
            {
                foreach (DeconstructGroupRow row in rows)
                {
                    BuildGroupRowUI(row);
                }
            }

            // Update Refund Display
            CurrencyCollection refund = currentSelection.CalculateRefund(
                includeBuildings, includeContainers, includeLabels, includeStickers);
            RebuildRefundUI(refund);

            // Update Confirm Button
            int selectedTotal = currentSelection.GetSelectedCount(
                includeBuildings, includeContainers, includeLabels, includeStickers);
            confirmButton.interactable = selectedTotal > 0;
            confirmButtonLabel.text = selectedTotal > 0 ? $"Deconstruct ({selectedTotal})" : "Deconstruct";
        }

        private void UpdateCategoryButton(Button button, TMP_Text label, string title, int count, bool enabled)
        {
            string check = enabled ? "[x]" : "[ ]";
            label.text = $"{check} {title} ({count})";
            label.color = enabled && count > 0 ? DeconstructUiTheme.Paper : DeconstructUiTheme.MutedPaper;
            theme.StyleButton(button, accent: false, danger: false, activeToggle: enabled && count > 0);
        }

        private void BuildGroupRowUI(DeconstructGroupRow row)
        {
            RectTransform rowRect = CreateBox(listContent, "Row_" + row.Key, 0f, 0f, 480f, 48f, DeconstructUiTheme.Wood);
            LayoutElement layoutElement = rowRect.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 48f;
            layoutElement.preferredHeight = 48f;

            // Icon Slot with In-Game Icon
            RectTransform iconSlot = CreateBox(rowRect, "IconSlot", 6f, 4f, 40f, 40f, DeconstructUiTheme.Ink);
            if (row.Icon != null)
            {
                CreateIcon(iconSlot, row.Icon, 4f, 4f, 32f);
            }

            // Building / Item Name
            string displayName = row.Name;
            if (row.Category == DeconstructCategory.Buildings && row.Count != row.NodeCount)
            {
                displayName = $"{row.Name} ({row.NodeCount} {(row.NodeCount == 1 ? "node" : "nodes")})";
            }

            TMP_Text nameLabel = CreateLabel(rowRect, displayName, 56f, 6f, 310f, 36f, 17f, DeconstructUiTheme.Paper);
            nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;

            // Count Badge on the right
            TMP_Text countLabel = CreateLabel(rowRect, $"× {row.Count}", 370f, 6f, 96f, 36f, 18f, DeconstructUiTheme.Paper, FontStyles.Bold);
            countLabel.alignment = TextAlignmentOptions.MidlineRight;
        }

        private void RebuildRefundUI(CurrencyCollection refund)
        {
            ClearChildren(refundContent);
            if (refund == null || refund.IsEmpty)
            {
                CreateRefundChip(null, "No refund", DeconstructUiTheme.MutedPaper);
                return;
            }

            bool anyAdded = false;
            foreach (var entry in refund.GetCurrency())
            {
                if (entry.amt <= 0)
                {
                    continue;
                }

                anyAdded = true;
                if (entry.c == Currency.Gold)
                {
                    AddGoldRefundChips(entry.amt);
                }
                else
                {
                    CreateRefundChip(entry.c.GetIcon(), GameMath.FormatNumber(entry.amt), DeconstructUiTheme.Paper);
                }
            }

            if (!anyAdded)
            {
                CreateRefundChip(null, "No refund", DeconstructUiTheme.MutedPaper);
            }
        }

        private void AddGoldRefundChips(long amount)
        {
            int copper = (int)(amount % 100);
            amount /= 100;
            int silver = (int)(amount % 100);
            amount /= 100;
            long gold = amount;

            if (gold > 0)
            {
                CreateRefundChip(Currency.Gold.GetIcon(), GameMath.FormatNumber(gold), DeconstructUiTheme.Paper);
            }
            if (silver > 0 || gold > 0)
            {
                CreateRefundChip(silverIcon ?? Currency.Gold.GetIcon(), silver.ToString(), DeconstructUiTheme.Paper);
            }
            if (copper > 0 || (gold == 0 && silver == 0))
            {
                CreateRefundChip(copperIcon ?? Currency.Gold.GetIcon(), copper.ToString(), DeconstructUiTheme.Paper);
            }
        }

        private void CreateRefundChip(Sprite icon, string text, Color textColor)
        {
            RectTransform chip = CreateRect(refundContent, "CurrencyChip", 0f, 0f, 0f, 28f);
            HorizontalLayoutGroup hlg = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 5f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            if (icon != null)
            {
                RectTransform iconRect = CreateRect(chip, "Icon", 0f, 0f, 22f, 22f);
                LayoutElement iconLe = iconRect.gameObject.AddComponent<LayoutElement>();
                iconLe.minWidth = 22f;
                iconLe.preferredWidth = 22f;
                iconLe.minHeight = 22f;
                iconLe.preferredHeight = 22f;
                Image img = iconRect.gameObject.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            RectTransform textRect = CreateRect(chip, "Amount", 0f, 0f, 0f, 24f);
            TextMeshProUGUI label = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 16f;
            label.color = textColor;
            label.text = text;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            ContentSizeFitter fitter = textRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void CacheCurrencySprites(GameUI ui)
        {
            try
            {
                DeconstructOverlay overlay = ui.GetComponentInChildren<DeconstructOverlay>(true);
                if (overlay != null)
                {
                    CurrencyDisplay display = OverlayRefundField(overlay);
                    if (display != null)
                    {
                        copperIcon = CopperIconField(display);
                        silverIcon = SilverIconField(display);
                    }
                }
            }
            catch
            {
                // Fallback to gold icon if reflection fields ever change.
            }
        }

        private static void ClearChildren(Transform parent)
        {
            if (!parent)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private static RectTransform CreateRect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        private RectTransform CreateBox(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            RectTransform rect = CreateRect(parent, name, x, y, w, h);
            Image image = rect.gameObject.AddComponent<Image>();
            theme.ApplySlicedImage(image, color == DeconstructUiTheme.Ink ? "ItemSlot_0" : "Tooltip", color);
            return rect;
        }

        private static Image CreateIcon(Transform parent, Sprite sprite, float x, float y, float size)
        {
            Image image = CreateRect(parent, "Icon", x, y, size, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text CreateLabel(
            Transform parent,
            string text,
            float x,
            float y,
            float w,
            float h,
            float fontSize,
            Color color,
            FontStyles style = FontStyles.Normal)
        {
            TextMeshProUGUI label = CreateRect(parent, "Label", x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.text = text;
            label.richText = false;
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private Button CreateButton(
            Transform parent,
            string text,
            float x,
            float y,
            float w,
            float h,
            Action onClick,
            out TMP_Text buttonLabel)
        {
            RectTransform rect = CreateRect(parent, "Button", x, y, w, h);
            Image bg = rect.gameObject.AddComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            theme.StyleButton(button);

            float inset = w < 60f ? 6f : 10f;
            buttonLabel = CreateLabel(rect, text, inset, 0f, w - inset * 2f, h, 15f, DeconstructUiTheme.Paper);
            buttonLabel.alignment = TextAlignmentOptions.Midline;
            buttonLabel.enableAutoSizing = true;
            buttonLabel.fontSizeMin = 11f;
            buttonLabel.fontSizeMax = 15f;

            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            return button;
        }

        private RectTransform CreateScrollList(Transform parent, string name, float x, float y, float w, float h)
        {
            RectTransform root = CreateRect(parent, name, x, y, w, h);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = CreateRect(root, "Viewport", 0f, 0f, w - 22f, h);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportHit = viewport.gameObject.AddComponent<Image>();
            viewportHit.color = new Color(0f, 0f, 0f, 0.01f);

            RectTransform content = CreateRect(viewport, "Content", 0f, 0f, w - 22f, 0f);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            // Scrollbar
            RectTransform track = CreateRect(root, "Scrollbar", w - 18f, 0f, 18f, h);
            theme.ApplySlicedImage(track.gameObject.AddComponent<Image>(), "ScrollbarBG", DeconstructUiTheme.Ink);
            RectTransform slide = CreateRect(track, "SlidingArea", 3f, 8f, 12f, h - 16f);
            RectTransform handle = CreateRect(slide, "Handle", 0f, 0f, 12f, 24f);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            Image handleGraphic = handle.gameObject.AddComponent<Image>();
            theme.ApplySlicedImage(handleGraphic, "ScrollHandle", DeconstructUiTheme.Gold);

            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = handleGraphic;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;

            return content;
        }
    }
}
