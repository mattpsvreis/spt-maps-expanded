using System;
using System.Collections.Generic;
using EFT.UI;
using MapsExtended.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MapsExtended.Presentation.Controls
{
    public class LevelSelectSlider : MonoBehaviour
    {
        private const float LabelSize = 15f;
        private static readonly Vector2 LabelOffset = new(10f, 0f);

        public event Action<int> OnLevelSelectedBySlider;

        public RectTransform RectTransform => transform as RectTransform;

        private int _level = int.MinValue;
        private TextMeshProUGUI _label;
        private Scrollbar _bar;
        private readonly List<int> _floors = new();
        private bool _outlined;

        public int SelectedLevel
        {
            get => _level;
            set
            {
                if (!_floors.Contains(value))
                {
                    return;
                }

                var position = _floors.Count == 1
                    ? 0.5f
                    : _floors.IndexOf(value) / (_floors.Count - 1f);
                _bar.SetValueWithoutNotify(position);
                _label.text = $"Level {value}";
                _level = value;
            }
        }

        public static LevelSelectSlider Create(GameObject prefab, Transform parent)
        {
            var go = Instantiate(prefab, parent, true);
            go.name = "LevelSelectScrollbar";
            go.transform.localScale = Vector3.one;
            Destroy(go.GetComponent<MapZoomer>());
            return go.AddComponent<LevelSelectSlider>();
        }

        private void Awake()
        {
            var handle = transform.Find("Scrollbar/Sliding Area/Handle").gameObject;
            var labelObject = UIUtils.CreateUIGameObject(handle, "SlidingLayerText");
            _label = labelObject.AddComponent<TextMeshProUGUI>();
            _label.fontSize = LabelSize;
            _label.alignment = TextAlignmentOptions.Left;

            var labelRect = _label.GetRectTransform();
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            labelRect.anchoredPosition = LabelOffset;
            _outlined = UIUtils.TrySetTMPOutline(_label);

            _bar = transform.Find("Scrollbar").GetComponent<Scrollbar>();
            _bar.direction = Scrollbar.Direction.BottomToTop;
            _bar.onValueChanged.AddListener(BarMoved);
            _bar.onValueChanged.SetPersistentListenerState(0, UnityEventCallState.Off);

            WireStep(transform.Find("Plus").GetComponent<Button>(), 1);
            WireStep(transform.Find("Minus").GetComponent<Button>(), -1);
            gameObject.SetActive(false);
        }

        private void WireStep(Button button, int delta)
        {
            button.onClick.AddListener(() => ChangeLevelBy(delta));
            button.onClick.SetPersistentListenerState(0, UnityEventCallState.Off);
        }

        private void OnEnable()
        {
            if (_outlined || _label == null)
            {
                return;
            }

            _outlined = UIUtils.TrySetTMPOutline(_label);
            _label.text = _label.text;
        }

        private void OnDestroy()
        {
            OnLevelSelectedBySlider = null;
        }

        private void BarMoved(float value)
        {
            if (_floors.Count == 0)
            {
                return;
            }

            if (_floors.Count == 1)
            {
                _bar.SetValueWithoutNotify(0.5f);
                return;
            }

            var index = Mathf.Clamp(Mathf.RoundToInt(value * (_floors.Count - 1)), 0, _floors.Count - 1);
            var floor = _floors[index];
            if (_level != int.MinValue && _level != floor)
            {
                OnLevelSelectedBySlider?.Invoke(floor);
            }
        }

        public void ChangeLevelBy(int delta)
        {
            var index = _floors.IndexOf(_level) + delta;
            if (index < 0 || index >= _floors.Count)
            {
                return;
            }

            OnLevelSelectedBySlider?.Invoke(_floors[index]);
        }

        public void OnLoadMap(MapDef mapDef, int initialLevel)
        {
            _floors.Clear();
            _level = int.MinValue;

            var seen = new HashSet<int>();
            foreach (var layer in mapDef.Layers.Values)
            {
                seen.Add(layer.Level);
            }

            _floors.AddRange(seen);
            _floors.Sort();
            _bar.numberOfSteps = _floors.Count;
            SelectedLevel = initialLevel;
            gameObject.SetActive(_floors.Count > 1);
        }
    }
}
