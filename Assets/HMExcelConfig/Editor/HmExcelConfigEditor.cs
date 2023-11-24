using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HmExcelConfigEditor
{
    public class HmExcelConfigEditor : UnityEditor.EditorWindow
    {
        [MenuItem("HmExcelConfig/OpenConfig")]
        public static void OpenConfig()
        {
            var window = GetWindow<HmExcelConfigEditor>();
            window.titleContent = new GUIContent("HmExcelConfig工具");
        }


        private async void OnGUI()
        {
            var configSetting = HmExcelConfigSetting.Instance;
            if (configSetting == null) return;

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
                UnityEditor.EditorUtility.DisplayProgressBar("HmExcelConfigEditor正在生成Code", "正在生成Code,请稍候", 0f);
                string result = "";
                try
                {
                    result = await ExcelHelper.ExportAllExcelToCode(configSetting.ExcelFilePath, configSetting.CodePath,
                        configSetting.codeTemplatePath, configSetting.DataFilePath,
                        (progress, str) =>
                        {
                            UnityEditor.EditorUtility.DisplayProgressBar("HmExcelConfigEditor正在生成Code", str, progress);
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
                        int viewMode = (int) projectBrowserViewMode.GetValue(lastProjectBrowserInstance);
                        if (viewMode == 1)
                        {
                            MethodInfo showFolderContents = projectBrowserType.GetMethod("ShowFolderContents",
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            if (showFolderContents != null)
                            {
                                var objs = AssetDatabase.FindAssets("", new[] {folderPath});
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