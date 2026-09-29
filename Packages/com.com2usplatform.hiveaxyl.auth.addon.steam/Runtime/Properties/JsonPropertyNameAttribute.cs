// Copyright (c) Com2uS Platform Corp. All rights reserved.

// Compile-time shim for System.Text.Json's [JsonPropertyName] attribute, which Unity's
// BCL does not include. No type in this package currently applies the attribute.
// The Steam addon uses native Steamworks API — JSON serialization never runs at runtime.
// The shim has no runtime effect.
namespace System.Text.Json.Serialization
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    internal sealed class JsonPropertyNameAttribute : Attribute
    {
        public JsonPropertyNameAttribute(string name) { }
    }
}
