using System;
using System.Collections.Generic;
using System.Linq;

namespace HmExcelConfig
{
    public abstract class ExcelConfigCategoryBase
    {
        private static List<ExcelConfigCategoryBase> _bases;
        
        /// <summary>
        /// 获取所有的配置表的Category类,他们其实就是instance访问到的那个实例
        /// </summary>
        /// <returns></returns>
        public static List<ExcelConfigCategoryBase> GetAllExcelConfigCategoryType()
        {
            if (_bases != null) return _bases;
            _bases = new List<ExcelConfigCategoryBase>();
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a =>
                    a.GetTypes().Where(t =>
                        t != typeof(ExcelConfigCategoryBase) && t.BaseType == typeof(ExcelConfigCategoryBase)))
                .ToList();
            for (int i = 0; i < types.Count; i++)
            {
                var type = types[i];
                _bases.Add(Activator.CreateInstance(type) as ExcelConfigCategoryBase);
            }

            return _bases;
        }
        
        /// <summary> </summary>获得变种的数据资源路径 </summary>
        public string VariantDataPath(string variantName)
        {
            if (!haveVariant) return dataPath;
            return dataPath.Replace("[variantName]", variantName);
        }
        
        /// <summary>
        /// 数据资源路径,如:"Assets/Bundles/Config/TestClientConfig.csv",根据导出工具设置的数据输出路径自动填写
        /// 如果有变种的话,路径会是:"Assets/Bundles/Config/variant/[variantName]/Language.bytes"
        /// 请使用VariantDataPath(string variantName)接口获取
        /// </summary>
        public string dataPath;

        /// <summary> 是否有变种 </summary>
        public bool haveVariant;
        
        /// <summary>变种名列表</summary>
        public string[] VariantNames;

        public abstract bool Init(byte[] datas);

        public virtual void BeforeInit()
        {
        }

        public virtual void AfterInit()
        {
        }
    }
}