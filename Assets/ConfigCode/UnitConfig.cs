using System.Collections.Generic;
using ProtoBuf;
using HMExcelConfig;

/// <summary> 数据来源:D:\Code\Framework\HMExcelConfig\Excel\UnitConfig.xlsx </summary>
[ProtoContract]
public class UnitConfig:IExcelConfig
{
    /// <summary>Id</summary>
    [ProtoMember(1)]
    public int Id { get; set; }
    /// <summary>种类类型</summary>
    [ProtoMember(2)]
    public int level { get; set; }
    /// <summary>名字</summary>
    [ProtoMember(3)]
    public int Name { get; set; }
    /// <summary>详细说明</summary>
    [ProtoMember(4)]
    public int des { get; set; }
}