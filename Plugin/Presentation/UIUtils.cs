using BepInEx.Configuration;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapsExtended.Presentation
{
    public static class UIUtils
    {
        private static readonly Vector2 Center = new(0.5f, 0.5f);
        private static readonly Color32 OutlineBlack = new(0, 0, 0, 255);
        private const float OutlineWidth = 0.15f;

        public static void ResetRectTransform(this GameObject gameObject)
        {
            var rect = gameObject.transform as RectTransform;
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;
            rect.anchoredPosition3D = Vector3.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchorMin = Center;
            rect.anchorMax = Center;
            rect.pivot = Center;
        }

        public static RectTransform GetRectTransform(this GameObject gameObject)
        {
            return gameObject.transform as RectTransform;
        }

        public static RectTransform GetRectTransform(this Component component)
        {
            return component.transform as RectTransform;
        }

        public static Tween TweenColor(this Image image, Color to, float duration)
        {
            return DOTween.To(() => image.color, color => image.color = color, to, duration);
        }

        public static GameObject CreateUIGameObject(GameObject parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = parent.layer;
            child.transform.SetParent(parent.transform);
            child.ResetRectTransform();
            return child;
        }

        public static bool TrySetTMPOutline(TextMeshProUGUI text)
        {
            if (text == null)
            {
                Plugin.Log.LogWarning("TrySetTMPOutline: text cannot be null");
                return false;
            }

            try
            {
                text.outlineColor = OutlineBlack;
                text.outlineWidth = OutlineWidth;
                text.fontStyle = FontStyles.Bold;
                text.ForceMeshUpdate(true, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool BetterIsPressed(this KeyboardShortcut key)
        {
            return Input.GetKey(key.MainKey) && ModifiersHeld(key);
        }

        public static bool BetterIsDown(this KeyboardShortcut key)
        {
            return Input.GetKeyDown(key.MainKey) && ModifiersHeld(key);
        }

        private static bool ModifiersHeld(KeyboardShortcut key)
        {
            foreach (var modifier in key.Modifiers)
            {
                if (!Input.GetKey(modifier))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
