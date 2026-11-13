using KingdomFoundation.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KingdomBetterDeconstruct
{
    internal sealed class DeconstructBoxController : MonoBehaviour
    {
        private const float DragThresholdPixels = 12f;
        private const float BorderThickness = 2f;
        private const float PreviewIntervalSeconds = 0.08f;

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

        public void Initialize(Component ui)
        {
            Instance = this;

            RectTransform templateRect = FoundationApi.UiAccess.WorldOverlayTemplate;
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

        public bool HandleOverlayUpdate(Component overlay)
        {
            if (DeconstructConfirmModal.IsOpen)
            {
                HideVisuals();
                HideOverlayRefund(overlay);
                _pointerDown = false;
                _isDragging = false;
                return true;
            }

            if (!FoundationApi.UiAccess.IsWorkspaceVisible || !FoundationApi.UiAccess.Root || FoundationApi.UiAccess.IsMenuVisible)
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
                if (!mouse.leftButton.isPressed && !FoundationApi.View.InteractReleased)
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
                    _anchorWorld = FoundationApi.View.MouseWorld;
                }
            }

            if (!_pointerDown)
            {
                return false;
            }

            if (mouse.leftButton.isPressed && !FoundationApi.View.InteractReleased)
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
                    Vector2Int cursorWorld = FoundationApi.View.MouseWorld;
                    UpdateBoxTransform(_anchorWorld, cursorWorld);
                    UpdateLiveRefundPreview(overlay, _anchorWorld, cursorWorld);
                    return true;
                }

                return false;
            }

            if (FoundationApi.View.InteractReleased || mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed)
            {
                bool wasDragging = _isDragging;
                _pointerDown = false;
                _isDragging = false;
                HideVisuals();

                if (wasDragging)
                {
                    HideOverlayRefund(overlay);
                    Vector2Int cursorWorld = FoundationApi.View.MouseWorld;
                    DeconstructSelection selection = DeconstructSelection.FromBox(
                        _anchorWorld,
                        cursorWorld,
                        BetterDeconstructPlugin.IgnoreLockedDecorations.Value);

                    // Native Single mode exits on the selection release. Do this
                    // before opening the independent confirmation, because the
                    // ToggleDeconstructMode cleanup cancels an already-open modal.
                    if (FoundationApi.UiAccess.DeconstructionMode == GameDeconstructionMode.Single)
                    {
                        FoundationApi.UiAccess.SetDeconstructionVisible(false);
                    }

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

        public void ResetState(Component overlay = null)
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

        private static bool IsPointerOverHudUi(Component overlay) => FoundationApi.UiAccess.IsPointerOverHud(overlay);

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

        private void UpdateLiveRefundPreview(Component overlay, Vector2Int anchor, Vector2Int cursor)
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

            DeconstructSelection selection = DeconstructSelection.FromBox(
                anchor,
                cursor,
                BetterDeconstructPlugin.IgnoreLockedDecorations.Value);

            if (!selection.HasAnyItems)
            {
                FoundationApi.UiAccess.HideDeconstructionRefund(overlay);
                return;
            }

            GameMoney refund = selection.CalculateRefund(
                BetterDeconstructPlugin.DefaultIncludeBuildings.Value,
                BetterDeconstructPlugin.DefaultIncludeContainers.Value,
                BetterDeconstructPlugin.DefaultIncludeLabels.Value,
                BetterDeconstructPlugin.DefaultIncludeStickers.Value);

            FoundationApi.UiAccess.SetDeconstructionRefund(overlay, refund);
        }

        private static void HideOverlayRefund(Component overlay)
        {
            if (overlay) FoundationApi.UiAccess.HideDeconstructionRefund(overlay);
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
            if (!FoundationApi.View.IsReady || Mouse.current == null)
            {
                return;
            }

            Vector2 pos = Mouse.current.position.ReadValue();
            int edgeX = pos.x < 100f ? -1 : (pos.x > Screen.width - 100 ? 1 : 0);
            int edgeY = pos.y < 100f ? -1 : (pos.y > Screen.height - 100 ? 1 : 0);
            if (edgeX != 0 || edgeY != 0)
            {
                float zoomFactor = Mathf.Max(1f, 5f - FoundationApi.View.CameraZoom);
                float dt = Mathf.Min(0.1f, Time.unscaledDeltaTime) * 450f * zoomFactor;
                FoundationApi.View.PanCamera(edgeX * dt, edgeY * dt);
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
