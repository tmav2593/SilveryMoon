//#if UNITY_EDITOR
//using UnityEngine;
//using UnityEditor;
//using System.IO;

///// <summary>
///// Editor utility to create sample ItemDataSO assets for the provided tables.
///// Run: Tools > Create Sample Inventory Items
///// </summary>
//public static class CreateSampleItems
//{
//    [MenuItem("Tools/Create Sample Inventory Items")]
//    public static void CreateItems()
//    {
//        string folder = "Assets/Items";
//        if (!AssetDatabase.IsValidFolder(folder))
//        {
//            AssetDatabase.CreateFolder("Assets", "Items");
//        }

//        CreateRestorative("First Aid Kit", "A large medical kit that greatly restores health.", RestorativeType.Health, 100);
//        CreateRestorative("Bandage", "An average bandage that moderately restores health.", RestorativeType.Health, 50);
//        CreateRestorative("Pill", "A small dose of painkillers that slightly restores health.", RestorativeType.Health, 25);

//        CreateRestorative("Jerky", "A large chunk of beef jerky.", RestorativeType.Hunger, 100);
//        CreateRestorative("Bread", "A decent-sized piece of bread.", RestorativeType.Hunger, 50);
//        CreateRestorative("Cheese", "A small piece of cheese.", RestorativeType.Hunger, 25);

//        CreateRestorative("Oil Flask", "A flask of lantern oil.", RestorativeType.Light, 100);

//        CreateLightEquipment("Lantern", "A plain and simple lantern.", 50, 50);
//        CreateLightEquipment("Lantern of Enlightenment", "An opulent lantern that reveals what should be left unseen.", 100, 100);

//        AssetDatabase.SaveAssets();
//        AssetDatabase.Refresh();
//        Debug.Log("Sample items created in Assets/Items/");
//    }

//    static void CreateRestorative(string name, string desc, RestorativeType type, int heal)
//    {
//        var item = ScriptableObject.CreateInstance<ItemDataSO>();
//        item.itemName = name;
//        item.description = desc;
//        item.category = ItemCategory.Restorative;
//        item.restorativeType = type;
//        item.healValue = heal;
//        item.stackable = true;
//        item.maxStack = 10;
//        string path = $"Assets/Items/{SanitizeName(name)}.asset";
//        AssetDatabase.CreateAsset(item, path);
//    }

//    static void CreateLightEquipment(string name, string desc, int efficiency, int brightness)
//    {
//        var item = ScriptableObject.CreateInstance<ItemDataSO>();
//        item.itemName = name;
//        item.description = desc;
//        item.category = ItemCategory.LightEquipment;
//        item.fuelEfficiency = efficiency;
//        item.brightnessValue = brightness;
//        item.stackable = false;
//        item.maxStack = 1;
//        string path = $"Assets/Items/{SanitizeName(name)}.asset";
//        AssetDatabase.CreateAsset(item, path);
//    }

//    static string SanitizeName(string name) => name.Replace(" ", "_").Replace("/", "_");
//}
//#endif