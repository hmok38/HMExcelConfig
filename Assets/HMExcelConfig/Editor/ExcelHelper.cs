using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using OfficeOpenXml;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Events;


namespace HMExcelConfigEditor
{
    public class ExcelHelper
    {
        public const string ExcelHashRecordFileName = "HMExcelConfigHashes.json";

        public static string DataClassTemplate = @"using System.Collections.Generic;
using ProtoBuf;
using HMExcelConfig;

/// <summary> 数据来源:[filepath] </summary>
[ProtoContract]
public class [classname]:IExcelConfig
{";

        public static string DataFieldTemplate = @"
    /// <summary>[description]</summary>
    [ProtoMember([tag])]
    public [type] [name] { get; set; }";


        private static Dictionary<string, string> codeMap = new Dictionary<string, string>();
        private static Dictionary<string, string> categoryCodeMap = new Dictionary<string, string>();
        private static Dictionary<string, string> variantMap = new Dictionary<string, string>();
        private static Dictionary<string, ConfigInfo> configMap = new Dictionary<string, ConfigInfo>();

        public static async Task<string> ExportAllExcelToCode(string excelDir, string codeFileDir,
            string categoryCodeTemplatePath,
            string protoDataDir, string jsonDataDir, UnityAction<float, string> progressCB = null)
        {
            if (!Directory.Exists(excelDir)) return "不存在目录:" + excelDir;

            return await ExportSelectedExcelToCode(excelDir, GetExcelFiles(excelDir).Select(file => file.FullName),
                codeFileDir, categoryCodeTemplatePath, protoDataDir, jsonDataDir, progressCB);
        }

        /// <summary>
        /// 导出选中的Excel。主表和变体表共用同一个配置类，选中其中任意一个时会自动导出整个配置组。
        /// </summary>
        public static async Task<string> ExportSelectedExcelToCode(string excelDir,
            IEnumerable<string> selectedExcelFilePaths, string codeFileDir, string categoryCodeTemplatePath,
            string protoDataDir, string jsonDataDir, UnityAction<float, string> progressCB = null)
        {
            if (!Directory.Exists(excelDir)) return "不存在目录:" + excelDir;
            if (selectedExcelFilePaths == null) return "没有选择需要导出的Excel文件";

            var allFiles = GetExcelFiles(excelDir);
            var allFileMap = allFiles.ToDictionary(file => Path.GetFullPath(file.FullName),
                file => file, StringComparer.OrdinalIgnoreCase);
            var selectedPaths = new HashSet<string>(selectedExcelFilePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            if (selectedPaths.Count == 0) return "没有选择需要导出的Excel文件";

            foreach (var selectedPath in selectedPaths)
            {
                if (!allFileMap.ContainsKey(selectedPath))
                {
                    return $"选择的Excel文件不在配置目录中或文件类型不正确:{selectedPath}";
                }
            }

            var selectedClassNames = new HashSet<string>(selectedPaths
                .Select(path => GetClassNameAndVariantName(path, out _)), StringComparer.Ordinal);
            var files = allFiles
                .Where(file => selectedClassNames.Contains(GetClassNameAndVariantName(file.FullName, out _)))
                .ToArray();

            var exportedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var file in files)
                {
                    exportedHashes[GetExcelHashRecordKey(excelDir, file.FullName)] = CalculateExcelFileHash(file.FullName);
                }
            }
            catch (Exception e)
            {
                return "计算Excel文件Hash失败:" + e;
            }

            if (!File.Exists(categoryCodeTemplatePath)) return "不存在代码模版路径:" + categoryCodeTemplatePath;
            var categoryCodeTemplate = File.ReadAllText(categoryCodeTemplatePath);

            codeMap.Clear();
            categoryCodeMap.Clear();
            variantMap.Clear();
            configMap.Clear();

            Dictionary<string, FileInfo> fileInfoMap = new Dictionary<string, FileInfo>();

            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    var vFile = files[i];

                    //跳过其他文件
                    var fileName = Path.GetFileName(vFile.FullName);
                    if (!IsExcelConfigFile(fileName))
                    {
                        continue;
                    }

                    var className = GetClassNameAndVariantName(vFile.FullName, out string variantName);

                    progressCB?.Invoke(((float)(i + 1) / files.Length) * 0.9f, $"正在解析 {className}  {variantName} ");
                    ConfigInfo info = null;
                    if (configMap.ContainsKey(className))
                    {
                        info = configMap[className];
                    }
                    else
                    {
                        info = new ConfigInfo()
                        {
                            className = className,
                            variantDataMap = new Dictionary<string, List<FieldInfo>>()
                        };
                        configMap.Add(className, info);
                    }

                    if (!fileInfoMap.ContainsKey(className))
                    {
                        fileInfoMap.Add(className, vFile);
                    }
                    else
                    {
                        fileInfoMap[className] = vFile;
                    }


                    var resultStr = GetExcelData(vFile.FullName, ref info);
                    if (!string.IsNullOrEmpty(resultStr)) return resultStr;


                    await Task.Delay(10);
                }
                catch (Exception e)
                {
                    return e.ToString();
                }
            }

            foreach (var VARIABLE in configMap)
            {
                var vFile = fileInfoMap[VARIABLE.Key];
                ExportExcelToCode(VARIABLE.Value, vFile.FullName, categoryCodeTemplate, protoDataDir, out string code,
                    out string categoryCode);
                if (!codeMap.ContainsKey(VARIABLE.Key)) codeMap.Add(VARIABLE.Key, code);
                if (!categoryCodeMap.ContainsKey(VARIABLE.Key)) categoryCodeMap.Add(VARIABLE.Key, categoryCode);
            }


            progressCB?.Invoke(0.9f, "解析完毕,准备写入");
            //全部完成了,写入文件

            //var codeAsembly = GetAssembly(codeMap.Values.ToArray(), out string error);
            var codeAsembly = CompileAndRunCode(codeMap.Values.ToArray(), out string error);
            if (!string.IsNullOrEmpty(error))
            {
                return $"动态生成代码发生错误:" + error;
            }

            var types = codeAsembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                Debug.Log(types[i].FullName);
            }

            foreach (var codeKV in codeMap)
            {
                var result = WriteToFile(Path.Combine(codeFileDir, codeKV.Key) + ".cs", codeKV.Value);
                if (!string.IsNullOrEmpty(result)) return result;

                var type = codeAsembly.GetType(codeKV.Key);
                if (!configMap.TryGetValue(codeKV.Key, out ConfigInfo configInfo))
                {
                    return $"没有找到{codeKV.Key}的数据";
                }

                if (!WriteDataToProtobuf(configInfo, type, protoDataDir, jsonDataDir, out string protoError))
                {
                    return $"写入{codeKV.Key}的数据时发生错误: {protoError}";
                }
            }

            foreach (var codeKV in categoryCodeMap)
            {
                var result = WriteToFile(Path.Combine(codeFileDir, codeKV.Key) + "Category.cs", codeKV.Value);
                if (!string.IsNullOrEmpty(result)) return result;
            }

            var hashResult = SaveExcelHashRecord(jsonDataDir, exportedHashes);
            if (!string.IsNullOrEmpty(hashResult)) return hashResult;

            return "";
        }

        /// <summary>
        /// 获取配置目录下会参与导出的Excel文件。与原导出逻辑一致，只读取当前目录。
        /// </summary>
        public static FileInfo[] GetExcelFiles(string excelDir)
        {
            if (!Directory.Exists(excelDir)) return Array.Empty<FileInfo>();

            return new DirectoryInfo(excelDir).GetFiles()
                .Where(file => IsExcelConfigFile(file.Name))
                .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string GetExcelHashRecordPath(string jsonDataDir)
        {
            return Path.Combine(jsonDataDir, ExcelHashRecordFileName);
        }

        public static string GetExcelHashRecordKey(string excelDir, string excelFilePath)
        {
            var excelDirectoryPath = Path.GetFullPath(excelDir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var excelFullPath = Path.GetFullPath(excelFilePath);
            var prefix = excelDirectoryPath + Path.DirectorySeparatorChar;
            if (excelFullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return excelFullPath.Substring(prefix.Length).Replace('\\', '/');
            }

            return Path.GetFileName(excelFullPath);
        }

        public static string CalculateExcelFileHash(string excelFilePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = new FileStream(excelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var hash = md5.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public static Dictionary<string, string> LoadExcelHashRecord(string jsonDataDir, out string error)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var recordPath = GetExcelHashRecordPath(jsonDataDir);
            if (!File.Exists(recordPath))
            {
                error = "";
                return result;
            }

            try
            {
                var json = File.ReadAllText(recordPath, Encoding.UTF8);
                var records = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (records != null)
                {
                    foreach (var record in records)
                    {
                        result[record.Key.Replace('\\', '/')] = record.Value;
                    }
                }

                error = "";
            }
            catch (Exception e)
            {
                error = $"读取Hash记录失败:{recordPath}\n{e.Message}";
            }

            return result;
        }

        private static string SaveExcelHashRecord(string jsonDataDir,
            IReadOnlyDictionary<string, string> exportedHashes)
        {
            try
            {
                var records = LoadExcelHashRecord(jsonDataDir, out _);
                foreach (var exportedHash in exportedHashes)
                {
                    records[exportedHash.Key] = exportedHash.Value;
                }

                if (!Directory.Exists(jsonDataDir))
                {
                    Directory.CreateDirectory(jsonDataDir);
                }

                var sortedRecords = new SortedDictionary<string, string>(records, StringComparer.OrdinalIgnoreCase);
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(sortedRecords,
                    Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(GetExcelHashRecordPath(jsonDataDir), json, new UTF8Encoding(false));
                return "";
            }
            catch (Exception e)
            {
                return "保存Excel文件Hash记录失败:" + e;
            }
        }

        private static bool IsExcelConfigFile(string fileName)
        {
            return fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                   && !fileName.StartsWith("~$", StringComparison.Ordinal)
                   && !fileName.Contains("#");
        }

        private static bool WriteDataToProtobuf(ConfigInfo configInfo, Type type, string protoDataDir,
            string jsonDataDir,
            out string error)
        {
            var propertyInfoMap = new Dictionary<string, PropertyInfo>();

            //主表(非变种表)
            if (configInfo.classFieldInfos != null)
            {
                var mainObjs = GetCsDataObject(configInfo.classFieldInfos, type, propertyInfoMap, configInfo.className,
                    null, out error);
                if (!string.IsNullOrEmpty(error))
                {
                    return false;
                }

                //  Serializer.NonGeneric.PrepareSerializer(type);
                WriteToProbufDataFile(mainObjs, configInfo.className, "", protoDataDir, type, out error);
                if (!string.IsNullOrEmpty(error))
                {
                    return false;
                }

                WriteToJsonDataFile(mainObjs, configInfo.className, "", jsonDataDir, out error);
                if (!string.IsNullOrEmpty(error))
                {
                    return false;
                }
            }


            //变种表
            foreach (var VARIABLE in configInfo.variantDataMap)
            {
                var tempObjs = GetCsDataObject(VARIABLE.Value, type, propertyInfoMap, configInfo.className,
                    VARIABLE.Key, out error);
                if (!string.IsNullOrEmpty(error))
                {
                    return false;
                }

                WriteToProbufDataFile(tempObjs, configInfo.className, VARIABLE.Key, protoDataDir, type,
                    out error);
                if (!string.IsNullOrEmpty(error))
                {
                    return false;
                }

                WriteToJsonDataFile(tempObjs, configInfo.className, VARIABLE.Key, jsonDataDir, out error);
                if (!string.IsNullOrEmpty(error))
                {
                    return false;
                }
            }


            //
            // using (var ms = new System.IO.FileStream(Path.Combine(protoDataDir, configInfo.className),
            //            FileMode.OpenOrCreate))
            // {
            //     //ProtoBuf.Serializer.Serialize(ms, testProtobuff);
            // }

            error = "";
            return true;
        }

        private static Array GetCsDataObject(List<FieldInfo> fieldInfos, Type type,
            Dictionary<string, PropertyInfo> propertyInfoMap, string className, string varateName, out string error
        )
        {
            if (fieldInfos == null)
            {
                //是主表,且主表没有变量的情况下(即没放主表)
                error = "";
                return Array.CreateInstance(type, 0);
            }

            var idDatas = fieldInfos[0].datas;
            error = "";
            Debug.Log($"正在处理数据:{className}  varateName:{varateName}");
            //没有数据就不写?
            //if (idDatas.Length == 0) return true;

            List<object> list = new List<object>();

            for (int i = 0; i < idDatas.Length; i++)
            {
                //检查ID字段,如果没有值则此行数据不要
                if (idDatas[i] == null || string.IsNullOrEmpty(idDatas[i].Replace(" ", ""))) continue;
                var clasV = Activator.CreateInstance(type);

                for (int j = 0; j < fieldInfos.Count; j++)
                {
                    var fieldInfo = fieldInfos[j];
                    if (!propertyInfoMap.ContainsKey(fieldInfo.name))
                    {
                        var property = type.GetProperty(fieldInfo.name,
                            BindingFlags.Public | BindingFlags.Instance);
                        if (property == null)
                        {
                            error = $"获取数据时,发现{className} {varateName} 的属性{fieldInfo.name} 没有在类 {type.Name} 中找到";
                            return null;
                        }

                        propertyInfoMap.Add(fieldInfo.name, property);
                    }


                    var obj = GetObjectByValue(fieldInfo.typeStr, fieldInfo.datas[i], out string errorTemp);
                    if (!string.IsNullOrEmpty(errorTemp))
                    {
                        error = $"获取数据时{className} {varateName} 的属性{fieldInfo.name} 的值转换错误:{errorTemp}";
                        return null;
                    }

                    propertyInfoMap[fieldInfo.name].SetValue(clasV, obj);
                }

                list.Add(clasV);
            }


            // for (int i = 0; i < list.Count; i++)
            // {
            //     Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(list[i]));
            // }

            error = "";

            var array = Array.CreateInstance(type, list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                array.SetValue(list[i], i);
            }

            return array;
        }

        private static bool WriteToProbufDataFile(Array csDatas, string className, string varateName,
            string protoDataDir, Type type, out string error)
        {
            try
            {
                string path = "";
                string dir = "";
                if (!string.IsNullOrEmpty(varateName))
                {
                    path = Path.Combine(protoDataDir, "variant");
                    dir = Path.Combine(path, varateName);
                }
                else
                {
                    dir = protoDataDir;
                }

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                path = Path.Combine(dir, className + ".bytes");


                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                using (var ms = new System.IO.FileStream(path,
                           FileMode.OpenOrCreate))
                {
                    //ProtoBuf.Serializer.SerializeWithLengthPrefix(ms, csDatas,PrefixStyle.Fixed32);
                    ProtoBuf.Serializer.Serialize(ms, csDatas);
                }

                error = "";
                return true;
            }
            catch (Exception e)
            {
                error = e.ToString();
                return false;
            }
        }

        private static bool WriteToJsonDataFile(Array csDatas, string className, string varateName, string protoDataDir,
            out string error)
        {
            try
            {
                string dir = "";
                if (!string.IsNullOrEmpty(varateName))
                {
                    dir = Path.Combine(protoDataDir, "variant", varateName);
                }
                else
                {
                    dir = protoDataDir;
                }

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var path = Path.Combine(dir, className + ".json");
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(csDatas, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(path, json, Encoding.UTF8);
                error = "";
                return true;
            }
            catch (Exception e)
            {
                error = e.ToString();
                return false;
            }
        }

        private static object GetObjectByValue(string typeStr, string valueStr, out string error)
        {
            if (string.IsNullOrEmpty(typeStr))
            {
                error = "类型值不能为空";
                return null;
            }

            try
            {
                error = "";
                switch (typeStr)
                {
                    case "string": return valueStr;
                    case "int": return int.Parse(string.IsNullOrWhiteSpace(valueStr) ? "0" : valueStr);
                    case "uint": return uint.Parse(string.IsNullOrWhiteSpace(valueStr) ? "0" : valueStr);
                    case "bool": return bool.Parse(string.IsNullOrWhiteSpace(valueStr) ? "false" : valueStr);
                    case "long": return long.Parse(string.IsNullOrWhiteSpace(valueStr) ? "0" : valueStr);
                    case "float": return float.Parse(string.IsNullOrWhiteSpace(valueStr) ? "0" : valueStr);
                    case "double": return double.Parse(string.IsNullOrWhiteSpace(valueStr) ? "0" : valueStr);
                    case "int[]":
                    case "uint[]":
                    case "bool[]":
                    case "long[]":
                    case "float[]":
                    case "double[]":
                    case "string[]":
                    {
                        string baseType = typeStr.Replace("[", "").Replace("]", "");
                        var strs = valueStr.Split(',');
                        if (typeStr.Equals("string[]"))
                        {
                            if (valueStr.Length > 0 && (!valueStr.StartsWith("\"") || !valueStr.EndsWith("\"")))
                            {
                                error =
                                    ($"字符串数组中的值必须前后有冒号,值: {(string.IsNullOrWhiteSpace(valueStr) ? "空白符" : valueStr)}");
                                return null;
                            }

                            var regex = new Regex("\",");
                            strs = regex.Split(valueStr);

                            for (int i = 0; i < strs.Length; i++)
                            {
                                //如果前后是"就移除前后的"
                                if (strs[i].StartsWith("\""))
                                {
                                    var old = strs[i];
                                    strs[i] = old.Substring(1, old.Length - 1);

                                    //最后一个的话,要把结尾的"去掉
                                    if (i == strs.Length - 1 && strs[i].EndsWith("\""))
                                    {
                                        old = strs[i];
                                        strs[i] = old.Substring(0, old.Length - 1);
                                    }
                                }
                            }
                        }

                        Type type = GetBaseType(baseType, out string typeResult);
                        if (type == null)
                        {
                            error = $"类型{typeStr}的基础类型错误:" + typeResult;
                            return null;
                        }

                        if (string.IsNullOrWhiteSpace(valueStr))
                        {
                            return Array.CreateInstance(type, 0);
                        }

                        var array = Array.CreateInstance(type, strs.Length);
                        for (int i = 0; i < array.Length; i++)
                        {
                            var obj = GetObjectByValue(baseType, strs[i], out string thisError);
                            if (!string.IsNullOrEmpty(thisError))
                            {
                                error = $"类型 {typeStr} 的值 ({valueStr}) 转数据错误:" + thisError;
                                return null;
                            }

                            array.SetValue(obj, i);
                        }

                        return array;
                    }
                }

                error = $"不支持此类型{typeStr}";
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"类型 {typeStr} 的值 ({valueStr}) 转数据错误 {e.ToString()}");
                error = $"类型 {typeStr} 的值 ({valueStr}) 转数据错误 {e.ToString()}";
                return null;
            }
        }

        private static Type GetBaseType(string typeStr, out string result)
        {
            result = "";
            switch (typeStr)
            {
                case "string": return typeof(string);
                case "int": return typeof(int);
                case "uint": return typeof(uint);
                case "bool": return typeof(bool);
                case "long": return typeof(long);
                case "float": return typeof(float);
                case "double": return typeof(double);
            }

            result = $"数组基础类型不支持此种类型:{typeStr}";
            return null;
        }


        // private static Assembly GetAssembly(string[] code, out string error)
        // {
        //     SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);
        //     //var codeProvider = new CSharpCodeProvider();
        //
        //     var icc = codeProvider.CreateCompiler();
        //
        //     System.CodeDom.Compiler.CompilerParameters parameters = new CompilerParameters();
        //     parameters.GenerateExecutable = false;
        //     parameters.GenerateInMemory = true;
        //
        //     var assems = AppDomain.CurrentDomain.GetAssemblies();
        //     for (int i = 0; i < assems.Length; i++)
        //     {
        //         if (assems[i].IsDynamic || string.IsNullOrEmpty(assems[i].Location)) continue;
        //         parameters.ReferencedAssemblies.Add(assems[i].Location);
        //     }
        //
        //
        //     CompilerResults results = icc.CompileAssemblyFromSourceBatch(parameters, code);
        //     error = "";
        //     if (results.CompiledAssembly == null)
        //     {
        //         error = results.Errors.HasErrors ? results.Errors[0].ErrorText : "未知错误";
        //     }
        //
        //     return results.CompiledAssembly;
        // }


        static Assembly CompileAndRunCode(string[] codes, out string error)
        {
            SyntaxTree[] trees = new SyntaxTree[codes.Length];
            for (int i = 0; i < codes.Length; i++)
            {
                trees[i] = CSharpSyntaxTree.ParseText(codes[i]);
            }

            var compilation =
                CSharpCompilation.Create("DynamicAssembly").AddReferences(AppDomain.CurrentDomain.GetAssemblies()
                        .Where(x => !x.IsDynamic && !string.IsNullOrWhiteSpace(x.Location))
                        .Select(x => MetadataReference.CreateFromFile(x.Location)))
                    .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddSyntaxTrees(trees);


            EmitResult result = null;
            byte[] dllBytes;

            using (var ms = new System.IO.MemoryStream())
            {
                result = compilation.Emit(ms);
                dllBytes = ms.ToArray();
            }

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                error = "动态编译错误: ";
                foreach (var diagnostic in failures)
                {
                    error += $"{diagnostic.Id}: {diagnostic.GetMessage()}";
                }

                return null;
            }
            else
            {
                error = "";
                return Assembly.Load(dllBytes);
            }
        }


        public static string GetExcelData(string excelFilePath, ref ConfigInfo configInfo)
        {
            var fileName = Path.GetFileName(excelFilePath);
            if (!IsExcelConfigFile(fileName))
            {
                return "文件类型不正确:" + excelFilePath;
            }

            var className = GetClassNameAndVariantName(excelFilePath, out string variantName);
            configInfo.className = className;

            var fieldInfos = new List<FieldInfo>();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var pa = new ExcelPackage(new FileInfo(excelFilePath)))
            {
                var sheet = pa.Workbook.Worksheets.FirstOrDefault();
                if (sheet == null)
                {
                    return "文件内不存在sheet表:" + excelFilePath;
                }

                int idDecRawIndex = -1;
                int idDecColIndex = -1;

                var start = sheet.Dimension.Start;
                var end = sheet.Dimension.End;
                //按列读取
                for (var col = start.Column; col <= end.Column; col++)
                {
                    //找ID字段,确定表格的第一个字段位置
                    if (idDecRawIndex == -1)
                    {
                        for (int row = start.Row; row <= end.Row; row++)
                        {
                            if (sheet.Cells[row, col].Value != null &&
                                sheet.Cells[row, col].Value.ToString().ToLower().Equals("id")
                                && sheet.Cells[row + 1, col].Value != null
                                && (sheet.Cells[row + 1, col].Value.ToString().ToLower().Equals("string") ||
                                    sheet.Cells[row + 1, col].Value.ToString().ToLower().Equals("int")))
                            {
                                idDecRawIndex = row - 1;
                                idDecColIndex = col;
                                break;
                            }
                        }
                    }

                    if (idDecRawIndex == -1) continue;
                    if (sheet.Cells[idDecRawIndex + 1, col] == null ||
                        sheet.Cells[idDecRawIndex + 1, col].Value == null
                        || sheet.Cells[Mathf.Max(idDecRawIndex - 1, start.Row), col].Value?.ToString().IndexOf("#") == 0
                        || sheet.Cells[Mathf.Max(idDecRawIndex, start.Row), col].Value?.ToString().IndexOf("#") == 0
                        || sheet.Cells[Mathf.Max(idDecRawIndex - 10, start.Row), col].Value?.ToString().IndexOf("#") ==
                        0) continue;
                    var fieldInfo = new FieldInfo();

                    fieldInfo.dec = sheet.Cells[idDecRawIndex, col].Value?.ToString();
                    fieldInfo.dec = fieldInfo.dec.Replace("\n", "");
                    if (sheet.Cells[idDecRawIndex + 1, col].Value == null)
                    {
                        var a = idDecRawIndex + 1;
                    }

                    fieldInfo.name = sheet.Cells[idDecRawIndex + 1, col].Value.ToString();
                    fieldInfo.typeStr = sheet.Cells[idDecRawIndex + 2, col].Value?.ToString();
                    if (string.IsNullOrEmpty(fieldInfo.typeStr))
                    {
                        fieldInfo.typeStr = "string";
                    }

                    var dataStartRow = idDecRawIndex + 3;
                    fieldInfo.datas = new string[end.Row + 1 - dataStartRow];
                    //if(fieldInfo.datas.Length==0)Debug.Log($"表{excelFilePath} 数据数量为0");
                    for (int row = dataStartRow; row < end.Row + 1; row++)
                    {
                        //这一行的第一列的前一列不为空,且以#开始的,代表是注释符号,这一行数据不要
                        if (sheet.Cells[row, Mathf.Max(idDecColIndex - 1, 1)].Value?.ToString().IndexOf("#") == 0)
                            continue;
                        var v = sheet.Cells[row, col].Value;
                        string vStr = (v != null ? v.ToString() : "");

                        fieldInfo.datas[row - dataStartRow] = vStr;
                    }

                    fieldInfos.Add(fieldInfo);
                }
            }

            if (string.IsNullOrEmpty(variantName))
            {
                configInfo.classFieldInfos = fieldInfos;
            }
            else
            {
                //if (configInfo.classFieldInfos == null) configInfo.classFieldInfos = fieldInfos;
                if (configInfo.variantDataMap.ContainsKey(variantName))
                {
                    Debug.Log("11");
                }

                configInfo.variantDataMap.Add(variantName, fieldInfos);
            }

            return "";
        }

        /// <summary>
        /// 根据文件名获得类名和变体名
        /// </summary>
        /// <param name="excelFilePath"></param>
        /// <param name="variantName"></param>
        /// <returns></returns>
        private static string GetClassNameAndVariantName(string excelFilePath, out string variantName)
        {
            variantName = "";
            var fileName = Path.GetFileName(excelFilePath);


            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            //使用了变种表
            if (fileNameWithoutExtension.Contains('.'))
            {
                var fileNames = fileNameWithoutExtension.Split('.');
                fileNameWithoutExtension = fileNames[0];
                variantName = fileNames[1];
            }

            return fileNameWithoutExtension;
        }

        private static void ExportExcelToCode(ConfigInfo configInfo, string filePath,
            string categoryCodeTemplate, string protoDataDir,
            out string code, out string categoryCode)
        {
            var fieldInfos = configInfo.classFieldInfos ?? configInfo.variantDataMap.First().Value;
            if (fieldInfos == null)
            {
                Debug.LogError($"主表没有字段,变种表也没有字段,{filePath}");
            }

            DirectoryInfo directoryInfo = new DirectoryInfo(Application.dataPath);
            var dirFullName = directoryInfo.Parent.FullName + Path.DirectorySeparatorChar;
            var className = GetClassNameAndVariantName(filePath, out string variantName);
            var classCode = DataClassTemplate;
            classCode = classCode.Replace("[classname]", className);

            var path = filePath.Replace(dirFullName, ""); //移除项目根目录以上的路径
            classCode = classCode.Replace("[filepath]", path);
            for (int i = 0; i < fieldInfos.Count; i++)
            {
                var field = fieldInfos[i];
                if (field != null)
                {
                    var fieldTmp = DataFieldTemplate;

                    fieldTmp = fieldTmp.Replace("[description]", field.dec);
                    fieldTmp = fieldTmp.Replace("[tag]", (i + 1).ToString());
                    fieldTmp = fieldTmp.Replace("[type]", field.typeStr);
                    fieldTmp = fieldTmp.Replace("[name]", field.name);
                    classCode += fieldTmp;
                }
            }

            classCode += "\n}";

            code = classCode;

            bool haveVariant = configInfo.variantDataMap.Count > 0;
            string dataUrl = protoDataDir + "/" + className + ".bytes";
            string variantNames = "";
            if (haveVariant)
            {
                dataUrl = protoDataDir + "/variant/[variantName]/" + className + ".bytes";
                foreach (var variantData in configInfo.variantDataMap)
                {
                    if (!string.IsNullOrEmpty(variantData.Key))
                    {
                        if (variantNames.Length <= 0)
                        {
                            variantNames = $"\"{variantData.Key}\"";
                        }
                        else
                        {
                            variantNames = $"{variantNames},\"{variantData.Key}\"";
                        }
                    }
                }
            }

            var categoryClassCode = categoryCodeTemplate;
            categoryClassCode = categoryClassCode.Replace("[classname]", className)
                .Replace("[idtype]", fieldInfos[0].typeStr).Replace("[dataUrl]", dataUrl)
                .Replace("[haveVariant]", haveVariant.ToString().ToLower()).Replace("[VariantNames]", variantNames);
            categoryCode = categoryClassCode;
        }

        private static string WriteToFile(string filePath, string codeContent)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                DirectoryInfo directoryInfo = fileInfo.Directory;
                if (!Directory.Exists(directoryInfo.FullName))
                {
                    Directory.CreateDirectory(directoryInfo.FullName);
                }


                File.WriteAllText(filePath, codeContent);
            }
            catch (Exception e)
            {
                return e.Message;
            }

            return "";
        }

        private static bool ConvertType(string type, string value, out string result)
        {
            switch (type)
            {
                case "string":

                    result = $"\"{value}\"";
                    break;
                case "bool":
                    result = $"{value.ToLower()}";
                    break;
                case "int":
                case "uint":
                case "int32":
                case "int64":
                case "long":
                case "float":
                case "double":
                {
                    value = value.Replace("{", "").Replace("}", "");
                    if (value == "")
                    {
                        result = "0";
                        return true;
                    }

                    result = value;
                    break;
                }

                case "uint[]":
                case "int[]":
                case "int32[]":
                case "long[]":
                case "string[]":
                case "float[]":
                case "double[]":
                {
                    value = value.Replace("{", "").Replace("}", "");
                    if (!value.StartsWith("[")) value = $"[{value}";
                    if (!value.StartsWith("]")) value = $"{value}]";
                    result = value;
                    break;
                }

                // case "int[][]":
                //     return $"[{value}]";

                // case "AttrConfig":
                //     string[] ss = value.Split(':');
                //     return "{\"_t\":\"AttrConfig\"," + "\"Ks\":" + ss[0] + ",\"Vs\":" + ss[1] + "}";
                default:
                    result = $"不支持此类型: {type}";
                    return false;
            }

            return true;
        }

        public class FieldInfo
        {
            public string name;
            public string typeStr;
            public string dec;
            public string[] datas;
        }

        public class ConfigInfo
        {
            public string className;
            public List<FieldInfo> classFieldInfos;

            /// <summary>
            /// 表格数据,key为变种名 即excel文件名中如 Language.chinese.xls,Language为类名,chinese为变种名
            /// 如果此配置表没有变种,则数量为0
            /// </summary>
            public Dictionary<string, List<FieldInfo>> variantDataMap;

            public string dataCode;

            public Type dataClassType;

            public string categoryCode;
        }

        /// <summary>
        /// 导出excel表到json,一定是数组
        /// </summary>
        /// <param name="excelPath"></param>
        /// <returns></returns>
        public static string ExportExcelToJson(string excelPath)
        {
            if (!File.Exists(excelPath))
            {
                Debug.LogError("不存在Excel文件:" + excelPath);
                return null;
            }

            UnityEditor.EditorUtility.DisplayProgressBar("正在读取excel", "正在读取Excel文件:" + excelPath, 0.1f);
            ConfigInfo configInfo = new ConfigInfo();
            var info = GetExcelData(excelPath, ref configInfo);
            int dataCount = configInfo.classFieldInfos[0].datas.Length;
            UnityEditor.EditorUtility.DisplayProgressBar("正在读取excel", "转换数据", 0.5f);
            List<Dictionary<string, object>> datas = new List<Dictionary<string, object>>();
            for (int i = 0; i < dataCount; i++)
            {
                Dictionary<string, object> map = new Dictionary<string, object>();
                for (int fieldIndex = 0; fieldIndex < configInfo.classFieldInfos.Count; fieldIndex++)
                {
                    var sFieldInfo = configInfo.classFieldInfos[fieldIndex];
                    if (sFieldInfo.typeStr.Contains('[') || sFieldInfo.typeStr.Contains(']'))
                    {
                        Debug.LogError($"不支持数组类型导出到Json 字段名:{sFieldInfo.name} 类型:{sFieldInfo.typeStr}");
                        UnityEditor.EditorUtility.ClearProgressBar();
                        return null;
                    }

                    var obj = GetObjectByValue(sFieldInfo.typeStr, sFieldInfo.datas[i], out string error);
                    map.Add(sFieldInfo.name, obj);
                }

                datas.Add(map);
            }

            UnityEditor.EditorUtility.DisplayProgressBar("正在读取excel", "正在转换json", 0.6f);
            try
            {
                var a = Newtonsoft.Json.JsonConvert.SerializeObject(datas);
                UnityEditor.EditorUtility.ClearProgressBar();
                return a;
            }
            catch (Exception e)
            {
                Debug.LogError($"导出Json失败,错误信息:{e.ToString()}");
                UnityEditor.EditorUtility.ClearProgressBar();
                return "";
            }
        }

        /// <summary>
        /// 导出excel数据到类,一定是传入类的List
        /// </summary>
        /// <param name="excelPath"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static List<T> ExportExcelToClass<T>(string excelPath) where T : class
        {
            var json = ExportExcelToJson(excelPath);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<T>>(json);
                Debug.Log($"list : {(list != null ? ("数据量 " + list.Count.ToString()) : "为空")}");
                return list;
            }
            catch (Exception e)
            {
                Debug.LogError($"导出Excel数据到类失败,错误信息:{e.ToString()}");
                return null;
            }
        }
    }
}
