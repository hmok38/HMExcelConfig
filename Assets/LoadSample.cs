using System.Collections;
using System.Collections.Generic;
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

            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            configs[i].Init(asset.bytes);
            Debug.Log(configs[i].GetType().Name+" ");
        }
        Debug.Log("Language DataCount= "+LanguageCategory.Instance.ConfigMap.Keys.Count);
        Debug.Log("UnitConfig DataCount= "+UnitConfigCategory.Instance.ConfigMap.Keys.Count);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
