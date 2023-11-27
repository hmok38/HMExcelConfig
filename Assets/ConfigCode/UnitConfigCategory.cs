using System;
using System.Collections.Generic;
using HMExcelConfig;
using UnityEngine;

public partial class UnitConfigCategory : ExcelConfigCategoryBase
{
    private static UnitConfigCategory _instance;
    public static UnitConfigCategory Instance => _instance;
    private readonly Dictionary<int, UnitConfig> _configMap = new Dictionary<int, UnitConfig>();
    public Dictionary<int, UnitConfig> ConfigMap => this._configMap;
    public int AllConfigCount => this._configMap.Count;
    
    public UnitConfigCategory()
    {
        this.dataPath = "Assets/Bundles/Config/UnitConfig.bytes";
        this.haveVariant = false;
        this.VariantNames = new string[] {};
        UnitConfigCategory._instance = this;
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
                    List<UnitConfig> configs =
                        ProtoBuf.Serializer.Deserialize<List<UnitConfig>>(ms);
                   
                    for (var i = 0; i < configs.Count; i++)
                    {
                        var config = configs[i];

                        if (_configMap.ContainsKey(config.Id))
                            Debug.LogError($"配置表 UnitConfig 中有相同Id:{config.Id.ToString()}");
                        else
                        {
                            _configMap.Add(config.Id, config);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"解析 UnitConfig 时发生错误:" + e);
                return false;
            }
        }

        this.AfterInit();
        return true;
    }

    public UnitConfig GetConfig(int id)
    {
        if (this._configMap.ContainsKey(id))
        {
            return this._configMap[id];
        }

        return null;
    }

   
}