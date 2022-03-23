﻿// Decompiled with JetBrains decompiler
// Type: libWiiSharp.Lz77
// Assembly: NUS Downloader, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: DDAF9FEC-76DE-4BD8-8A6D-D7CAD5827AC6
// Assembly location: C:\dotpeek\NUS Downloader.exe

using System;
using System.IO;

namespace libWiiSharp
{
  public class Lz77
  {
    private const int N = 4096;
    private const int F = 18;
    private const int threshold = 2;
    private static uint lz77Magic = 1280980791;
    private int[] leftSon = new int[4097];
    private int[] rightSon = new int[4353];
    private int[] dad = new int[4097];
    private ushort[] textBuffer = new ushort[4113];
    private int matchPosition;
    private int matchLength;

    public static uint Lz77Magic => Lz77.lz77Magic;

    public static bool IsLz77Compressed(string file) => Lz77.IsLz77Compressed(File.ReadAllBytes(file));

    public static bool IsLz77Compressed(byte[] file)
    {
      Headers.HeaderType startIndex = Headers.DetectHeader(file);
      return (int) Shared.Swap(BitConverter.ToUInt32(file, (int) startIndex)) == (int) Lz77.lz77Magic;
    }

    public static bool IsLz77Compressed(Stream file)
    {
      Headers.HeaderType offset = Headers.DetectHeader(file);
      byte[] buffer = new byte[4];
      file.Seek((long) offset, SeekOrigin.Begin);
      file.Read(buffer, 0, buffer.Length);
      return (int) Shared.Swap(BitConverter.ToUInt32(buffer, 0)) == (int) Lz77.lz77Magic;
    }

    public void Compress(string inFile, string outFile)
    {
      Stream stream;
      using (FileStream inFile1 = new FileStream(inFile, FileMode.Open))
        stream = this.compress((Stream) inFile1);
      byte[] buffer = new byte[stream.Length];
      stream.Read(buffer, 0, buffer.Length);
      if (File.Exists(outFile))
        File.Delete(outFile);
      using (FileStream fileStream = new FileStream(outFile, FileMode.Create))
        fileStream.Write(buffer, 0, buffer.Length);
    }

    public byte[] Compress(byte[] file) => ((MemoryStream) this.compress((Stream) new MemoryStream(file))).ToArray();

    public Stream Compress(Stream file) => this.compress(file);

    public void Decompress(string inFile, string outFile)
    {
      Stream stream;
      using (FileStream inFile1 = new FileStream(inFile, FileMode.Open))
        stream = this.decompress((Stream) inFile1);
      byte[] buffer = new byte[stream.Length];
      stream.Read(buffer, 0, buffer.Length);
      if (File.Exists(outFile))
        File.Delete(outFile);
      using (FileStream fileStream = new FileStream(outFile, FileMode.Create))
        fileStream.Write(buffer, 0, buffer.Length);
    }

    public byte[] Decompress(byte[] file) => ((MemoryStream) this.decompress((Stream) new MemoryStream(file))).ToArray();

    public Stream Decompress(Stream file) => this.decompress(file);

    private Stream decompress(Stream inFile)
    {
      if (!Lz77.IsLz77Compressed(inFile))
        return inFile;
      inFile.Seek(0L, SeekOrigin.Begin);
      uint num1 = 0;
      Headers.HeaderType offset = Headers.DetectHeader(inFile);
      byte[] buffer = new byte[8];
      inFile.Seek((long) offset, SeekOrigin.Begin);
      inFile.Read(buffer, 0, 8);
      if ((int) Shared.Swap(BitConverter.ToUInt32(buffer, 0)) != (int) Lz77.lz77Magic)
      {
        inFile.Dispose();
        throw new Exception("Invaild Magic!");
      }
      if (buffer[4] != (byte) 16)
      {
        inFile.Dispose();
        throw new Exception("Unsupported Compression Type!");
      }
      uint num2 = BitConverter.ToUInt32(buffer, 4) >> 8;
      for (int index = 0; index < 4078; ++index)
        this.textBuffer[index] = (ushort) 223;
      int num3 = 4078;
      uint num4 = 7;
      int num5 = 7;
      MemoryStream memoryStream = new MemoryStream();
label_10:
      while (true)
      {
        num4 <<= 1;
        ++num5;
        if (num5 == 8)
        {
          int num6;
          if ((num6 = inFile.ReadByte()) != -1)
          {
            num4 = (uint) num6;
            num5 = 0;
          }
          else
            goto label_24;
        }
        if (((int) num4 & 128) == 0)
        {
          int num7;
          if ((long) (num7 = inFile.ReadByte()) != inFile.Length - 1L)
          {
            if (num1 < num2)
              memoryStream.WriteByte((byte) num7);
            ushort[] textBuffer = this.textBuffer;
            int index = num3;
            int num8 = index + 1;
            int num9 = (int) (byte) num7;
            textBuffer[index] = (ushort) num9;
            num3 = num8 & 4095;
            ++num1;
          }
          else
            goto label_24;
        }
        else
          break;
      }
      int num10;
      int num11;
      if ((num10 = inFile.ReadByte()) != -1 && (num11 = inFile.ReadByte()) != -1)
      {
        int num12 = num11 | num10 << 8 & 3840;
        int num13 = (num10 >> 4 & 15) + 2;
        for (int index1 = 0; index1 <= num13; ++index1)
        {
          int num14 = (int) this.textBuffer[num3 - num12 - 1 & 4095];
          if (num1 < num2)
            memoryStream.WriteByte((byte) num14);
          ushort[] textBuffer = this.textBuffer;
          int index2 = num3;
          int num15 = index2 + 1;
          int num16 = (int) (byte) num14;
          textBuffer[index2] = (ushort) num16;
          num3 = num15 & 4095;
          ++num1;
        }
        goto label_10;
      }
label_24:
      return (Stream) memoryStream;
    }

    private Stream compress(Stream inFile)
    {
      if (Lz77.IsLz77Compressed(inFile))
        return inFile;
      inFile.Seek(0L, SeekOrigin.Begin);
      int num1 = 0;
      int[] numArray1 = new int[17];
      uint num2 = (uint) (((int) Convert.ToUInt32(inFile.Length) << 8) + 16);
      MemoryStream memoryStream = new MemoryStream();
      memoryStream.Write(BitConverter.GetBytes(Shared.Swap(Lz77.lz77Magic)), 0, 4);
      memoryStream.Write(BitConverter.GetBytes(num2), 0, 4);
      this.InitTree();
      numArray1[0] = 0;
      int num3 = 1;
      int num4 = 128;
      int p = 0;
      int r = 4078;
      for (int index = p; index < r; ++index)
        this.textBuffer[index] = ushort.MaxValue;
      int num5;
      int num6;
      for (num5 = 0; num5 < 18 && (num6 = inFile.ReadByte()) != -1; ++num5)
        this.textBuffer[r + num5] = (ushort) num6;
      if (num5 == 0)
        return inFile;
      for (int index = 1; index <= 18; ++index)
        this.InsertNode(r - index);
      this.InsertNode(r);
      do
      {
        if (this.matchLength > num5)
          this.matchLength = num5;
        if (this.matchLength <= 2)
        {
          this.matchLength = 1;
          numArray1[num3++] = (int) this.textBuffer[r];
        }
        else
        {
          numArray1[0] |= num4;
          int[] numArray2 = numArray1;
          int index1 = num3;
          int num7 = index1 + 1;
          int num8 = (int) (ushort) (r - this.matchPosition - 1 >> 8 & 15) | this.matchLength - 3 << 4;
          numArray2[index1] = num8;
          int[] numArray3 = numArray1;
          int index2 = num7;
          num3 = index2 + 1;
          int num9 = (int) (ushort) (r - this.matchPosition - 1 & (int) byte.MaxValue);
          numArray3[index2] = num9;
        }
        if ((num4 >>= 1) == 0)
        {
          for (int index = 0; index < num3; ++index)
            memoryStream.WriteByte((byte) numArray1[index]);
          num1 += num3;
          numArray1[0] = 0;
          num3 = 1;
          num4 = 128;
        }
        int matchLength = this.matchLength;
        int num10;
        int num11;
        for (num10 = 0; num10 < matchLength && (num11 = inFile.ReadByte()) != -1; ++num10)
        {
          this.DeleteNode(p);
          this.textBuffer[p] = (ushort) num11;
          if (p < 17)
            this.textBuffer[p + 4096] = (ushort) num11;
          p = p + 1 & 4095;
          r = r + 1 & 4095;
          this.InsertNode(r);
        }
        while (num10++ < matchLength)
        {
          this.DeleteNode(p);
          p = p + 1 & 4095;
          r = r + 1 & 4095;
          if (--num5 != 0)
            this.InsertNode(r);
        }
      }
      while (num5 > 0);
      if (num3 > 1)
      {
        for (int index = 0; index < num3; ++index)
          memoryStream.WriteByte((byte) numArray1[index]);
        num1 += num3;
      }
      if (num1 % 4 != 0)
      {
        for (int index = 0; index < 4 - num1 % 4; ++index)
          memoryStream.WriteByte((byte) 0);
      }
      return (Stream) memoryStream;
    }

    private void InitTree()
    {
      for (int index = 4097; index <= 4352; ++index)
        this.rightSon[index] = 4096;
      for (int index = 0; index < 4096; ++index)
        this.dad[index] = 4096;
    }

    private void InsertNode(int r)
    {
      int num1 = 1;
      int index = 4097 + (this.textBuffer[r] == ushort.MaxValue ? 0 : (int) this.textBuffer[r]);
      this.rightSon[r] = this.leftSon[r] = 4096;
      this.matchLength = 0;
      int num2;
      do
      {
        do
        {
          if (num1 >= 0)
          {
            if (this.rightSon[index] != 4096)
            {
              index = this.rightSon[index];
            }
            else
            {
              this.rightSon[index] = r;
              this.dad[r] = index;
              return;
            }
          }
          else if (this.leftSon[index] != 4096)
          {
            index = this.leftSon[index];
          }
          else
          {
            this.leftSon[index] = r;
            this.dad[r] = index;
            return;
          }
          num2 = 1;
          while (num2 < 18 && (num1 = (int) this.textBuffer[r + num2] - (int) this.textBuffer[index + num2]) == 0)
            ++num2;
        }
        while (num2 <= this.matchLength);
        this.matchPosition = index;
      }
      while ((this.matchLength = num2) < 18);
      this.dad[r] = this.dad[index];
      this.leftSon[r] = this.leftSon[index];
      this.rightSon[r] = this.rightSon[index];
      this.dad[this.leftSon[index]] = r;
      this.dad[this.rightSon[index]] = r;
      if (this.rightSon[this.dad[index]] == index)
        this.rightSon[this.dad[index]] = r;
      else
        this.leftSon[this.dad[index]] = r;
      this.dad[index] = 4096;
    }

    private void DeleteNode(int p)
    {
      if (this.dad[p] == 4096)
        return;
      int index;
      if (this.rightSon[p] == 4096)
        index = this.leftSon[p];
      else if (this.leftSon[p] == 4096)
      {
        index = this.rightSon[p];
      }
      else
      {
        index = this.leftSon[p];
        if (this.rightSon[index] != 4096)
        {
          do
          {
            index = this.rightSon[index];
          }
          while (this.rightSon[index] != 4096);
          this.rightSon[this.dad[index]] = this.leftSon[index];
          this.dad[this.leftSon[index]] = this.dad[index];
          this.leftSon[index] = this.leftSon[p];
          this.dad[this.leftSon[p]] = index;
        }
        this.rightSon[index] = this.rightSon[p];
        this.dad[this.rightSon[p]] = index;
      }
      this.dad[index] = this.dad[p];
      if (this.rightSon[this.dad[p]] == p)
        this.rightSon[this.dad[p]] = index;
      else
        this.leftSon[this.dad[p]] = index;
      this.dad[p] = 4096;
    }
  }
}
