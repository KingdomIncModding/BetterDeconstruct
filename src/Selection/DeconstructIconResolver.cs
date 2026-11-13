using System.Linq;
using KingdomFoundation.Api;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomBetterDeconstruct
{
    internal static class DeconstructIconResolver
    {
        public static Sprite GetBuildingIcon(WorkspaceObject element) => element?.Icon;
        public static string GetBuildingName(WorkspaceObject element) => element?.Name ?? "Unknown";

        public static Sprite GetContainerIcon()
        {
            return FoundationApi.Presentation.FindSprite("Icons_31");
        }

        public static Sprite GetLabelIcon()
        {
            return FoundationApi.Presentation.FindSprite("Icons_32");
        }

        public static Sprite GetStickerIcon(WorkspaceObject stickerDeco = null)
        {
            return stickerDeco?.Icon ?? FoundationApi.Presentation.FindSprite("Icons2_45");
        }

        public static string GetContainerName()
        {
            string translated = FoundationApi.Presentation.Translate("@deco_rect");
            return string.IsNullOrWhiteSpace(translated) || translated == "@deco_rect" ? "Container" : translated;
        }

        public static string GetLabelName()
        {
            string translated = FoundationApi.Presentation.Translate("@deco_label");
            return string.IsNullOrWhiteSpace(translated) || translated == "@deco_label" ? "Label" : translated;
        }

        public static string GetStickerName()
        {
            string translated = FoundationApi.Presentation.Translate("@deco_sticker");
            return string.IsNullOrWhiteSpace(translated) || translated == "@deco_sticker" ? "Sticker" : translated;
        }
    }
}

