using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using HMExcelConfig;
using UnityEngine;

public class LoadSample : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        InitConfig();
    }
    
    private async void InitConfig()
    {
        var configs = ExcelConfigCategoryBase.GetAllExcelConfigCategoryType();
        for (int i = 0; i < configs.Count; i++)
        {
            string path = configs[i].dataPath;
            //如果有变种就载入其变种表的数据
            if (configs[i].haveVariant)
            {
                path = configs[i].VariantDataPath(configs[i].VariantNames[0]);
            }
            Debug.Log($"这里载入的是表 {path} 的变种表{configs[i].VariantNames[0]}的内容");
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            configs[i].Init(asset.bytes);
            Debug.Log(configs[i].GetType().Name+" ");
            // if (configs[i].GetType().Name.IndexOf("DemoExcelConfigCategory") >= 0)
            // {
            //     DemoExcelConfigCategory c = configs[i] as DemoExcelConfigCategory;
            //     var ty = c.ConfigMap.GetType();
            //     foreach (var key in c.ConfigMap.Keys)
            //     {
            //         Debug.Log($"{key} : {JsonUtility.ToJson(c.ConfigMap[key])}");
            //     }
            //  
            // }
            
        }
      
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
