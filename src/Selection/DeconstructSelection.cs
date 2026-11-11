using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Behaviour.Flow;
using Assets.Behaviour.UI;
using Assets.Behaviour.UI.Shared;
using Assets.Source.Flow;
using Assets.Source.Player;
using KingdomFoundation.Api;
using UnityEngine;

namespace KingdomBetterDeconstruct
{
    internal enum DeconstructCategory
    {
        Buildings,
        Containers,
        Labels,
        Stickers
    }

    internal sealed class DeconstructGroupRow
    {
        public DeconstructCategory Category;
        public string Key;
        public string Name;
        public Sprite Icon;
        public int Count;
        public int NodeCount;
    }

    internal sealed class DeconstructSelection
    {
        public readonly List<FlowElement> Buildings = new List<FlowElement>();
        public readonly List<FlowDeco> Containers = new List<FlowDeco>();
        public readonly List<FlowDeco> Labels = new List<FlowDeco>();
        public readonly List<FlowDeco> Stickers = new List<FlowDeco>();

        public int TotalBuildingUnits => Buildings.Sum(b => Math.Max(1, b.StackSize));
        public int TotalBuildingNodes => Buildings.Count;
        public int TotalContainers => Containers.Count;
        public int TotalLabels => Labels.Count;
        public int TotalStickers => Stickers.Count;

        public bool HasAnyItems =>
            Buildings.Count > 0 || Containers.Count > 0 || Labels.Count > 0 || Stickers.Count > 0;

        public int GetSelectedCount(bool includeBuildings, bool includeContainers, bool includeLabels, bool includeStickers)
        {
            int total = 0;
            if (includeBuildings) total += TotalBuildingUnits;
            if (includeContainers) total += TotalContainers;
            if (includeLabels) total += TotalLabels;
            if (includeStickers) total += TotalStickers;
            return total;
        }

        public static DeconstructSelection FromBox(Vector2Int anchor, Vector2Int cursor, bool ignoreLockedDecorations)
        {
            var result = new DeconstructSelection();
            if (!FlowColliderManager.Instance || !WorldFlowManager.Instance)
            {
                return result;
            }

            FlowDiagram activeFlow = FoundationApi.World.ActiveFlow;
            if (activeFlow == null)
            {
                return result;
            }

            int minX = Mathf.Min(anchor.x, cursor.x);
            int maxX = Mathf.Max(anchor.x, cursor.x);
            int minY = Mathf.Min(anchor.y, cursor.y);
            int maxY = Mathf.Max(anchor.y, cursor.y);
            Vector2Int topLeft = new Vector2Int(minX, maxY);
            int rawW = maxX - minX;
            int rawH = maxY - minY;
            if (rawW < 2 && rawH < 2)
            {
                return result;
            }

            Vector2Int size = new Vector2Int(Mathf.Max(2, rawW), Mathf.Max(2, rawH));

            var seenElements = new HashSet<FlowElement>();
            var seenDecos = new HashSet<FlowDeco>();

            foreach (Collider2D collider in FlowColliderManager.Instance.GetOverlap(topLeft, size))
            {
                if (!collider)
                {
                    continue;
                }

                WorldFlowElement worldElement = collider.GetComponent<WorldFlowElement>();
                if (!worldElement && collider.TryGetComponent<FlowCollider>(out var flowCollider))
                {
                    worldElement = flowCollider.Element;
                }

                if (worldElement && worldElement.Element != null &&
                    activeFlow.Elements.Contains(worldElement.Element) &&
                    seenElements.Add(worldElement.Element))
                {
                    result.Buildings.Add(worldElement.Element);
                    continue;
                }

                WorldFlowDeco worldDeco = collider.GetComponent<WorldFlowDeco>();
                if (!worldDeco && collider.TryGetComponent<FlowCollider>(out var decoCollider))
                {
                    worldDeco = decoCollider.Deco;
                }

                if (worldDeco && worldDeco.Decoration != null &&
                    activeFlow.Decorations.Contains(worldDeco.Decoration) &&
                    seenDecos.Add(worldDeco.Decoration))
                {
                    FlowDeco deco = worldDeco.Decoration;
                    if (ignoreLockedDecorations && deco.Locked)
                    {
                        continue;
                    }

                    if (deco.Sticker != null)
                    {
                        result.Stickers.Add(deco);
                    }
                    else if (deco.Label != null)
                    {
                        result.Labels.Add(deco);
                    }
                    else
                    {
                        result.Containers.Add(deco);
                    }
                }
            }

            return result;
        }

        public List<DeconstructGroupRow> BuildRows(
            bool includeBuildings,
            bool includeContainers,
            bool includeLabels,
            bool includeStickers)
        {
            var rows = new List<DeconstructGroupRow>();

            if (includeBuildings && Buildings.Count > 0)
            {
                var groups = new Dictionary<string, DeconstructGroupRow>(StringComparer.Ordinal);
                foreach (FlowElement element in Buildings)
                {
                    string key = element.ElementType ?? element.GetType().Name;
                    if (!groups.TryGetValue(key, out DeconstructGroupRow group))
                    {
                        group = new DeconstructGroupRow
                        {
                            Category = DeconstructCategory.Buildings,
                            Key = key,
                            Name = DeconstructIconResolver.GetBuildingName(element),
                            Icon = DeconstructIconResolver.GetBuildingIcon(element),
                            Count = 0,
                            NodeCount = 0
                        };
                        groups.Add(key, group);
                    }
                    else if (group.Icon == null)
                    {
                        group.Icon = DeconstructIconResolver.GetBuildingIcon(element);
                    }

                    group.Count += Math.Max(1, element.StackSize);
                    group.NodeCount += 1;
                }

                rows.AddRange(groups.Values
                    .OrderByDescending(g => g.Count)
                    .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase));
            }

            if (includeContainers && Containers.Count > 0)
            {
                rows.Add(new DeconstructGroupRow
                {
                    Category = DeconstructCategory.Containers,
                    Key = "__containers__",
                    Name = DeconstructIconResolver.GetContainerName(),
                    Icon = DeconstructIconResolver.GetContainerIcon(),
                    Count = Containers.Count,
                    NodeCount = Containers.Count
                });
            }

            if (includeLabels && Labels.Count > 0)
            {
                rows.Add(new DeconstructGroupRow
                {
                    Category = DeconstructCategory.Labels,
                    Key = "__labels__",
                    Name = DeconstructIconResolver.GetLabelName(),
                    Icon = DeconstructIconResolver.GetLabelIcon(),
                    Count = Labels.Count,
                    NodeCount = Labels.Count
                });
            }

            if (includeStickers && Stickers.Count > 0)
            {
                rows.Add(new DeconstructGroupRow
                {
                    Category = DeconstructCategory.Stickers,
                    Key = "__stickers__",
                    Name = DeconstructIconResolver.GetStickerName(),
                    Icon = DeconstructIconResolver.GetStickerIcon(Stickers[0]),
                    Count = Stickers.Count,
                    NodeCount = Stickers.Count
                });
            }

            return rows;
        }

        public CurrencyCollection CalculateRefund(
            bool includeBuildings,
            bool includeContainers,
            bool includeLabels,
            bool includeStickers)
        {
            var total = new CurrencyCollection();
            if (GamePlayer.Current == null)
            {
                return total;
            }

            if (includeBuildings && Buildings.Count > 0)
            {
                var refundedByType = new Dictionary<Type, int>();
                foreach (FlowElement element in Buildings)
                {
                    if (element == null)
                    {
                        continue;
                    }

                    Type type = element.GetType();
                    refundedByType.TryGetValue(type, out int alreadyRefunded);
                    CurrencyCollection refund = element.GetRefund(alreadyRefunded);
                    if (refund != null)
                    {
                        total.Add(refund);
                    }

                    refundedByType[type] = alreadyRefunded + Math.Max(1, element.StackSize);
                }
            }

            if (includeContainers)
            {
                foreach (FlowDeco container in Containers)
                {
                    CurrencyCollection refund = container?.GetRefund(withAnchored: false);
                    if (refund != null)
                    {
                        total.Add(refund);
                    }
                }
            }

            if (includeLabels)
            {
                foreach (FlowDeco label in Labels)
                {
                    CurrencyCollection refund = label?.GetRefund(withAnchored: false);
                    if (refund != null)
                    {
                        total.Add(refund);
                    }
                }
            }

            if (includeStickers)
            {
                foreach (FlowDeco sticker in Stickers)
                {
                    CurrencyCollection refund = sticker?.GetRefund(withAnchored: false);
                    if (refund != null)
                    {
                        total.Add(refund);
                    }
                }
            }

            return total;
        }

        public int ExecuteDeconstruct(
            bool includeBuildings,
            bool includeContainers,
            bool includeLabels,
            bool includeStickers)
        {
            if (!WorldFlowManager.Instance || GamePlayer.Current == null)
            {
                return 0;
            }

            FlowDiagram activeFlow = FoundationApi.World.ActiveFlow;
            if (activeFlow == null)
            {
                return 0;
            }

            int removedCount = 0;
            var totalRefund = new CurrencyCollection();

            if (includeBuildings)
            {
                foreach (FlowElement element in Buildings)
                {
                    if (element == null || !activeFlow.Elements.Contains(element))
                    {
                        continue;
                    }

                    CurrencyCollection refund = element.GetRefund();
                    if (refund != null && !refund.IsEmpty)
                    {
                        totalRefund.Add(refund);
                    }

                    WorldFlowManager.Instance.RemoveElement(element);
                    removedCount += Math.Max(1, element.StackSize);
                }
            }

            if (includeContainers)
            {
                removedCount += RemoveDecorations(Containers, activeFlow, totalRefund);
            }

            if (includeLabels)
            {
                removedCount += RemoveDecorations(Labels, activeFlow, totalRefund);
            }

            if (includeStickers)
            {
                removedCount += RemoveDecorations(Stickers, activeFlow, totalRefund);
            }

            if (!totalRefund.IsEmpty)
            {
                FoundationApi.Economy.Grant(totalRefund, countAsIncome: false);
            }

            if (removedCount > 0)
            {
                UISounds.TurnPage();
                if (WorldFlowCamera.Instance)
                {
                    WorldFlowCamera.Instance.RecalculateBounds();
                }
            }

            return removedCount;
        }

        private static int RemoveDecorations(
            List<FlowDeco> decos,
            FlowDiagram activeFlow,
            CurrencyCollection totalRefund)
        {
            int count = 0;
            foreach (FlowDeco deco in decos)
            {
                if (deco == null || !activeFlow.Decorations.Contains(deco) || !deco.World)
                {
                    continue;
                }

                CurrencyCollection refund = deco.GetRefund(withAnchored: false);
                if (refund != null && !refund.IsEmpty)
                {
                    totalRefund.Add(refund);
                }

                WorldFlowManager.Instance.RemoveDecoration(deco.World);
                count++;
            }

            return count;
        }
    }
}
