using System;
using System.Collections.Generic;
using System.Linq;
using KingdomFoundation.Api;
using UnityEngine;

namespace KingdomBetterDeconstruct
{
    internal sealed class DeconstructSelection
    {
        public readonly List<WorkspaceObject> Buildings = new List<WorkspaceObject>();
        public readonly List<WorkspaceObject> Containers = new List<WorkspaceObject>();
        public readonly List<WorkspaceObject> Labels = new List<WorkspaceObject>();
        public readonly List<WorkspaceObject> Stickers = new List<WorkspaceObject>();
        public int TotalBuildingUnits => Buildings.Sum(b => Math.Max(1, b.StackSize));
        public int TotalBuildingNodes => Buildings.Count;
        public int TotalContainers => Containers.Count;
        public int TotalLabels => Labels.Count;
        public int TotalStickers => Stickers.Count;
        public bool HasAnyItems => Buildings.Count > 0 || Containers.Count > 0 || Labels.Count > 0 || Stickers.Count > 0;

        public int GetSelectedCount(bool includeBuildings, bool includeContainers, bool includeLabels, bool includeStickers)
        {
            int total = 0;
            if (includeBuildings)
                total += TotalBuildingUnits;
            if (includeContainers)
                total += TotalContainers;
            if (includeLabels)
                total += TotalLabels;
            if (includeStickers)
                total += TotalStickers;
            return total;
        }

        public static DeconstructSelection FromBox(Vector2Int anchor, Vector2Int cursor, bool ignoreLockedDecorations)
        {
            var result = new DeconstructSelection();
            if (!FoundationApi.WorkspaceAccess.IsReady)
                return result;
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
            foreach (WorkspaceObject item in FoundationApi.WorkspaceAccess.Overlap(topLeft, size))
            {
                if (ignoreLockedDecorations && item.Locked)
                    continue;
                switch (item.Kind)
                {
                    case WorkspaceObjectKind.Building:
                        result.Buildings.Add(item);
                        break;
                    case WorkspaceObjectKind.Container:
                        result.Containers.Add(item);
                        break;
                    case WorkspaceObjectKind.Label:
                        result.Labels.Add(item);
                        break;
                    case WorkspaceObjectKind.Sticker:
                        result.Stickers.Add(item);
                        break;
                }
            }

            return result;
        }

        public List<DeconstructGroupRow> BuildRows(bool includeBuildings, bool includeContainers, bool includeLabels, bool includeStickers)
        {
            var rows = new List<DeconstructGroupRow>();
            if (includeBuildings && Buildings.Count > 0)
            {
                var groups = new Dictionary<string, DeconstructGroupRow>(StringComparer.Ordinal);
                foreach (WorkspaceObject element in Buildings)
                {
                    string key = element.TypeId;
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

                rows.AddRange(groups.Values.OrderByDescending(g => g.Count).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase));
            }

            if (includeContainers && Containers.Count > 0)
            {
                rows.Add(new DeconstructGroupRow { Category = DeconstructCategory.Containers, Key = "__containers__", Name = DeconstructIconResolver.GetContainerName(), Icon = DeconstructIconResolver.GetContainerIcon(), Count = Containers.Count, NodeCount = Containers.Count });
            }

            if (includeLabels && Labels.Count > 0)
            {
                rows.Add(new DeconstructGroupRow { Category = DeconstructCategory.Labels, Key = "__labels__", Name = DeconstructIconResolver.GetLabelName(), Icon = DeconstructIconResolver.GetLabelIcon(), Count = Labels.Count, NodeCount = Labels.Count });
            }

            if (includeStickers && Stickers.Count > 0)
            {
                rows.Add(new DeconstructGroupRow { Category = DeconstructCategory.Stickers, Key = "__stickers__", Name = DeconstructIconResolver.GetStickerName(), Icon = DeconstructIconResolver.GetStickerIcon(Stickers[0]), Count = Stickers.Count, NodeCount = Stickers.Count });
            }

            return rows;
        }

        public GameMoney CalculateRefund(bool includeBuildings, bool includeContainers, bool includeLabels, bool includeStickers)
        {
            var total = new GameMoney();
            if (!FoundationApi.World.IsInKingdom)
            {
                return total;
            }

            if (includeBuildings && Buildings.Count > 0)
            {
                var refundedByType = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (WorkspaceObject element in Buildings)
                {
                    if (element == null)
                    {
                        continue;
                    }

                    string type = element.TypeId;
                    refundedByType.TryGetValue(type, out int alreadyRefunded);
                    GameMoney refund = FoundationApi.WorkspaceAccess.Refund(element, alreadyRefunded);
                    if (refund != null)
                    {
                        total.Add(refund);
                    }

                    refundedByType[type] = alreadyRefunded + Math.Max(1, element.StackSize);
                }
            }

            if (includeContainers)
            {
                foreach (WorkspaceObject container in Containers)
                {
                    GameMoney refund = container == null ? null : FoundationApi.WorkspaceAccess.Refund(container);
                    if (refund != null)
                    {
                        total.Add(refund);
                    }
                }
            }

            if (includeLabels)
            {
                foreach (WorkspaceObject label in Labels)
                {
                    GameMoney refund = label == null ? null : FoundationApi.WorkspaceAccess.Refund(label);
                    if (refund != null)
                    {
                        total.Add(refund);
                    }
                }
            }

            if (includeStickers)
            {
                foreach (WorkspaceObject sticker in Stickers)
                {
                    GameMoney refund = sticker == null ? null : FoundationApi.WorkspaceAccess.Refund(sticker);
                    if (refund != null)
                    {
                        total.Add(refund);
                    }
                }
            }

            return total;
        }

        public int ExecuteDeconstruct(bool includeBuildings, bool includeContainers, bool includeLabels, bool includeStickers)
        {
            if (!FoundationApi.WorkspaceAccess.IsReady)
            {
                return 0;
            }

            int removedCount = 0;
            var totalRefund = new GameMoney();
            if (includeBuildings)
            {
                foreach (WorkspaceObject element in Buildings)
                {
                    if (element == null || !FoundationApi.WorkspaceAccess.IsInActiveWorkspace(element))
                    {
                        continue;
                    }

                    GameMoney refund = FoundationApi.WorkspaceAccess.Refund(element);
                    if (refund != null && !refund.IsEmpty)
                    {
                        totalRefund.Add(refund);
                    }

                    FoundationApi.WorkspaceAccess.Remove(element);
                    removedCount += Math.Max(1, element.StackSize);
                }
            }

            if (includeContainers)
            {
                removedCount += RemoveDecorations(Containers, totalRefund);
            }

            if (includeLabels)
            {
                removedCount += RemoveDecorations(Labels, totalRefund);
            }

            if (includeStickers)
            {
                removedCount += RemoveDecorations(Stickers, totalRefund);
            }

            if (!totalRefund.IsEmpty)
            {
                FoundationApi.WorkspaceAccess.Grant(totalRefund, countAsIncome: false);
            }

            if (removedCount > 0)
            {
                FoundationApi.UiAccess.PlaySound(GameUiSound.TurnPage);
                if (FoundationApi.View.IsReady)
                {
                    FoundationApi.View.RecalculateCameraBounds();
                }
            }

            return removedCount;
        }

        private static int RemoveDecorations(List<WorkspaceObject> decos, GameMoney totalRefund)
        {
            int count = 0;
            foreach (WorkspaceObject deco in decos)
            {
                if (deco == null || !FoundationApi.WorkspaceAccess.IsInActiveWorkspace(deco))
                {
                    continue;
                }

                GameMoney refund = FoundationApi.WorkspaceAccess.Refund(deco);
                if (refund != null && !refund.IsEmpty)
                {
                    totalRefund.Add(refund);
                }

                FoundationApi.WorkspaceAccess.Remove(deco);
                count++;
            }

            return count;
        }
    }
}
