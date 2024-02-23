using HM;
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
        //Debug.Log(typeof(ProtoBuf.Serializer).Assembly.FullName);
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
            var asset = await HMAddressableManager.LoadAsync<TextAsset>(path);

            // UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            configs[i].Init(asset.bytes);
            Debug.Log(configs[i].GetType().Name + " ");
            // if (configs[i].GetType().Name.IndexOf("DemoExcelConfigCategory") >= 0)
            // {
            //     DemoExcelConfigCategory c = configs[i] as DemoExcelConfigCategory;
            //     var ty = c.ConfigMap.GetType();
            //     foreach (var key in c.ConfigMap.Keys)
            //     {
            //         Debug.Log($"{key} : {Newtonsoft.Json.JsonConvert.SerializeObject(c.ConfigMap[key])}");
            //     }
            // }
        }
    }

    // Update is called once per frame
    void Update()
    {
    }
}