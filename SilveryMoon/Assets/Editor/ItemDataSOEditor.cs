#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using InventorySystem;

/// <summary>
/// Custom inspector for ItemDataSO:
/// - Shows only the fields relevant to the selected category.
/// - When the category is changed, clears the now-irrelevant fields to avoid confusion.
/// - Exposes worldPrefab so you can link a prefab for pickups/equipping.
/// </summary>
[CustomEditor(typeof(ItemDataSO))]
public class ItemDataSOEditor : Editor
{
    SerializedProperty itemNameProp;
    SerializedProperty iconProp;
    SerializedProperty descriptionProp;
    SerializedProperty categoryProp;
    SerializedProperty stackableProp;
    SerializedProperty maxStackProp;
    SerializedProperty worldPrefabProp;

    SerializedProperty restorativeTypeProp;
    SerializedProperty healValueProp;

    SerializedProperty fuelEfficiencyProp;
    SerializedProperty brightnessValueProp;

    SerializedProperty totemEffectTagProp;

    void OnEnable()
    {
        itemNameProp = serializedObject.FindProperty("itemName");
        iconProp = serializedObject.FindProperty("icon");
        descriptionProp = serializedObject.FindProperty("description");
        categoryProp = serializedObject.FindProperty("category");
        stackableProp = serializedObject.FindProperty("stackable");
        maxStackProp = serializedObject.FindProperty("maxStack");
        worldPrefabProp = serializedObject.FindProperty("worldPrefab");

        restorativeTypeProp = serializedObject.FindProperty("restorativeType");
        healValueProp = serializedObject.FindProperty("healValue");

        fuelEfficiencyProp = serializedObject.FindProperty("fuelEfficiency");
        brightnessValueProp = serializedObject.FindProperty("brightnessValue");

        totemEffectTagProp = serializedObject.FindProperty("totemEffectTag");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Basic fields always visible
        EditorGUILayout.PropertyField(itemNameProp);
        EditorGUILayout.PropertyField(iconProp);
        EditorGUILayout.PropertyField(descriptionProp);

        // World prefab (always useful)
        EditorGUILayout.PropertyField(worldPrefabProp, new GUIContent("World Prefab", "Prefab used for world pickup or equipped world object (optional)."));

        // Category selector — detect changes so we can clear irrelevant data
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(categoryProp);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, "Change Item Category and Clear Irrelevant Fields");
            serializedObject.ApplyModifiedProperties();
            ClearIrrelevantFields((ItemCategory)categoryProp.enumValueIndex);
        }

        // General inventory properties
        EditorGUILayout.PropertyField(stackableProp);
        if (stackableProp.boolValue)
        {
            EditorGUILayout.PropertyField(maxStackProp);
        }

        // Show fields based on category
        var cat = (ItemCategory)categoryProp.enumValueIndex;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Category Details", EditorStyles.boldLabel);

        if (cat == ItemCategory.Restorative)
        {
            EditorGUILayout.PropertyField(restorativeTypeProp, new GUIContent("Restorative Type"));
            EditorGUILayout.PropertyField(healValueProp, new GUIContent("Heal Value"));
        }
        else if (cat == ItemCategory.LightEquipment)
        {
            EditorGUILayout.PropertyField(fuelEfficiencyProp, new GUIContent("Fuel Efficiency"));
            EditorGUILayout.PropertyField(brightnessValueProp, new GUIContent("Brightness Value"));
        }
        else if (cat == ItemCategory.Totem)
        {
            EditorGUILayout.PropertyField(totemEffectTagProp, new GUIContent("Totem Effect Tag"));
        }
        else
        {
            EditorGUILayout.LabelField("No specific fields for this category.");
        }

        serializedObject.ApplyModifiedProperties();
    }

    void ClearIrrelevantFields(ItemCategory newCategory)
    {
        if (serializedObject == null) return;

        if (newCategory != ItemCategory.Restorative)
        {
            restorativeTypeProp.enumValueIndex = (int)RestorativeType.Health;
            healValueProp.intValue = 0;
        }

        if (newCategory != ItemCategory.LightEquipment)
        {
            fuelEfficiencyProp.intValue = 0;
            brightnessValueProp.intValue = 0;
        }

        if (newCategory != ItemCategory.Totem)
        {
            totemEffectTagProp.stringValue = string.Empty;
        }

        if (newCategory == ItemCategory.LightEquipment || newCategory == ItemCategory.Totem)
        {
            stackableProp.boolValue = false;
            maxStackProp.intValue = 1;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }
}
#endif