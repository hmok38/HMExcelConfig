using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HMExcelConfigEditor
{
    public class HMExcelConfigEditor : UnityEditor.EditorWindow
    {
        private sealed class ExcelFileItem
        {
            public string FullPath;
            public string RecordKey;
            public string CurrentHash;
            public string ExportedHash;
            public string Error;
            public bool Selected;
            public bool HasExportRecord;
            public bool IsChanged;
        }

        private readonly List<ExcelFileItem> _excelFiles = new List<ExcelFileItem>();
        private Vector2 _excelListScroll;
        private string _excelScanError;
        private bool _isExporting;

        [MenuItem("HMExcelConfig/OpenConfig")]
        public static void OpenConfig()
        {
            var window = GetWindow<HMExcelConfigEditor>();
            window.titleContent = new GUIContent("HMExcelConfig工具");
            window.RefreshExcelFiles(false);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("HMExcelConfig工具");
            RefreshExcelFiles(true);
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

            GUILayout.Space(20);
            var jsonFilePathStr = EditorGUILayout.TextField("Json数据输出路径:", configSetting.JsonFilePath);
            if (GUILayout.Button($"打开目录", GUILayout.Width(100)))
            {
                if (!Directory.Exists(configSetting.JsonFilePath))
                {
                    Directory.CreateDirectory(configSetting.JsonFilePath);
                }

                UnityEditor.EditorUtility.RevealInFinder(configSetting.JsonFilePath);
            }

            GUILayout.Space(10);
            var shouldRefreshExcelFiles = false;
            if (!excelFilePathStr.Equals(configSetting.ExcelFilePath))
            {
                configSetting.ExcelFilePath = CheckFolderPath(excelFilePathStr);
                UnityEditor.EditorUtility.SetDirty(configSetting);
                shouldRefreshExcelFiles = true;
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

            if (!jsonFilePathStr.Equals(configSetting.JsonFilePath))
            {
                configSetting.JsonFilePath = CheckFolderPath(jsonFilePathStr);
                UnityEditor.EditorUtility.SetDirty(configSetting);
                shouldRefreshExcelFiles = true;
            }

            if (shouldRefreshExcelFiles)
            {
                RefreshExcelFiles(true);
            }

            DrawExcelFileSelection();

            var selectedFiles = _excelFiles.Where(file => file.Selected).Select(file => file.FullPath).ToArray();
            var exportRequested = false;
            using (new EditorGUI.DisabledScope(_isExporting || selectedFiles.Length == 0))
            {
                exportRequested = GUILayout.Button($"生成选中的代码和数据 ({selectedFiles.Length})",
                    GUILayout.Width(250), GUILayout.Height(50));
            }

            if (exportRequested)
            {
                _isExporting = true;
                UnityEditor.EditorUtility.DisplayProgressBar("HMExcelConfigEditor正在生成Code", "正在生成Code,请稍候", 0f);
                string result = "";
                try
                {
                    result = await ExcelHelper.ExportSelectedExcelToCode(configSetting.ExcelFilePath, selectedFiles,
                        configSetting.CodePath, HMExcelConfigSetting.CodeTemplatePath,
                        configSetting.DataFilePath, configSetting.JsonFilePath,
                        (progress, str) =>
                        {
                            UnityEditor.EditorUtility.DisplayProgressBar("HMExcelConfigEditor正在生成Code", str,
                                progress);
                        });
                }
                catch (Exception e)
                {
                    result = e.ToString();
                }
                finally
                {
                    _isExporting = false;
                    UnityEditor.EditorUtility.ClearProgressBar();
                }

                if (string.IsNullOrEmpty(result))
                {
                    Debug.Log($"生成Code结束,输出的目录在 {configSetting.CodePath}");
                    RefreshExcelFiles(true);
                    AssetDatabase.Refresh();
                }
                else
                {
                    Debug.LogError($"生成Code错误,相关信息 {result}");
                }

                return;
            }

            GUILayout.Space(5);
            GUILayout.Label($"配置文件模版在:{HMExcelConfigSetting.CodeTemplatePath},如需修改可直接修改并重新生成代码和数据即可");
        }

        private void DrawExcelFileSelection()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("选择要导出的Excel表", EditorStyles.boldLabel);

            var changedCount = _excelFiles.Count(file => file.IsChanged);
            var selectedCount = _excelFiles.Count(file => file.Selected);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_isExporting))
                {
                    if (GUILayout.Button("刷新", GUILayout.Width(60)))
                    {
                        RefreshExcelFiles(false);
                    }

                    if (GUILayout.Button("选择变更", GUILayout.Width(75)))
                    {
                        foreach (var file in _excelFiles)
                        {
                            file.Selected = file.IsChanged;
                        }
                    }

                    if (GUILayout.Button("全选", GUILayout.Width(60)))
                    {
                        foreach (var file in _excelFiles)
                        {
                            file.Selected = true;
                        }
                    }

                    if (GUILayout.Button("全不选", GUILayout.Width(60)))
                    {
                        foreach (var file in _excelFiles)
                        {
                            file.Selected = false;
                        }
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label($"共 {_excelFiles.Count} 个 / 变更 {changedCount} 个 / 已选 {selectedCount} 个");
            }

            if (!string.IsNullOrEmpty(_excelScanError))
            {
                EditorGUILayout.HelpBox(_excelScanError, MessageType.Warning);
            }

            if (_excelFiles.Count == 0)
            {
                EditorGUILayout.HelpBox("当前目录没有可导出的 .xlsx 文件。", MessageType.Info);
                return;
            }

            var scrollHeight = Mathf.Clamp(position.height - 430f, 120f, 300f);
            _excelListScroll = EditorGUILayout.BeginScrollView(_excelListScroll, GUILayout.Height(scrollHeight));
            using (new EditorGUI.DisabledScope(_isExporting))
            {
                foreach (var file in _excelFiles)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        file.Selected = EditorGUILayout.Toggle(file.Selected, GUILayout.Width(20));

                        var oldContentColor = GUI.contentColor;
                        if (!string.IsNullOrEmpty(file.Error))
                        {
                            GUI.contentColor = EditorGUIUtility.isProSkin
                                ? new Color(1f, 0.35f, 0.35f)
                                : new Color(0.75f, 0f, 0f);
                        }
                        else if (file.IsChanged)
                        {
                            GUI.contentColor = EditorGUIUtility.isProSkin
                                ? new Color(1f, 0.65f, 0.2f)
                                : new Color(0.75f, 0.3f, 0f);
                        }

                        var status = GetExcelFileStatus(file);
                        var tooltip = string.IsNullOrEmpty(file.Error)
                            ? $"当前MD5: {file.CurrentHash}\n上次导出MD5: {(file.HasExportRecord ? file.ExportedHash : "无")}"
                            : file.Error;
                        EditorGUILayout.LabelField(new GUIContent(file.RecordKey, tooltip),
                            GUILayout.ExpandWidth(true));
                        GUILayout.Label(status, GUILayout.Width(60));
                        GUI.contentColor = oldContentColor;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField($"Hash记录: {ExcelHelper.GetExcelHashRecordPath(HMExcelConfigSetting.Instance.JsonFilePath)}",
                EditorStyles.miniLabel);
            EditorGUILayout.HelpBox("橙色表名表示内容有变化或从未导出；同一主表的变体文件会作为一组导出。",
                MessageType.None);
        }

        private static string GetExcelFileStatus(ExcelFileItem file)
        {
            if (!string.IsNullOrEmpty(file.Error)) return "读取失败";
            if (!file.HasExportRecord) return "未导出";
            return file.IsChanged ? "已变更" : "未变更";
        }

        private void RefreshExcelFiles(bool selectChangedByDefault)
        {
            var previousSelections = _excelFiles.ToDictionary(file => file.FullPath, file => file.Selected,
                StringComparer.OrdinalIgnoreCase);
            _excelFiles.Clear();
            _excelScanError = "";

            var configSetting = HMExcelConfigSetting.Instance;
            if (configSetting == null) return;
            if (!Directory.Exists(configSetting.ExcelFilePath))
            {
                _excelScanError = "不存在Excel目录:" + configSetting.ExcelFilePath;
                return;
            }

            var hashRecords = ExcelHelper.LoadExcelHashRecord(configSetting.JsonFilePath, out var hashRecordError);
            _excelScanError = hashRecordError;

            try
            {
                foreach (var fileInfo in ExcelHelper.GetExcelFiles(configSetting.ExcelFilePath))
                {
                    var item = new ExcelFileItem
                    {
                        FullPath = fileInfo.FullName,
                        RecordKey = ExcelHelper.GetExcelHashRecordKey(configSetting.ExcelFilePath, fileInfo.FullName)
                    };

                    try
                    {
                        item.CurrentHash = ExcelHelper.CalculateExcelFileHash(fileInfo.FullName);
                        item.HasExportRecord = hashRecords.TryGetValue(item.RecordKey, out var exportedHash);
                        item.ExportedHash = exportedHash;
                        item.IsChanged = !item.HasExportRecord ||
                                         !string.Equals(item.CurrentHash, item.ExportedHash,
                                             StringComparison.OrdinalIgnoreCase);
                    }
                    catch (Exception e)
                    {
                        item.Error = e.Message;
                        item.IsChanged = true;
                    }

                    item.Selected = selectChangedByDefault
                        ? item.IsChanged
                        : previousSelections.TryGetValue(item.FullPath, out var selected) ? selected : item.IsChanged;
                    _excelFiles.Add(item);
                }

                _excelFiles.Sort((left, right) =>
                {
                    var changedComparison = right.IsChanged.CompareTo(left.IsChanged);
                    return changedComparison != 0
                        ? changedComparison
                        : string.Compare(left.RecordKey, right.RecordKey, StringComparison.OrdinalIgnoreCase);
                });
            }
            catch (Exception e)
            {
                _excelScanError = string.IsNullOrEmpty(_excelScanError)
                    ? e.ToString()
                    : _excelScanError + "\n" + e;
            }

            Repaint();
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
