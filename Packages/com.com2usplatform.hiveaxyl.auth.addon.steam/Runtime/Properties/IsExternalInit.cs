// Copyright (c) Com2uS Platform Corp. All rights reserved.

// Compile-time shim that lets C# 9 record types and init accessors compile on Unity
// versions that do not include System.Runtime.CompilerServices.IsExternalInit in their
// BCL. No type in this package currently uses them; the shim has no runtime effect.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}
