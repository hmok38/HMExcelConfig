using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HMExcelConfigEditor
{
    public class HMExcelConfigEditor : UnityEditor.EditorWindow
    {
        [MenuItem("HMExcelConfig/OpenConfig")]
        public static void OpenConfig()
        {
            var window = GetWindow<HMExcelConfigEditor>();
            window.titleContent = new GUIContent("HMExcelConfig工具");
        }

        [MenuItem("HMExcelConfig/导出excel到类演示")]
        public static void Test()
        {
            Debug.Log("可以根据需要写接口,将Excel导出为类,然后保存到SO或者预制体中,记得要保存修改  EditorUtility.SetDirty()");
            var list = ExcelHelper.ExportExcelToClass<TestClass>("Excel\\PhaseTaskConig.xlsx");
        }

        public class TestClass
        {
            public string Id;
            public string CycleType;
            public int phaseId;

            /// <summary>
            /// 这个是excel表中没有的字段,不会报错,会被赋值为默认值
            /// </summary>
            public int phaseId2;

            /// <summary>
            /// 这个是excel表中没有的字段,不会报错,会被赋值为默认值
            /// </summary>
            public string phaseId3;
        }

        private async void OnGUI()
        {
            var configSetting = HMExcelConfigSetting.Instance;
            if (configSetting == null)
            {
                GUILayout.Label($"编译时发生错误,请检查在 {HMExcelConfigSetting.ConfigPath} 是否存在配置表,且配置表正常");
                return;
            }

            GUILayout.Space(30);

            var excelFilePathStr = EditorGUILayout.TextField("Excel表路径:", configSetting.ExcelFilePath);
            if (GUILayout.Button($"打开目录", GUILayout.Width(100)))
            {
                if (Directory.Exists(configSetting.ExcelFilePath))
                {
                    UnityEditor.EditorUtility.RevealInFinder(configSetting.ExcelFilePath);
                }
            }

            GUILayout.Space(20);
            var codePathStr = EditorGUILayout.TextField("Protobuf 类输出路径:", configSetting.CodePath);


            if (GUILayout.Button($"选中目录", GUILayout.Width(100)))
            {
                if (AssetDatabase.IsValidFolder(configSetting.CodePath))
                {
                    var obj =
                        UnityEditor.AssetDatabase.LoadAssetAtPath<DefaultAsset>(configSetting.CodePath);
                    if (obj == null)
                    {
                        Debug.Log("目录不在工程内");
                        return;
                    }

                    PingFolder(obj, configSetting.CodePath);
                    //UnityEditor.EditorGUIUtility.PingObject(obj);
                    EditorUtility.FocusProjectWindow();
                }
                else
                {
                    Debug.Log($"路径不是合法的目录");
                }
            }

            GUILayout.Space(20);
            var dataFilePathStr = EditorGUILayout.TextField("数据输出路径:", configSetting.DataFilePath);
            if (GUILayout.Button($"选中目录", GUILayout.Width(100)))
            {
                if (AssetDatabase.IsValidFolder(configSetting.DataFilePath))
                {
                    var obj =
                        UnityEditor.AssetDatabase.LoadAssetAtPath<DefaultAsset>(configSetting.DataFilePath);
                    if (obj == null)
                    {
                        Debug.Log("目录不在工程内");
                        return;
                    }

                    PingFolder(obj, configSetting.DataFilePath);
                    //UnityEditor.EditorGUIUtility.PingObject(obj);
                    EditorUtility.FocusProjectWindow();
                }
                else
                {
                    Debug.Log($"路径不是合法的目录");
                }
            }

            GUILayout.Space(30);


            if (!excelFilePathStr.Equals(configSetting.ExcelFilePath))
            {
                configSetting.ExcelFilePath = CheckFolderPath(excelFilePathStr);
                UnityEditor.EditorUtility.SetDirty(configSetting);
            }

            if (!codePathStr.Equals(configSetting.CodePath))
            {
                configSetting.CodePath = CheckFolderPath(codePathStr);
                UnityEditor.EditorUtility.SetDirty(configSetting);
            }

            if (!dataFilePathStr.Equals(configSetting.DataFilePath))
            {
                configSetting.DataFilePath = CheckFolderPath(dataFilePathStr);
                UnityEditor.EditorUtility.SetDirty(configSetting);
            }

            if (GUILayout.Button("生成代码和数据", GUILayout.Width(250), GUILayout.Height(60)))
            {
                UnityEditor.EditorUtility.DisplayProgressBar("HMExcelConfigEditor正在生成Code", "正在生成Code,请稍候", 0f);
                string result = "";
                try
                {
                    result = await ExcelHelper.ExportAllExcelToCode(configSetting.ExcelFilePath, configSetting.CodePath,
                        HMExcelConfigSetting.CodeTemplatePath, configSetting.DataFilePath,
                        (progress, str) =>
                        {
                            UnityEditor.EditorUtility.DisplayProgressBar("HMExcelConfigEditor正在生成Code", str, progress);
                        });
                }
                catch (Exception e)
                {
                    result = e.ToString();
                }

                if (string.IsNullOrEmpty(result))
                {
                    Debug.Log($"生成Code结束,输出的目录在 {configSetting.CodePath}");
                    AssetDatabase.Refresh();
                }
                else
                {
                    Debug.LogError($"生成Code错误,相关信息 {result}");
                }

                UnityEditor.EditorUtility.ClearProgressBar();
                return;
            }

            GUILayout.Label($"配置文件模版在:{HMExcelConfigSetting.CodeTemplatePath},如需修改可直接修改并重新生成代码和数据即可");
        }

        private string CheckFolderPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.Replace(" ", ""); //去掉空格

            if (path.EndsWith("/") || path.EndsWith("\\"))
            {
                path = path.Remove(path.Length - 1);
            }

            return path;
        }

        /// <summary>
        /// 根据Project窗口格式,one column就ping一下,two column就显示内容
        /// </summary>
        /// <param name="folderAsset"></param>
        private void PingFolder(DefaultAsset folderAsset, string folderPath)
        {
            Type projectBrowserType = Type.GetType("UnityEditor.ProjectBrowser,UnityEditor");
            if (projectBrowserType != null)
            {
                FieldInfo lastProjectBrowser = projectBrowserType.GetField("s_LastInteractedProjectBrowser",
                    BindingFlags.Static | BindingFlags.Public);
                if (lastProjectBrowser != null)
                {
                    object lastProjectBrowserInstance = lastProjectBrowser.GetValue(null);
                    FieldInfo projectBrowserViewMode =
                        projectBrowserType.GetField("m_ViewMode", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (projectBrowserViewMode != null)
                    {
                        // 0 - one column, 1 - two column
                        int viewMode = (int)projectBrowserViewMode.GetValue(lastProjectBrowserInstance);
                        if (viewMode == 1)
                        {
                            MethodInfo showFolderContents = projectBrowserType.GetMethod("ShowFolderContents",
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            if (showFolderContents != null)
                            {
                                var objs = AssetDatabase.FindAssets("", new[] { folderPath });
                                if (objs.Length > 0)
                                {
                                    UnityEditor.EditorGUIUtility.PingObject(
                                        AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(objs[0])));
                                    return;
                                }
                            }
                        }
                    }
                }

                UnityEditor.EditorGUIUtility.PingObject(folderAsset);
            }
        }
    }
}