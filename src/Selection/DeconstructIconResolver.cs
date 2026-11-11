using System.Linq;
using Assets.Behaviour.Flow;
using Assets.Source.Flow;
using HarmonyLib;
using KingdomFoundation.Api;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomBetterDeconstruct
{
    internal static class DeconstructIconResolver
    {
        private static readonly AccessTools.FieldRef<WorldFlowElement, Image> WorldIconField =
            AccessTools.FieldRefAccess<WorldFlowElement, Image>("_icon");

        public static Sprite GetBuildingIcon(FlowElement element)
        {
            if (element == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(element.IconName))
            {
                Sprite sprite = FoundationApi.Presentation.FindSprite(element.IconName);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            if (element.World != null)
            {
                Image worldIcon = WorldIconField(element.World);
                if (worldIcon != null && worldIcon.sprite != null)
                {
                    return worldIcon.sprite;
                }
            }

            if (element.Icon != null)
            {
                return element.Icon;
            }

            FlowElement catalogMatch = FoundationApi.Machines.Catalog
                .FirstOrDefault(item => item != null && item.ElementType == element.ElementType);
            if (catalogMatch != null && !string.IsNullOrEmpty(catalogMatch.IconName))
            {
                return FoundationApi.Presentation.FindSprite(catalogMatch.IconName);
            }

            return null;
        }

        public static string GetBuildingName(FlowElement element)
        {
            if (element == null)
            {
                return "Unknown";
            }

            if (!string.IsNullOrEmpty(element.Label))
            {
                string translated = FoundationApi.Presentation.Translate(element.Label);
                if (!string.IsNullOrWhiteSpace(translated))
                {
                    return translated;
                }
            }

            return element.ElementType ?? "Unknown";
        }

        public static Sprite GetContainerIcon()
        {
            return FoundationApi.Presentation.FindSprite("Icons_31");
        }

        public static Sprite GetLabelIcon()
        {
            return FoundationApi.Presentation.FindSprite("Icons_32");
        }

        public static Sprite GetStickerIcon(FlowDeco stickerDeco = null)
        {
            if (stickerDeco?.Sticker != null)
            {
                Sprite stickerSprite = stickerDeco.Sticker.GetSprite();
                if (stickerSprite != null)
                {
                    return stickerSprite;
                }
            }

            return FoundationApi.Presentation.FindSprite("Icons2_45");
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

