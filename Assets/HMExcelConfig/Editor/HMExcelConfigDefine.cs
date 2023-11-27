namespace HMExcelConfigEditor
{
    public class HMExcelConfigDefine
    {
        public const string ConfigCategoryTemplate =
            @"using System;
using System.Collections.Generic;
using HMExcelConfig;
using UnityEngine;

public partial class [classname]Category : ExcelConfigCategoryBase
{
    private static [classname]Category _instance;
    public static [classname]Category Instance => _instance;
    private readonly Dictionary<[idtype], [classname]> _configMap = new Dictionary<[idtype], [classname]>();
    public Dictionary<[idtype], [classname]> ConfigMap => this._configMap;
    public int AllConfigCount => this._configMap.Count;
    
    public [classname]Category()
    {
        this.dataPath = ""[dataUrl]"";
        this.haveVariant = [haveVariant];
        this.VariantNames = new string[] {[VariantNames]};
        [classname]Category._instance = this;
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
                    List<[classname]> configs =
                        ProtoBuf.Serializer.Deserialize<List<[classname]>>(ms);
                   
                    for (var i = 0; i < configs.Count; i++)
                    {
                        var config = configs[i];

                        if (_configMap.ContainsKey(config.Id))
                            Debug.LogError($""配置表 [classname] 中有相同Id:{config.Id.ToString()}"");
                        else
                        {
                            _configMap.Add(config.Id, config);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($""解析 [classname] 时发生错误:"" + e);
                return false;
            }
        }

        this.AfterInit();
        return true;
    }

    public [classname] GetConfig([idtype] id)
    {
        if (this._configMap.ContainsKey(id))
        {
            return this._configMap[id];
        }

        return null;
    }

   
}";
    }
}