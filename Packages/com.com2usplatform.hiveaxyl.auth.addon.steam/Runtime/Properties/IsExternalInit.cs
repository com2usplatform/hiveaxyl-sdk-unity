// Copyright (c) Com2uS Platform Corp. All rights reserved.

// Shim required for C# 9 record types when targeting Unity versions that do not
// include System.Runtime.CompilerServices.IsExternalInit in their BCL.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}
