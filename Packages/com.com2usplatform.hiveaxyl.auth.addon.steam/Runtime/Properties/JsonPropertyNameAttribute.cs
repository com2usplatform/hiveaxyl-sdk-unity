// Copyright (c) Com2uS Platform Corp. All rights reserved.

// Shim required for the [JsonPropertyName] attribute emitted by hive-codegen on all DTOs.
// The Steam addon uses native Steamworks API — JSON serialization never runs at runtime.
// This shim exists only to satisfy the compiler; it has no runtime effect.
namespace System.Text.Json.Serialization
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    internal sealed class JsonPropertyNameAttribute : Attribute
    {
        public JsonPropertyNameAttribute(string name) { }
    }
}
