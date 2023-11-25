using System.IO;
using UnityEditor;
using UnityEngine;

namespace HmExcelConfigEditor
{
    public class HmExcelConfigSetting : ScriptableObject
    {
        private const string ConfigPath = "Assets/HmExcelConfig/Editor/HmExcelConfigSetting.asset";
        private static HmExcelConfigSetting _instance;

        public static HmExcelConfigSetting Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (File.Exists(ConfigPath))
                        _instance = AssetDatabase.LoadAssetAtPath<HmExcelConfigSetting>(ConfigPath);
                    else
                    {
                        _instance = ScriptableObject.CreateInstance<HmExcelConfigSetting>();
                        FileInfo fileInfo = new FileInfo(ConfigPath);
                        DirectoryInfo directoryInfo = fileInfo.Directory;
                        if (!Directory.Exists(directoryInfo.FullName))
                        {
                            Directory.CreateDirectory(directoryInfo.FullName);
                        }

                        AssetDatabase.CreateAsset(_instance, ConfigPath);
                    }
                }

                return _instance;
            }
        }

        [Header("Excel表路径")] public string ExcelFilePath = "Excel";
        [Header("Protobuf 类输出路径")]public string CodePath="Assets/ConfigCode";
        [Header("数据输出路径")]public string DataFilePath="Assets/Bundles/Config";
        [Header("代码模版(管理类)路径")] public string codeTemplatePath = "Assets/HmExcelConfig/Editor/ConfigCategory.cs.template";
        
    }
}
