using System.Collections.Generic;
using ProtoBuf;
using HMExcelConfig;

/// <summary> 数据来源:D:\Code\Framework\HMExcelConfig\Excel\DemoExcelConfig.xlsx </summary>
[ProtoContract]
public class DemoExcelConfig:IExcelConfig
{
    /// <summary>(这是主表的内容)第一个有效字段必须是Id,且必须是Int或者string类型</summary>
    [ProtoMember(1)]
    public int Id { get; set; }
    /// <summary>值类型</summary>
    [ProtoMember(2)]
    public float ValueType { get; set; }
    /// <summary>值列表</summary>
    [ProtoMember(3)]
    public int[] ValueTypeList { get; set; }
    /// <summary>字符串类型</summary>
    [ProtoMember(4)]
    public string Name { get; set; }
    /// <summary>字符串列表类型</summary>
    [ProtoMember(5)]
    public string[] NameList { get; set; }
    /// <summary>布尔值类型,输入false,0,不输入都为false,其他都为true</summary>
    [ProtoMember(6)]
    public bool ValueBool { get; set; }
    /// <summary>布尔值列表类型混输入也可以,列表不输入就代表为元素数量为0个的布尔值列表</summary>
    [ProtoMember(7)]
    public bool[] ValueBoolList { get; set; }
}