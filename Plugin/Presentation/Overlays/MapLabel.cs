using System.Collections.Generic;
using MapsExtended.Core;
using TMPro;
using UnityEngine;

namespace MapsExtended.Presentation.Overlays
{
    public class MapLabel : MonoBehaviour, ILayerBound
    {
        private TextMeshProUGUI _label;
        private Vector3 _position;
        private Color _color = Color.white;
        private bool _outlineApplied;

        private string Text { get; set; }
        public string Category { get; protected set; }

        public RectTransform RectTransform => transform as RectTransform;

        public Vector3 Position
        {
            get => _position;
            set
            {
                gameObject.GetRectTransform().anchoredPosition = value;
                _position = value;
            }
        }

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                _label.color = value;
            }
        }

        public Dictionary<LayerStatus, float> LabelAlphaLayerStatus { get; set; } = new()
        {
            { LayerStatus.Hidden, 0f },
            { LayerStatus.Underneath, 0f },
            { LayerStatus.OnTop, 1f },
            { LayerStatus.FullReveal, 1f },
        };

        public static MapLabel Create(GameObject parent, MapLabelDef def, float degreesRotation, float scale)
        {
            var go = UIUtils.CreateUIGameObject(parent, $"MapLabel {def.Text}");
            var rect = go.GetRectTransform();
            rect.anchoredPosition = def.Position;
            rect.localScale = scale * Vector2.one;
            rect.localRotation = Quaternion.Euler(0f, 0f, degreesRotation - def.DegreesRotation);

            var label = go.AddComponent<MapLabel>();
            label.Text = def.Text;
            label.Category = def.Category;
            label.Position = def.Position;

            label._label = go.AddComponent<TextMeshProUGUI>();
            label.Color = def.Color;
            label._label.autoSizeTextContainer = true;
            label._label.fontSize = def.FontSize;
            label._label.alignment = TextAlignmentOptions.Center;
            label._label.text = label.Text;
            label._outlineApplied = UIUtils.TrySetTMPOutline(label._label);
            return label;
        }

        private void OnEnable()
        {
            if (_outlineApplied || _label == null)
            {
                return;
            }

            _outlineApplied = UIUtils.TrySetTMPOutline(_label);
            _label.text = _label.text;
        }

        public void HandleNewLayerStatus(LayerStatus status)
        {
            var color = _label.color;
            color.a = LabelAlphaLayerStatus[status];
            _label.color = color;
        }
    }
}
