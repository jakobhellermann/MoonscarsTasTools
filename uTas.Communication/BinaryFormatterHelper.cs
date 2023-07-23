using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

#pragma warning disable SYSLIB0011 // TODO migrate away

namespace MoonscarsTASTools.uTas.Communication;

public static class BinaryFormatterHelper {
    private static readonly BinaryFormatter BinaryFormatter = new();

    public static T FromByteArray<T>(byte[] data, int offset = 0, int length = 0) {
        if (length == 0) length = data.Length - offset;

        using MemoryStream ms = new(data, offset, length);
        var obj = BinaryFormatter.Deserialize(ms);
        return (T)obj;
    }

    public static byte[] ToByteArray<T>(T obj) {
        if (obj == null) return Array.Empty<byte>();

        using MemoryStream ms = new();
        BinaryFormatter.Serialize(ms, obj);
        return ms.ToArray();
    }
}