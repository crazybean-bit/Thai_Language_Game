using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ThaiMystery
{
    public static class Theme
    {
        public static readonly Color Paper = new Color32(0xE6, 0xDA, 0xC2, 0xFF);
        public static readonly Color Ink = new Color32(0x2B, 0x22, 0x1E, 0xFF);
        public static readonly Color InkSoft = new Color32(0x6E, 0x5E, 0x52, 0xFF);
        public static readonly Color Unknown = new Color32(0x9A, 0x8C, 0x7E, 0xFF);
        public static readonly Color Confirmed = new Color32(0x2F, 0x7A, 0x45, 0xFF);
        public static readonly Color Header = new Color32(0x0E, 0x0C, 0x0B, 0xFF);
        public static readonly Color Backdrop = new Color32(0x1A, 0x16, 0x14, 0xF0);
        public static readonly Color Highlight = new Color32(0xC9, 0x9A, 0x4B, 0xFF);
    }

    // Small helpers for building uGUI in code, so the prototype needs no prefabs.
    public static class UIFactory
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static Image Panel(string name, Transform parent, Color color, bool raycast = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        // Beige pixel-art frame, 9-sliced so it scales to any size.
        public static Image Frame(string name, Transform parent, string sprite = "ui_frame")
        {
            var img = Panel(name, parent, Color.white);
            img.sprite = SpriteLibrary.GetSliced(sprite, 8);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.34f;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = ThaiFonts.Main;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        public static Button MakeButton(GameObject go, System.Action onClick)
        {
            var btn = go.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() =>
            {
                // Deselect so a later Space/Enter doesn't "submit" this button again.
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                onClick();
            });
            return btn;
        }

        public static TMP_InputField InputField(string name, Transform parent, string placeholder, float size)
        {
            var bg = Panel(name, parent, Color.white);
            bg.gameObject.SetActive(false);

            var area = Rect("Text Area", bg.transform);
            Stretch(area, 14, 14, 6, 6);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = Text("Placeholder", area, placeholder, size, Theme.Unknown, TextAlignmentOptions.MidlineLeft);
            ph.fontStyle = FontStyles.Italic;
            Stretch(ph.rectTransform);
            var txt = Text("Text", area, "", size, Theme.Ink, TextAlignmentOptions.MidlineLeft);
            Stretch(txt.rectTransform);

            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = txt;
            field.placeholder = ph;
            field.fontAsset = ThaiFonts.Main;
            field.pointSize = size;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 40;

            bg.gameObject.SetActive(true);
            return field;
        }
    }
}
