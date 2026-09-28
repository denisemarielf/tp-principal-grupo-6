using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.UI
{
    /// <summary>
    /// Draws a Team/Player count range as a slider plus two bound Min/Max fields.
    /// Max is intentionally left uncapped: legacy/session-less configs can exceed the
    /// recommended max and must stay valid. Only Min is capped on edit.
    /// </summary>
    [CustomPropertyDrawer(typeof(TeamCount))]
    [CustomPropertyDrawer(typeof(PlayerCount))]
    class MatchmakerCountRangeDrawer : PropertyDrawer
    {
        const string k_MinFieldName = "Min";
        const string k_MaxFieldName = "Max";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var minProp = property.FindPropertyRelative(k_MinFieldName);
            var maxProp = property.FindPropertyRelative(k_MaxFieldName);

            var container = new VisualElement();

            var slider = new MinMaxSlider
            {
                label = "Range",
                lowLimit = 0,
                highLimit = MatchMakerQueueInspectorConfig.RecommendedMaxCount
            };

            var minField = new IntegerField("Min")
            {
                tooltip = "Minimum value of the range.",
                isDelayed = true
            };
            var maxField = new IntegerField("Max")
            {
                tooltip = "Maximum value of the range.",
                isDelayed = true
            };
            minField.BindProperty(minProp);
            maxField.BindProperty(maxProp);

            var warning = new HelpBox(
                $"This value exceeds the maximum of {MatchMakerQueueInspectorConfig.RecommendedMaxCount}. It is kept for backward compatibility; editing this field will cap it to {MatchMakerQueueInspectorConfig.RecommendedMaxCount}.",
                HelpBoxMessageType.Warning);

            container.Add(slider);
            container.Add(minField);
            container.Add(maxField);
            container.Add(warning);

            // Max is intentionally left uncapped: legacy/session-less configs can exceed the
            // recommended max and must stay valid. Only Min is capped on edit.
            minField.RegisterValueChangedCallback(evt => CapInput(minField, evt.newValue));

            // The slider can't bind to two int properties, so route its edits through the fields.
            slider.RegisterValueChangedCallback(evt =>
            {
                minField.value = ClampInput(Mathf.RoundToInt(evt.newValue.x));
                maxField.value = ClampInput(Mathf.RoundToInt(evt.newValue.y));
            });

            void RefreshSlider()
            {
                var min = minProp.intValue;
                var max = maxProp.intValue;
                // A single range can't represent mixed values across a multi-object selection.
                slider.SetEnabled(!minProp.hasMultipleDifferentValues && !maxProp.hasMultipleDifferentValues);
                slider.SetValueWithoutNotify(new Vector2(min, max));
                warning.style.display = ExceedsRecommended(min, max) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // Resync slider + warning on any property change (typing, Undo/Redo).
            container.TrackPropertyValue(minProp, _ => RefreshSlider());
            container.TrackPropertyValue(maxProp, _ => RefreshSlider());
            RefreshSlider();

            return container;
        }

        internal static void CapInput(IntegerField field, int value)
        {
            var capped = Mathf.Clamp(value, 0, MatchMakerQueueInspectorConfig.RecommendedMaxCount);
            if (capped != value)
            {
                field.value = capped;
            }
        }

        // Clamps new user input to the valid range [0, RecommendedMaxCount].
        internal static int ClampInput(int value) =>
            Mathf.Clamp(value, 0, MatchMakerQueueInspectorConfig.RecommendedMaxCount);

        internal static bool ExceedsRecommended(int min, int max) =>
            min > MatchMakerQueueInspectorConfig.RecommendedMaxCount ||
            max > MatchMakerQueueInspectorConfig.RecommendedMaxCount;
    }
}
