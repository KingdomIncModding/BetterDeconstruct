using System;
using System.Collections.Generic;
using System.Linq;
using KingdomFoundation.Api;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomBetterDeconstruct
{
    internal sealed class DeconstructUiTheme
    {
        public static readonly Color Ink = new Color(0.13f, 0.105f, 0.09f, 1f);
        public static readonly Color Wood = new Color(0.24f, 0.19f, 0.15f, 1f);
        public static readonly Color Gold = new Color(0.66f, 0.51f, 0.29f, 1f);
        public static readonly Color Paper = new Color(0.91f, 0.84f, 0.66f, 1f);
        public static readonly Color MutedPaper = new Color(0.72f, 0.66f, 0.54f, 1f);
        public static readonly Color DangerTint = new Color(1f, 0.68f, 0.61f, 1f);

        private readonly Dictionary<string, Sprite> sprites;

        public DeconstructUiTheme()
        {
            sprites = Resources.FindObjectsOfTypeAll<Sprite>()
                .Where(s => s != null && !string.IsNullOrEmpty(s.name))
                .GroupBy(s => s.name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        }

        public Sprite GetSprite(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            Sprite found = FoundationApi.Presentation.FindSprite(name);
            if (found != null)
            {
                return found;
            }

            return sprites.TryGetValue(name, out Sprite cached) ? cached : null;
        }

        public void ApplySlicedImage(Image image, string spriteName, Color fallback)
        {
            if (!image)
            {
                return;
            }

            Sprite sprite = GetSprite(spriteName);
            image.sprite = sprite;
            image.type = sprite ? Image.Type.Sliced : Image.Type.Simple;
            image.color = sprite ? Color.white : fallback;
        }

        public void StyleButton(Button button, bool accent = false, bool danger = false, bool activeToggle = false)
        {
            if (!button)
            {
                return;
            }

            Image bg = button.GetComponent<Image>();
            ApplySlicedImage(bg, accent || activeToggle ? "Button02" : "Button03", Wood);
            if (bg)
            {
                bg.color = danger
                    ? DangerTint
                    : activeToggle
                        ? new Color(1f, 0.95f, 0.82f, 1f)
                        : Color.white;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.94f, 0.76f, 1f);
            colors.pressedColor = new Color(0.72f, 0.66f, 0.53f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.65f, 0.62f, 0.59f, 0.48f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }
    }
}

