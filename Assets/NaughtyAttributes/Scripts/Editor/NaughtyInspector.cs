using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(UnityEngine.Object), true)]
    public class NaughtyInspector : UnityEditor.Editor
    {
        [System.Serializable]
        private class SerializedData
        {
            public List<SerializedProperty> _serializedProperties = new List<SerializedProperty>();
            public IEnumerable<FieldInfo> _nonSerializedFields;
            public IEnumerable<PropertyInfo> _nativeProperties;
            public IEnumerable<MethodInfo> _methods;
            public bool _anyMethods, _anyNativeProperties, _anyNaughtyAttribute;
        }

        private static readonly Dictionary<object, SerializedData> data = new Dictionary<object, SerializedData>();

        private List<SerializedProperty> _serializedProperties = new List<SerializedProperty>();
        private IEnumerable<FieldInfo> _nonSerializedFields;
        private IEnumerable<PropertyInfo> _nativeProperties;
        private IEnumerable<MethodInfo> _methods;
        private bool _anyMethods, _anyNativeProperties, _anyNaughtyAttribute;

        protected virtual void OnEnable()
        {
            SerializedData entry;
            
            if (!data.ContainsKey(target))
            {
                entry = new SerializedData();
                data.Add(target, entry);
                
                entry._nonSerializedFields = ReflectionUtility.GetAllFields(
                    target, f => f.GetCustomAttributes(typeof(ShowNonSerializedFieldAttribute), true).Length > 0).ToList();
                entry._nativeProperties = ReflectionUtility.GetAllProperties(
                    target, p => p.GetCustomAttributes(typeof(ShowNativePropertyAttribute), true).Length > 0).ToList();
                entry._methods = ReflectionUtility.GetAllMethods(
                    target, m => m.GetCustomAttributes(typeof(ButtonAttribute), true).Length > 0).ToList();
                GetSerializedProperties(ref _serializedProperties);
                entry._serializedProperties = _serializedProperties;
                entry._anyNaughtyAttribute = _serializedProperties.Any(p => PropertyUtility.GetAttribute<INaughtyAttribute>(p) != null);
                
                entry._anyNativeProperties = entry._nativeProperties.Any();
                entry._anyMethods = entry._methods.Any();
            }
            
            
            entry = data[target];
            _serializedProperties = entry._serializedProperties;
            _nonSerializedFields = entry._nonSerializedFields;
            _nativeProperties = entry._nativeProperties;
            _anyNativeProperties = entry._anyNativeProperties;
            _methods = entry._methods;
            _anyMethods = entry._anyMethods;
            _anyNaughtyAttribute = entry._anyNaughtyAttribute;

        }

        protected virtual void OnDisable()
        {
            ReorderableListPropertyDrawer.Instance.ClearCache();
        }

        public override void OnInspectorGUI()
        {
            GetSerializedProperties(ref _serializedProperties);

            if (!_anyNaughtyAttribute)
            {
                DrawDefaultInspector();
            }
            else
            {
                DrawSerializedProperties();
            }

            DrawNonSerializedFields();
            DrawNativeProperties();
            DrawButtons();
        }

        protected void GetSerializedProperties(ref List<SerializedProperty> outSerializedProperties)
        {
            outSerializedProperties.Clear();
            using (var iterator = serializedObject.GetIterator())
            {
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        outSerializedProperties.Add(serializedObject.FindProperty(iterator.name));
                    } while (iterator.NextVisible(false));
                }
            }
        }

        protected void DrawSerializedProperties()
        {
            serializedObject.Update();

            // Draw non-grouped serialized properties
            foreach (var property in GetNonGroupedProperties(_serializedProperties))
            {
                if (property.name.Equals("m_Script", System.StringComparison.Ordinal))
                {
                    GUI.enabled = false;
                    EditorGUILayout.PropertyField(property);
                    GUI.enabled = true;
                }
                else
                {
                    NaughtyEditorGUI.PropertyField_Layout(property, true);
                }
            }

            // Draw grouped serialized properties
            foreach (var group in GetGroupedProperties(_serializedProperties))
            {
                IEnumerable<SerializedProperty> visibleProperties = group.Where(p => PropertyUtility.IsVisible(p));
                if (!visibleProperties.Any())
                {
                    continue;
                }

                NaughtyEditorGUI.BeginBoxGroup_Layout(group.Key);
                foreach (var property in visibleProperties)
                {
                    NaughtyEditorGUI.PropertyField_Layout(property, true);
                }

                NaughtyEditorGUI.EndBoxGroup_Layout();
            }

            serializedObject.ApplyModifiedProperties();
        }

        protected void DrawNonSerializedFields()
        {
            if (_nonSerializedFields.Any())
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Non-Serialized Fields", GetHeaderGUIStyle());
                NaughtyEditorGUI.HorizontalLine(
                    EditorGUILayout.GetControlRect(false), HorizontalLineAttribute.DefaultHeight, HorizontalLineAttribute.DefaultColor.GetColor());

                foreach (var field in _nonSerializedFields)
                {
                    NaughtyEditorGUI.NonSerializedField_Layout(serializedObject.targetObject, field);
                }
            }
        }

        protected void DrawNativeProperties()
        {
            if (_anyNativeProperties)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Native Properties", GetHeaderGUIStyle());
                NaughtyEditorGUI.HorizontalLine(
                    EditorGUILayout.GetControlRect(false), HorizontalLineAttribute.DefaultHeight, HorizontalLineAttribute.DefaultColor.GetColor());

                foreach (var property in _nativeProperties)
                {
                    NaughtyEditorGUI.NativeProperty_Layout(serializedObject.targetObject, property);
                }
            }
        }

        protected void DrawButtons()
        {
            if (_anyMethods)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Buttons", GetHeaderGUIStyle());
                NaughtyEditorGUI.HorizontalLine(
                    EditorGUILayout.GetControlRect(false), HorizontalLineAttribute.DefaultHeight, HorizontalLineAttribute.DefaultColor.GetColor());

                foreach (var method in _methods)
                {
                    NaughtyEditorGUI.Button(serializedObject.targetObject, method);
                }
            }
        }

        private static IEnumerable<SerializedProperty> GetNonGroupedProperties(IEnumerable<SerializedProperty> properties)
        {
            return properties.Where(p => PropertyUtility.GetAttribute<BoxGroupAttribute>(p) == null);
        }

        private static IEnumerable<IGrouping<string, SerializedProperty>> GetGroupedProperties(IEnumerable<SerializedProperty> properties)
        {
            return properties
                .Where(p => PropertyUtility.GetAttribute<BoxGroupAttribute>(p) != null)
                .GroupBy(p => PropertyUtility.GetAttribute<BoxGroupAttribute>(p).Name);
        }

        private static GUIStyle GetHeaderGUIStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.UpperCenter;

            return style;
        }
    }
}