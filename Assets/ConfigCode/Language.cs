using System.Collections.Generic;
using ProtoBuf;
using HMExcelConfig;

/// <summary> 数据来源:D:\Code\Framework\HMExcelConfig\Excel\Language.xlsx </summary>
[ProtoContract]
public class Language:IExcelConfig
{
    /// <summary>Id</summary>
    [ProtoMember(1)]
    public string Id { get; set; }
    /// <summary>内容</summary>
    [ProtoMember(2)]
    public string Content { get; set; }
}