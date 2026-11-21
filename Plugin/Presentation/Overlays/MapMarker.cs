using System;
using System.Collections.Generic;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MapsExtended.Presentation.Overlays
{
    public class MapMarker : MonoBehaviour, ILayerBound, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Dictionary<string, Dictionary<LayerStatus, float>> CategoryImageAlpha = new()
        {
            ["Extract"] = SoftFloor(),
            ["Secret"] = SoftFloor(),
            ["Transit"] = SoftFloor(),
            ["Quest"] = SoftFloor(),
            ["Airdrop"] = SoftFloor()
        };

        private static readonly Dictionary<string, Dictionary<LayerStatus, float>> CategoryLabelAlpha = new()
        {
            ["Extract"] = TopOnly(),
            ["Secret"] = TopOnly(),
            ["Transit"] = TopOnly(),
            ["Quest"] = TopOnly()
        };

        private static readonly Vector2 LabelSizeMultiplier = new(2.5f, 2f);
        private const float MinFont = 9f;
        private const float MaxFont = 13f;
        private const float HoverWidth = 240f;
        private const float HoverFont = 12f;

        public event Action<ILayerBound> OnPositionChanged;

        public string Text { get; private set; }
        public string Description { get; private set; } = "";
        public string Category { get; private set; }
        public MapCanvas ContainingMapView { get; set; }
        public string AssociatedItemId { get; private set; } = "";
        public bool IsDynamic { get; protected set; }
        public string ImagePath => _imagePath;

        internal Image Image { get; set; }
        internal TextMeshProUGUI Label { get; set; }

        private RectTransform RectTransform => gameObject.transform as RectTransform;
        private bool _showInRaid = true;
        private Vector3 _position;
        private float _rotation;
        private Color _color = Color.white;
        private Vector2 _size = new(30f, 30f);
        private string _imagePath;
        private float _baseRotation;
        private bool _outlined;
        private bool _hovered;

        protected Dictionary<LayerStatus, float> ImageAlphaLayerStatus { get; set; } = new()
        {
            [LayerStatus.Hidden] = 0f,
            [LayerStatus.Underneath] = 0f,
            [LayerStatus.OnTop] = 1f,
            [LayerStatus.FullReveal] = 1f
        };

        protected Dictionary<LayerStatus, float> LabelAlphaLayerStatus { get; set; } = new()
        {
            [LayerStatus.Hidden] = 0f,
            [LayerStatus.Underneath] = 0f,
            [LayerStatus.OnTop] = 0f,
            [LayerStatus.FullReveal] = 1f
        };

        public Vector3 Position
        {
            get => _position;
            set => Move(value);
        }

        public float Rotation
        {
            get => _rotation;
            set => SetRotation(value);
        }

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                Tint(Image, value);
                Tint(Label, value);
                GetComponent<FactionMarkerTint>()?.NativeColourChanged(value);
            }
        }

        public Vector2 Size
        {
            get => _size;
            set
            {
                _size = value;
                RectTransform.sizeDelta = _size;
                Image.GetRectTransform().sizeDelta = _size;
                Label.GetRectTransform().sizeDelta = _size * LabelSizeMultiplier;
            }
        }

        public void SetDescription(string description)
        {
            description ??= "";
            if (Description == description)
            {
                return;
            }

            if (_hovered && !string.IsNullOrEmpty(Description))
            {
                HideDescription();
            }

            Description = description;
            if (_hovered && !string.IsNullOrEmpty(Description))
            {
                ShowDescription();
            }
        }

        public static MapMarker Create(GameObject parent, MapMarkerDef def, Vector2 size, float degreesRotation, float scale)
        {
            var marker = Create<MapMarker>(parent, def.Text, def.Category, def.ImagePath, def.Color, def.Position, size,
                def.Pivot, degreesRotation, scale, def.ShowInRaid, def.Sprite);
            marker.AssociatedItemId = def.AssociatedItemId;
            marker.Description = def.Description ?? "";
            return marker;
        }

        public static T Create<T>(
            GameObject parent,
            string text,
            string category,
            string imageRelativePath,
            Color color,
            Vector3 position,
            Vector2 size,
            Vector2 pivot,
            float degreesRotation,
            float scale,
            bool showInRaid = true,
            Sprite sprite = null) where T : MapMarker
        {
            var go = UIUtils.CreateUIGameObject(parent, $"MapMarker {text}");
            var hit = go.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            var rect = go.GetRectTransform();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = scale * Vector2.one;
            rect.localRotation = Quaternion.Euler(0, 0, degreesRotation);
            rect.pivot = pivot;

            var marker = go.AddComponent<T>();
            marker.Text = text;
            marker.Category = category;
            marker.Position = position;
            marker._baseRotation = degreesRotation;
            marker._showInRaid = showInRaid;

            var imageGo = UIUtils.CreateUIGameObject(go, "image");
            imageGo.AddComponent<CanvasRenderer>();
            imageGo.GetRectTransform().sizeDelta = size;
            imageGo.GetRectTransform().pivot = new Vector2(0.5f, 0.5f);
            marker.Image = imageGo.AddComponent<Image>();
            marker.Image.raycastTarget = false;
            marker.Image.type = Image.Type.Simple;
            if (sprite == null)
            {
                marker.SetImagePath(imageRelativePath);
                Settings.MarkerPack.SettingChanged += marker.OnPackChanged;
            }
            else
            {
                marker.Image.sprite = sprite;
            }

            var labelGo = UIUtils.CreateUIGameObject(go, "label");
            labelGo.AddComponent<CanvasRenderer>();
            labelGo.GetRectTransform().anchorMin = new Vector2(0.5f, 0f);
            labelGo.GetRectTransform().anchorMax = new Vector2(0.5f, 0f);
            labelGo.GetRectTransform().pivot = new Vector2(0.5f, 1f);
            labelGo.GetRectTransform().sizeDelta = size * LabelSizeMultiplier;
            marker.Label = labelGo.AddComponent<TextMeshProUGUI>();
            marker.Label.alignment = TextAlignmentOptions.Top;
            marker.Label.enableWordWrapping = true;
            marker.Label.enableAutoSizing = true;
            marker.Label.fontSizeMin = MinFont;
            marker.Label.fontSizeMax = MaxFont;
            marker.Label.text = marker.Text;
            marker.Label.raycastTarget = false;
            marker._outlined = UIUtils.TrySetTMPOutline(marker.Label);
            marker.Color = color;
            marker._size = size;
            marker.Label.gameObject.SetActive(false);
            return marker;
        }

        protected virtual void OnEnable() => EnsureOutline();

        protected virtual void OnDestroy()
        {
            if (Settings.MarkerPack != null)
            {
                Settings.MarkerPack.SettingChanged -= OnPackChanged;
            }

            OnPositionChanged = null;
        }

        public void SetImagePath(string path)
        {
            Image.sprite = TextureUtils.GetOrLoadCachedSprite(path);
            _imagePath = path;
        }

        public void Move(Vector3 newPosition, bool callback = true)
        {
            RectTransform.anchoredPosition = newPosition;
            _position = newPosition;
            if (callback)
            {
                OnPositionChanged?.Invoke(this);
            }
        }

        public void SetRotation(float degreesRotation)
        {
            _rotation = degreesRotation;
            Image.gameObject.GetRectTransform().localRotation = Quaternion.Euler(0, 0, degreesRotation - _baseRotation);
        }

        public void MoveAndRotate(Vector3 newPosition, float rotation, bool callback = true)
        {
            Move(newPosition, callback);
            SetRotation(rotation);
        }

        public void HandleNewLayerStatus(LayerStatus status)
        {
            if (!_showInRaid && GameUtils.IsInRaid())
            {
                gameObject.SetActive(false);
                return;
            }

            if (_hovered)
            {
                status = LayerStatus.FullReveal;
            }

            var imageAlpha = ImageAlphaLayerStatus[status];
            var labelAlpha = LabelAlphaLayerStatus[status];
            if (CategoryImageAlpha.TryGetValue(Category, out var images))
            {
                imageAlpha = images[status];
            }

            if (CategoryLabelAlpha.TryGetValue(Category, out var labels))
            {
                labelAlpha = labels[status];
            }

            Image.color = new Color(Image.color.r, Image.color.g, Image.color.b, imageAlpha);
            Label.color = new Color(Label.color.r, Label.color.g, Label.color.b, labelAlpha);
            Image.gameObject.SetActive(imageAlpha > 0f);
            Label.gameObject.SetActive(labelAlpha > 0f);
            gameObject.SetActive(labelAlpha > 0f || imageAlpha > 0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            EnsureOutline();
            _hovered = true;
            transform.SetAsLastSibling();
            ShowDescription();
            HandleNewLayerStatus(LayerStatus.FullReveal);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            HideDescription();
            OnPositionChanged?.Invoke(this);
        }

        private void OnPackChanged(object sender, EventArgs e)
        {
            if (Image != null && _imagePath != null)
            {
                SetImagePath(_imagePath);
            }
        }

        private void ShowDescription()
        {
            if (Label == null || string.IsNullOrEmpty(Description))
            {
                return;
            }

            var text = string.IsNullOrEmpty(Text) ? Description : Text + "\n" + Description;
            Label.enableAutoSizing = false;
            Label.fontSize = HoverFont;
            Label.text = text;
            var height = Label.GetPreferredValues(text, HoverWidth, 0f).y;
            Label.rectTransform.sizeDelta = new Vector2(HoverWidth, Mathf.Max(height, HoverFont));
        }

        private void HideDescription()
        {
            if (Label == null || string.IsNullOrEmpty(Description))
            {
                return;
            }

            Label.enableAutoSizing = true;
            Label.fontSizeMin = MinFont;
            Label.fontSizeMax = MaxFont;
            Label.text = Text;
            Label.rectTransform.sizeDelta = _size * LabelSizeMultiplier;
        }

        private void EnsureOutline()
        {
            if (_outlined || Label == null)
            {
                return;
            }

            Label.enableAutoSizing = true;
            Label.enableWordWrapping = true;
            Label.fontSizeMin = MinFont;
            Label.fontSizeMax = MaxFont;
            Label.alignment = TextAlignmentOptions.Top;
            Label.text = $"{Label.text}";
            _outlined = UIUtils.TrySetTMPOutline(Label);
        }

        private static void Tint(Graphic graphic, Color colour)
        {
            if (graphic == null)
            {
                return;
            }

            colour.a = graphic.color.a;
            graphic.color = colour;
        }

        private static Dictionary<LayerStatus, float> SoftFloor() => new()
        {
            [LayerStatus.Hidden] = 0.5f,
            [LayerStatus.Underneath] = 0.75f,
            [LayerStatus.OnTop] = 1f,
            [LayerStatus.FullReveal] = 1f
        };

        private static Dictionary<LayerStatus, float> TopOnly() => new()
        {
            [LayerStatus.Hidden] = 0f,
            [LayerStatus.Underneath] = 0f,
            [LayerStatus.OnTop] = 1f,
            [LayerStatus.FullReveal] = 1f
        };
    }
}
