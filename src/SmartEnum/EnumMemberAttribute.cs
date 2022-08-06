using System;

namespace Ardalis.SmartEnum
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class EnumMemberAttribute : Attribute
    {
    }
}
