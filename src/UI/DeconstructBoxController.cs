using Assets.Behaviour.Flow;
using Assets.Behaviour.UI;
using Assets.Behaviour.UI.BuyMenu;
using Assets.Behaviour.UI.HUD;
using Assets.Behaviour.UI.Shared;
using Assets.Source.Player;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using GamePlayerInput = Assets.Behaviour.Input.PlayerInput;

namespace KingdomBetterDeconstruct
{
    internal sealed class DeconstructBoxController : MonoBehaviour
    {
        private const float DragThresholdPixels = 12f;
        private const float BorderThickness = 2f;
        private const float PreviewIntervalSeconds = 0.08f;

        private static readonly AccessTools.FieldRef<GameUI, AreaCopyGhost> CloneGhostField =
            AccessTools.FieldRefAccess<GameUI, AreaCopyGhost>("_cloneGhost");

        private static readonly AccessTools.FieldRef<DeconstructOverlay, RectTransform> RefundContainerField =
            AccessTools.FieldRefAccess<DeconstructOverlay, RectTransform>("_refundContainer");

        private static readonly AccessTools.FieldRef<DeconstructOverlay, CurrencyDisplay> RefundDisplayField =
            AccessTools.FieldRefAccess<DeconstructOverlay, CurrencyDisplay>("_refund");

        public static DeconstructBoxController Instance { get; private set; }
        public static bool IsDragging => Instance != null && Instance._isDragging;

        private RectTransform _boxRect;
        private GameObject _visualRoot;

        private bool _pointerDown;
        private bool _isDragging;
        private bool _suppressUntilRelease;
        private Vector2 _startScreenPos;
        private Vector2Int _anchorWorld;
        private float _nextPreviewTime;

        public void Initialize(GameUI ui)
        {
            Instance = this;

            AreaCopyGhost cloneGhost = CloneGhostField(ui);
            RectTransform templateRect = cloneGhost != null ? cloneGhost.GetComponent<RectTransform>() : null;
            Transform worldParent = templateRect != null ? templateRect.parent : ui.transform;

            _boxRect = gameObject.GetComponent<RectTransform>();
            if (_boxRect == null)
            {
                _boxRect = gameObject.AddComponent<RectTransform>();
            }

            _boxRect.SetParent(worldParent, false);
            if (templateRect != null)
            {
                _boxRect.anchorMin = templateRect.anchorMin;
                _boxRect.anchorMax = templateRect.anchorMax;
                _boxRect.pivot = templateRect.pivot;
                _boxRect.localScale = templateRect.localScale;
                _boxRect.localRotation = templateRect.localRotation;
            }
            else
            {
                _boxRect.anchorMin = new Vector2(0f, 1f);
                _boxRect.anchorMax = new Vector2(0f, 1f);
                _boxRect.pivot = new Vector2(0f, 1f);
                _boxRect.localScale = Vector3.one;
            }

            _boxRect.anchoredPosition = Vector2.zero;
            _boxRect.sizeDelta = Vector2.zero;

            _visualRoot = new GameObject("Visuals", typeof(RectTransform));
            RectTransform visualRect = (RectTransform)_visualRoot.transform;
            visualRect.SetParent(_boxRect, false);
            visualRect.anchorMin = Vector2.zero;
            visualRect.anchorMax = Vector2.one;
            visualRect.offsetMin = Vector2.zero;
            visualRect.offsetMax = Vector2.zero;

            Color fillColor = new Color(0.82f, 0.16f, 0.16f, 0.18f);
            Color borderColor = new Color(0.96f, 0.28f, 0.24f, 0.92f);

            Image fill = _visualRoot.AddComponent<Image>();
            fill.color = fillColor;
            fill.raycastTarget = false;

            CreateEdge(visualRect, "TopEdge", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -BorderThickness), Vector2.zero, borderColor);
            CreateEdge(visualRect, "BottomEdge", new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, BorderThickness), borderColor);
            CreateEdge(visualRect, "LeftEdge", new Vector2(0f, 0f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(BorderThickness, 0f), borderColor);
            CreateEdge(visualRect, "RightEdge", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-BorderThickness, 0f), Vector2.zero, borderColor);

            _visualRoot.SetActive(false);
        }

        public bool HandleOverlayUpdate(DeconstructOverlay overlay)
        {
            if (DeconstructConfirmModal.IsOpen)
            {
                HideVisuals();
                HideOverlayRefund(overlay);
                _pointerDown = false;
                _isDragging = false;
                return true;
            }

            if (!WorldFlowManager.Visible || !WorldFlowManager.Instance || !GameUI.Instance || GameUI.MenuVisible)
            {
                ResetState(overlay);
                return false;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            if (_suppressUntilRelease)
            {
                HideVisuals();
                HideOverlayRefund(overlay);
                if (!mouse.leftButton.isPressed && !GamePlayerInput.InteractReleased)
                {
                    _suppressUntilRelease = false;
                }

                return true;
            }

            if (mouse.leftButton.wasPressedThisFrame && !_pointerDown)
            {
                if (!IsPointerOverHudUi(overlay))
                {
                    _pointerDown = true;
                    _isDragging = false;
                    _startScreenPos = mouse.position.ReadValue();
                    _anchorWorld = GamePlayerInput.MouseWorld;
                }
            }

            if (!_pointerDown)
            {
                return false;
            }

            if (mouse.leftButton.isPressed && !GamePlayerInput.InteractReleased)
            {
                Vector2 currentScreenPos = mouse.position.ReadValue();
                if (!_isDragging)
                {
                    float screenDist = Vector2.Distance(_startScreenPos, currentScreenPos);
                    if (screenDist >= DragThresholdPixels)
                    {
                        _isDragging = true;
                        _nextPreviewTime = 0f;
                        if (_visualRoot != null)
                        {
                            _visualRoot.SetActive(true);
                        }
                    }
                }

                if (_isDragging)
                {
                    PanAtScreenEdge();
                    Vector2Int cursorWorld = GamePlayerInput.MouseWorld;
                    UpdateBoxTransform(_anchorWorld, cursorWorld);
                    UpdateLiveRefundPreview(overlay, _anchorWorld, cursorWorld);
                    return true;
                }

                return false;
            }

            if (GamePlayerInput.InteractReleased || mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed)
            {
                bool wasDragging = _isDragging;
                _pointerDown = false;
                _isDragging = false;
                HideVisuals();

                if (wasDragging)
                {
                    HideOverlayRefund(overlay);
                    Vector2Int cursorWorld = GamePlayerInput.MouseWorld;
                    DeconstructSelection selection = DeconstructSelection.FromBox(
                        _anchorWorld,
                        cursorWorld,
                        BetterDeconstructPlugin.IgnoreLockedDecorations.Value);

                    if (selection.HasAnyItems && DeconstructConfirmModal.Instance != null)
                    {
                        DeconstructConfirmModal.Instance.Show(selection);
                    }

                    return true;
                }
            }

            return false;
        }

        public void CancelDrag()
        {
            if (!_isDragging && !_pointerDown)
            {
                return;
            }

            _pointerDown = false;
            _isDragging = false;
            _suppressUntilRelease = true;
            HideVisuals();
        }

        public void ResetState(DeconstructOverlay overlay = null)
        {
            _pointerDown = false;
            _isDragging = false;
            _suppressUntilRelease = false;
            HideVisuals();
            if (overlay != null)
            {
                HideOverlayRefund(overlay);
            }
        }

        private static bool IsPointerOverHudUi(DeconstructOverlay overlay)
        {
            foreach (GameObject go in UIHelper.GetAllMouseOverUI())
            {
                if (!go)
                {
                    continue;
                }

                if (go.GetComponentInParent<WorldFlowManager>() != null)
                {
                    continue;
                }

                if (overlay != null && go.transform.IsChildOf(overlay.transform))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void UpdateBoxTransform(Vector2Int anchor, Vector2Int cursor)
        {
            if (_boxRect == null)
            {
                return;
            }

            int minX = Mathf.Min(anchor.x, cursor.x);
            int maxX = Mathf.Max(anchor.x, cursor.x);
            int minY = Mathf.Min(anchor.y, cursor.y);
            int maxY = Mathf.Max(anchor.y, cursor.y);

            _boxRect.anchoredPosition = new Vector2(minX, maxY);
            _boxRect.sizeDelta = new Vector2(Mathf.Max(2, maxX - minX), Mathf.Max(2, maxY - minY));
        }

        private void UpdateLiveRefundPreview(DeconstructOverlay overlay, Vector2Int anchor, Vector2Int cursor)
        {
            if (overlay == null)
            {
                return;
            }

            if (Time.unscaledTime < _nextPreviewTime)
            {
                return;
            }

            _nextPreviewTime = Time.unscaledTime + PreviewIntervalSeconds;

            RectTransform refundContainer = RefundContainerField(overlay);
            CurrencyDisplay refundDisplay = RefundDisplayField(overlay);
            if (refundContainer == null || refundDisplay == null)
            {
                return;
            }

            DeconstructSelection selection = DeconstructSelection.FromBox(
                anchor,
                cursor,
                BetterDeconstructPlugin.IgnoreLockedDecorations.Value);

            if (!selection.HasAnyItems)
            {
                refundContainer.gameObject.SetActive(false);
                return;
            }

            CurrencyCollection refund = selection.CalculateRefund(
                BetterDeconstructPlugin.DefaultIncludeBuildings.Value,
                BetterDeconstructPlugin.DefaultIncludeContainers.Value,
                BetterDeconstructPlugin.DefaultIncludeLabels.Value,
                BetterDeconstructPlugin.DefaultIncludeStickers.Value);

            if (refund != null && !refund.IsEmpty)
            {
                refundDisplay.SetAmount(refund);
                refundContainer.gameObject.SetActive(true);
            }
            else
            {
                refundContainer.gameObject.SetActive(false);
            }
        }

        private static void HideOverlayRefund(DeconstructOverlay overlay)
        {
            if (overlay == null)
            {
                return;
            }

            RectTransform refundContainer = RefundContainerField(overlay);
            if (refundContainer != null && refundContainer.gameObject.activeSelf)
            {
                refundContainer.gameObject.SetActive(false);
            }
        }

        private void HideVisuals()
        {
            if (_visualRoot != null && _visualRoot.activeSelf)
            {
                _visualRoot.SetActive(false);
            }
        }

        private static void PanAtScreenEdge()
        {
            if (!WorldFlowCamera.Instance || Mouse.current == null)
            {
                return;
            }

            Vector2 pos = Mouse.current.position.ReadValue();
            int edgeX = pos.x < 100f ? -1 : (pos.x > Screen.width - 100 ? 1 : 0);
            int edgeY = pos.y < 100f ? -1 : (pos.y > Screen.height - 100 ? 1 : 0);
            if (edgeX != 0 || edgeY != 0)
            {
                float zoomFactor = Mathf.Max(1f, 5f - WorldFlowCamera.Instance.ZoomLevel);
                float dt = Mathf.Min(0.1f, Time.unscaledDeltaTime) * 450f * zoomFactor;
                WorldFlowCamera.Instance.Move(edgeX * dt, edgeY * dt);
            }
        }

        private static void CreateEdge(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color)
        {
            var edgeRect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            edgeRect.SetParent(parent, false);
            edgeRect.anchorMin = anchorMin;
            edgeRect.anchorMax = anchorMax;
            edgeRect.offsetMin = offsetMin;
            edgeRect.offsetMax = offsetMax;

            Image img = edgeRect.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }
    }
}
