using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HMExcelConfigEditor
{
    public class HMExcelConfigSetting : ScriptableObject
    {
        public const string ConfigPath = "Assets/HMExcelConfigSetting/HMExcelConfigSetting.asset";
        public const string CodeTemplatePath = "Assets/HMExcelConfigSetting/ConfigCategory.cs.template";
        private static HMExcelConfigSetting _instance;

        public static HMExcelConfigSetting Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (File.Exists(ConfigPath))
                        _instance = AssetDatabase.LoadAssetAtPath<HMExcelConfigSetting>(ConfigPath);
                    else
                    {
                        _instance = ScriptableObject.CreateInstance<HMExcelConfigSetting>();
                        FileInfo fileInfo = new FileInfo(ConfigPath);
                        DirectoryInfo directoryInfo = fileInfo.Directory;
                        if (!Directory.Exists(directoryInfo.FullName))
                        {
                            Directory.CreateDirectory(directoryInfo.FullName);
                        }

                        AssetDatabase.CreateAsset(_instance, ConfigPath);
                    }
                }

           
                if (!File.Exists(HMExcelConfigSetting.CodeTemplatePath))
                {
                    FileInfo fileInfo = new FileInfo(HMExcelConfigSetting.CodeTemplatePath);
                    DirectoryInfo directoryInfo = fileInfo.Directory;
                    if (!Directory.Exists(directoryInfo.FullName))
                    {
                        Directory.CreateDirectory(directoryInfo.FullName);
                    }
                    using (var sm=new FileStream(HMExcelConfigSetting.CodeTemplatePath,FileMode.OpenOrCreate))
                    {
                        var byts = System.Text.Encoding.UTF8.GetBytes(HMExcelConfigDefine.ConfigCategoryTemplate);
                        sm.Write(byts,0,byts.Length);
                    }
                }
                

                return _instance;
            }
        }

        [Header("Excel表路径")] public string ExcelFilePath = "Excel";
        [Header("Protobuf 类输出路径")]public string CodePath="Assets/ConfigCode";
        [Header("数据输出路径")]public string DataFilePath="Assets/Bundles/Config";
        
    }
}
