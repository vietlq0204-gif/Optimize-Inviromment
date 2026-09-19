using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class BakedGrassPayload
{
    public const int Magic = 0x32475242; // BGR2
    public const int Version = 1;
    public const string FileExtension = ".bytes";

    public readonly struct Record
    {
        public Record(int batchIndex, Matrix4x4[] matrices)
        {
            BatchIndex = batchIndex;
            Matrices = matrices;
        }

        public int BatchIndex { get; }
        public Matrix4x4[] Matrices { get; }
    }

    public static void WriteHeader(BinaryWriter writer)
    {
        writer.Write(Magic);
        writer.Write(Version);
    }

    public static void WriteRecord(BinaryWriter writer, int batchIndex, IReadOnlyList<Matrix4x4> matrices)
    {
        if (matrices == null || matrices.Count == 0)
        {
            return;
        }

        writer.Write(batchIndex);
        writer.Write(matrices.Count);
        for (int i = 0; i < matrices.Count; i++)
        {
            WriteMatrix(writer, matrices[i]);
        }
    }

    public static List<Record> ReadRecords(byte[] bytes)
    {
        List<Record> records = new();
        if (bytes == null || bytes.Length == 0)
        {
            return records;
        }

        using MemoryStream stream = new(bytes);
        using BinaryReader reader = new(stream);

        int magic = reader.ReadInt32();
        int version = reader.ReadInt32();
        if (magic != Magic || version != Version)
        {
            throw new InvalidDataException("Baked grass payload version is not supported.");
        }

        while (stream.Position < stream.Length)
        {
            int batchIndex = reader.ReadInt32();
            int count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException("Baked grass payload contains a negative matrix count.");
            }

            Matrix4x4[] matrices = new Matrix4x4[count];
            for (int i = 0; i < count; i++)
            {
                matrices[i] = ReadMatrix(reader);
            }

            records.Add(new Record(batchIndex, matrices));
        }

        return records;
    }

    private static void WriteMatrix(BinaryWriter writer, Matrix4x4 matrix)
    {
        writer.Write(matrix.m00);
        writer.Write(matrix.m01);
        writer.Write(matrix.m02);
        writer.Write(matrix.m03);
        writer.Write(matrix.m10);
        writer.Write(matrix.m11);
        writer.Write(matrix.m12);
        writer.Write(matrix.m13);
        writer.Write(matrix.m20);
        writer.Write(matrix.m21);
        writer.Write(matrix.m22);
        writer.Write(matrix.m23);
        writer.Write(matrix.m30);
        writer.Write(matrix.m31);
        writer.Write(matrix.m32);
        writer.Write(matrix.m33);
    }

    private static Matrix4x4 ReadMatrix(BinaryReader reader)
    {
        Matrix4x4 matrix = default;
        matrix.m00 = reader.ReadSingle();
        matrix.m01 = reader.ReadSingle();
        matrix.m02 = reader.ReadSingle();
        matrix.m03 = reader.ReadSingle();
        matrix.m10 = reader.ReadSingle();
        matrix.m11 = reader.ReadSingle();
        matrix.m12 = reader.ReadSingle();
        matrix.m13 = reader.ReadSingle();
        matrix.m20 = reader.ReadSingle();
        matrix.m21 = reader.ReadSingle();
        matrix.m22 = reader.ReadSingle();
        matrix.m23 = reader.ReadSingle();
        matrix.m30 = reader.ReadSingle();
        matrix.m31 = reader.ReadSingle();
        matrix.m32 = reader.ReadSingle();
        matrix.m33 = reader.ReadSingle();
        return matrix;
    }
}
