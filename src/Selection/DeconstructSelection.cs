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

        public bool PruneUnavailable(bool ignoreLockedDecorations)
        {
            bool changed = false;
            changed |= PruneList(Buildings, ignoreLocked: false);
            changed |= PruneList(Containers, ignoreLockedDecorations);
            changed |= PruneList(Labels, ignoreLockedDecorations);
            changed |= PruneList(Stickers, ignoreLockedDecorations);
            return changed;
        }

        private static bool PruneList(List<WorkspaceObject> items, bool ignoreLocked)
        {
            bool changed = false;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                WorkspaceObject item = items[i];
                if (item == null || !FoundationApi.WorkspaceAccess.IsInActiveWorkspace(item) || (ignoreLocked && item.Locked))
                {
                    items.RemoveAt(i);
                    changed = true;
                }
            }
            return changed;
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
                    if (element == null || !FoundationApi.WorkspaceAccess.IsInActiveWorkspace(element))
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
                AddDecorationRefunds(Containers, total);
            }

            if (includeLabels)
            {
                AddDecorationRefunds(Labels, total);
            }

            if (includeStickers)
            {
                AddDecorationRefunds(Stickers, total);
            }

            return total;
        }

        private static void AddDecorationRefunds(List<WorkspaceObject> decos, GameMoney total)
        {
            foreach (WorkspaceObject deco in decos)
            {
                if (deco == null || !FoundationApi.WorkspaceAccess.IsInActiveWorkspace(deco))
                {
                    continue;
                }

                GameMoney refund = FoundationApi.WorkspaceAccess.Refund(deco);
                if (refund != null)
                {
                    total.Add(refund);
                }
            }
        }

        public int ExecuteDeconstruct(bool includeBuildings, bool includeContainers, bool includeLabels, bool includeStickers)
        {
            if (!FoundationApi.WorkspaceAccess.IsReady)
            {
                return 0;
            }

            var liveBuildings = includeBuildings
                ? Buildings.Where(item => item != null && FoundationApi.WorkspaceAccess.IsInActiveWorkspace(item)).ToList()
                : new List<WorkspaceObject>();
            var liveDecos = new List<WorkspaceObject>();
            if (includeContainers)
                liveDecos.AddRange(Containers.Where(item => item != null && FoundationApi.WorkspaceAccess.IsInActiveWorkspace(item)));
            if (includeLabels)
                liveDecos.AddRange(Labels.Where(item => item != null && FoundationApi.WorkspaceAccess.IsInActiveWorkspace(item)));
            if (includeStickers)
                liveDecos.AddRange(Stickers.Where(item => item != null && FoundationApi.WorkspaceAccess.IsInActiveWorkspace(item)));

            if (liveBuildings.Count == 0 && liveDecos.Count == 0)
            {
                return 0;
            }

            // Precompute exact refund and rollback journal before modifying workspace state.
            GameMoney totalRefund = CalculateRefund(includeBuildings, includeContainers, includeLabels, includeStickers);
            var buildingSet = new HashSet<WorkspaceObject>(liveBuildings);
            var workspace = FoundationApi.Kingdom?.ActiveWorkspace;
            var pipeSnapshots = new List<(string OutGuid, int OutIndex, string InGuid, int InIndex, Vector2Int[] Nodes)>();
            if (workspace != null && buildingSet.Count > 0)
            {
                foreach (var pipe in workspace.Pipes.ToArray())
                {
                    if (pipe?.ConnectedOutput?.Parent == null || pipe.ConnectedInput?.Parent == null)
                        continue;
                    if (!buildingSet.Contains(pipe.ConnectedOutput.Parent) && !buildingSet.Contains(pipe.ConnectedInput.Parent))
                        continue;
                    pipeSnapshots.Add((
                        pipe.ConnectedOutput.Parent.Guid,
                        pipe.ConnectedOutput.ParentIndex,
                        pipe.ConnectedInput.Parent.Guid,
                        pipe.ConnectedInput.ParentIndex,
                        pipe.Nodes.ToArray()));
                }
            }

            var buildingSnapshots = new List<(KingdomFoundation.Json.JsonValue Data, GameInventoryItem[][] InputBuffers, GameInventoryItem[][] OutputBuffers)>();
            foreach (var element in liveBuildings)
            {
                var inputBuffers = element.Inputs.Select(p => p.BufferedItems.ToArray()).ToArray();
                var outputBuffers = element.Outputs.Select(p => p.BufferedItems.ToArray()).ToArray();
                buildingSnapshots.Add((FoundationApi.WorkspaceAccess.Serialize(element), inputBuffers, outputBuffers));
            }

            var decoSnapshots = liveDecos.Select(deco => FoundationApi.WorkspaceAccess.Serialize(deco)).ToList();

            var rollback = new Stack<Action>();
            int removedCount = 0;
            try
            {
                for (int i = 0; i < liveBuildings.Count; i++)
                {
                    WorkspaceObject element = liveBuildings[i];
                    var snapshot = buildingSnapshots[i];
                    FoundationApi.WorkspaceAccess.Remove(element);
                    removedCount += Math.Max(1, element.StackSize);
                    rollback.Push(() =>
                    {
                        var restored = FoundationApi.WorkspaceAccess.Deserialize(snapshot.Data);
                        if (restored == null) return;
                        FoundationApi.WorkspaceAccess.PlaceBuilding(restored);
                        for (int p = 0; p < snapshot.InputBuffers.Length && p < restored.Inputs.Count; p++)
                            if (snapshot.InputBuffers[p].Length > 0) restored.Inputs[p].SetBuffer(snapshot.InputBuffers[p]);
                        for (int p = 0; p < snapshot.OutputBuffers.Length && p < restored.Outputs.Count; p++)
                            if (snapshot.OutputBuffers[p].Length > 0) restored.Outputs[p].SetBuffer(snapshot.OutputBuffers[p]);
                    });
                }

                for (int i = 0; i < liveDecos.Count; i++)
                {
                    WorkspaceObject deco = liveDecos[i];
                    var snapshotData = decoSnapshots[i];
                    FoundationApi.WorkspaceAccess.Remove(deco);
                    removedCount++;
                    rollback.Push(() =>
                    {
                        var restored = FoundationApi.WorkspaceAccess.Deserialize(snapshotData);
                        if (restored != null)
                            FoundationApi.WorkspaceAccess.PlaceDecoration(restored);
                    });
                }

                if (!totalRefund.IsEmpty)
                {
                    FoundationApi.WorkspaceAccess.Grant(totalRefund, countAsIncome: false);
                }
            }
            catch
            {
                while (rollback.Count > 0)
                {
                    try { rollback.Pop()(); }
                    catch { }
                }

                if (workspace != null && pipeSnapshots.Count > 0)
                {
                    var byGuid = new Dictionary<string, WorkspaceObject>(StringComparer.Ordinal);
                    foreach (var b in workspace.Buildings)
                    {
                        if (!string.IsNullOrEmpty(b.Guid))
                            byGuid[b.Guid] = b;
                    }

                    foreach (var ps in pipeSnapshots)
                    {
                        try
                        {
                            if (!byGuid.TryGetValue(ps.OutGuid, out var outObj) || !byGuid.TryGetValue(ps.InGuid, out var inObj))
                                continue;
                            if (ps.OutIndex < 0 || ps.OutIndex >= outObj.Outputs.Count || ps.InIndex < 0 || ps.InIndex >= inObj.Inputs.Count)
                                continue;
                            var pipe = FoundationApi.WorkspaceAccess.CreatePipe();
                            pipe.Connect(outObj.Outputs[ps.OutIndex], inObj.Inputs[ps.InIndex]);
                            if (ps.Nodes.Length > 0)
                                pipe.SetNodes(ps.Nodes);
                            FoundationApi.WorkspaceAccess.PlacePipe(pipe);
                        }
                        catch { }
                    }
                }

                throw;
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
    }
}
