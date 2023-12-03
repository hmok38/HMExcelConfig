using System;
using System.Collections.Generic;
using HMExcelConfig;
using UnityEngine;

public partial class DemoExcelConfigCategory : ExcelConfigCategoryBase
{
    private static DemoExcelConfigCategory _instance;
    public static DemoExcelConfigCategory Instance => _instance;
    private readonly Dictionary<int, DemoExcelConfig> _configMap = new Dictionary<int, DemoExcelConfig>();
    public Dictionary<int, DemoExcelConfig> ConfigMap => this._configMap;
    public int AllConfigCount => this._configMap.Count;
    
    public DemoExcelConfigCategory()
    {
        this.dataPath = "Assets/Bundles/Config/variant/[variantName]/DemoExcelConfig.bytes";
        this.haveVariant = true;
        this.VariantNames = new string[] {"Demo1","Demo2"};
        DemoExcelConfigCategory._instance = this;
    }
        

    public override bool Init(byte[] datas)
    {
        this.BeforeInit();
        _configMap.Clear();
        if (datas.Length > 0)
        {
            try
            {
                using (var ms = new System.IO.MemoryStream(datas))
                {
                    ms.Position = 0;
                    List<DemoExcelConfig> configs =
                        ProtoBuf.Serializer.Deserialize<List<DemoExcelConfig>>(ms);
                   
                    for (var i = 0; i < configs.Count; i++)
                    {
                        var config = configs[i];

                        if (_configMap.ContainsKey(config.Id))
                            Debug.LogError($"配置表 DemoExcelConfig 中有相同Id:{config.Id.ToString()}");
                        else
                        {
                            _configMap.Add(config.Id, config);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"解析 DemoExcelConfig 时发生错误:" + e);
                return false;
            }
        }

        this.AfterInit();
        return true;
    }

    public DemoExcelConfig GetConfig(int id)
    {
        if (this._configMap.ContainsKey(id))
        {
            return this._configMap[id];
        }

        return null;
    }

   
}