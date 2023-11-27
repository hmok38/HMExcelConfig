using System;
using System.Collections.Generic;
using HMExcelConfig;
using UnityEngine;

public partial class LanguageCategory : ExcelConfigCategoryBase
{
    private static LanguageCategory _instance;
    public static LanguageCategory Instance => _instance;
    private readonly Dictionary<string, Language> _configMap = new Dictionary<string, Language>();
    public Dictionary<string, Language> ConfigMap => this._configMap;
    public int AllConfigCount => this._configMap.Count;
    
    public LanguageCategory()
    {
        this.dataPath = "Assets/Bundles/Config/variant/[variantName]/Language.bytes";
        this.haveVariant = true;
        this.VariantNames = new string[] {"ChineseTraditional","Thai"};
        LanguageCategory._instance = this;
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
                    List<Language> configs =
                        ProtoBuf.Serializer.Deserialize<List<Language>>(ms);
                   
                    for (var i = 0; i < configs.Count; i++)
                    {
                        var config = configs[i];

                        if (_configMap.ContainsKey(config.Id))
                            Debug.LogError($"配置表 Language 中有相同Id:{config.Id.ToString()}");
                        else
                        {
                            _configMap.Add(config.Id, config);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"解析 Language 时发生错误:" + e);
                return false;
            }
        }

        this.AfterInit();
        return true;
    }

    public Language GetConfig(string id)
    {
        if (this._configMap.ContainsKey(id))
        {
            return this._configMap[id];
        }

        return null;
    }

   
}