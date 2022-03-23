﻿// Decompiled with JetBrains decompiler
// Type: libWiiSharp.NusClient
// Assembly: NUS Downloader, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: DDAF9FEC-76DE-4BD8-8A6D-D7CAD5827AC6
// Assembly location: C:\dotpeek\NUS Downloader.exe

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace libWiiSharp
{
  public class NusClient : IDisposable
  {
    private const string WII_NUS_URL = "http://ccs.cdn.wup.shop.nintendo.net/ccs/download/";
    private const string DSI_NUS_URL = "http://nus.cdn.t.shop.nintendowifi.net/ccs/download/";
    private const string WII_USER_AGENT = "wii libnup/1.0";
    private const string DSI_USER_AGENT = "Opera/9.50 (Nintendo; Opera/154; U; Nintendo DS; en)";
    private string nusUrl = "http://ccs.cdn.wup.shop.nintendo.net/ccs/download/";
    private WebClient wcNus = new WebClient();
    private bool useLocalFiles;
    private bool continueWithoutTicket;
    private int titleversion;
    private bool isDisposed;

    public int TitleVersion => this.titleversion;

    public bool UseLocalFiles
    {
      get => this.useLocalFiles;
      set => this.useLocalFiles = value;
    }

    public bool ContinueWithoutTicket
    {
      get => this.continueWithoutTicket;
      set => this.continueWithoutTicket = value;
    }

    ~NusClient() => this.Dispose(false);

    public void Dispose()
    {
      this.Dispose(true);
      GC.SuppressFinalize((object) this);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (disposing && !this.isDisposed)
        this.wcNus.Dispose();
      this.isDisposed = true;
    }

    public void ConfigureNusClient(WebClient wcReady) => this.wcNus = wcReady;

    public void SetToWiiServer()
    {
      this.nusUrl = "http://ccs.cdn.wup.shop.nintendo.net/ccs/download/";
      this.wcNus.Headers.Add("User-Agent", "wii libnup/1.0");
    }

    public void SetToDSiServer()
    {
      this.nusUrl = "http://nus.cdn.t.shop.nintendowifi.net/ccs/download/";
      this.wcNus.Headers.Add("User-Agent", "Opera/9.50 (Nintendo; Opera/154; U; Nintendo DS; en)");
    }

    public void DownloadTitle(
      string titleId,
      string titleVersion,
      string outputDir,
      bool alternative_path,
      string wadName,
      params StoreType[] storeTypes)
    {
      if (titleId.Length != 16)
        throw new Exception("Title ID must be 16 characters long!");
      this.downloadTitle(titleId, titleVersion, outputDir, alternative_path, wadName, storeTypes);
    }

    public TMD DownloadTMD(string titleId, string titleVersion) => titleId.Length == 16 ? this.downloadTmd(titleId, titleVersion) : throw new Exception("Title ID must be 16 characters long!");

    public Ticket DownloadTicket(string titleId) => titleId.Length == 16 ? this.downloadTicket(titleId) : throw new Exception("Title ID must be 16 characters long!");

    public byte[] DownloadSingleContent(string titleId, string titleVersion, string contentId)
    {
      if (titleId.Length != 16)
        throw new Exception("Title ID must be 16 characters long!");
      return this.downloadSingleContent(titleId, titleVersion, contentId);
    }

    public void DownloadSingleContent(
      string titleId,
      string titleVersion,
      string contentId,
      string savePath)
    {
      if (titleId.Length != 16)
        throw new Exception("Title ID must be 16 characters long!");
      if (!Directory.Exists(Path.GetDirectoryName(savePath)))
        Directory.CreateDirectory(Path.GetDirectoryName(savePath));
      if (System.IO.File.Exists(savePath))
        System.IO.File.Delete(savePath);
      byte[] bytes = this.downloadSingleContent(titleId, titleVersion, contentId);
      System.IO.File.WriteAllBytes(savePath, bytes);
    }

    private byte[] downloadSingleContent(string titleId, string titleVersion, string contentId)
    {
      uint num = uint.Parse(contentId, NumberStyles.HexNumber);
      contentId = num.ToString("x8");
      this.fireDebug("Downloading Content (Content ID: {0}) of Title {1} v{2}...", (object) contentId, (object) titleId, string.IsNullOrEmpty(titleVersion) ? (object) "[Latest]" : (object) titleVersion);
      this.fireDebug("   Checking for Internet connection...");
      if (!this.CheckInet())
      {
        this.fireDebug("   Connection not found...");
        throw new Exception("You're not connected to the internet!");
      }
      this.fireProgress(0);
      string str1 = "tmd" + (string.IsNullOrEmpty(titleVersion) ? string.Empty : string.Format(".{0}", (object) titleVersion));
      string str2 = string.Format("{0}{1}/", (object) this.nusUrl, (object) titleId);
      string empty = string.Empty;
      int contentIndex = 0;
      this.fireDebug("   Downloading TMD...");
      byte[] tmdFile = this.wcNus.DownloadData(str2 + str1);
      this.fireDebug("   Parsing TMD...");
      TMD tmd = TMD.Load(tmdFile);
      this.fireProgress(20);
      this.fireDebug("   Looking for Content ID {0} in TMD...", (object) contentId);
      bool flag = false;
      for (int index = 0; index < tmd.Contents.Length; ++index)
      {
        if ((int) tmd.Contents[index].ContentID == (int) num)
        {
          this.fireDebug("   Content ID {0} found in TMD...", (object) contentId);
          flag = true;
          empty = tmd.Contents[index].ContentID.ToString("x8");
          contentIndex = index;
          break;
        }
      }
      if (!flag)
      {
        this.fireDebug("   Content ID {0} wasn't found in TMD...", (object) contentId);
        throw new Exception("Content ID wasn't found in the TMD!");
      }
      this.fireDebug("   Downloading Ticket...");
      byte[] ticket = this.wcNus.DownloadData(str2 + "cetk");
      this.fireDebug("   Parsing Ticket...");
      Ticket tik = Ticket.Load(ticket);
      this.fireProgress(40);
      this.fireDebug("   Downloading Content... ({0} bytes)", (object) tmd.Contents[contentIndex].Size);
      byte[] content = this.wcNus.DownloadData(str2 + empty);
      this.fireProgress(80);
      this.fireDebug("   Decrypting Content...");
      byte[] array = this.decryptContent(content, contentIndex, tik, tmd);
      Array.Resize<byte>(ref array, (int) tmd.Contents[contentIndex].Size);
      if (!Shared.CompareByteArrays(SHA1.Create().ComputeHash(array), tmd.Contents[contentIndex].Hash))
      {
        this.fireDebug("/!\\ /!\\ /!\\ Hashes do not match /!\\ /!\\ /!\\");
        throw new Exception("Hashes do not match!");
      }
      this.fireProgress(100);
      this.fireDebug("Downloading Content (Content ID: {0}) of Title {1} v{2} Finished...", (object) contentId, (object) titleId, string.IsNullOrEmpty(titleVersion) ? (object) "[Latest]" : (object) titleVersion);
      return array;
    }

    private Ticket downloadTicket(string titleId)
    {
      if (!this.CheckInet())
        throw new Exception("You're not connected to the internet!");
      return Ticket.Load(this.wcNus.DownloadData(string.Format("{0}{1}/", (object) this.nusUrl, (object) titleId) + "cetk"));
    }

    private TMD downloadTmd(string titleId, string titleVersion)
    {
      if (!this.CheckInet())
        throw new Exception("You're not connected to the internet!");
      return TMD.Load(this.wcNus.DownloadData(string.Format("{0}{1}/", (object) this.nusUrl, (object) titleId) + ("tmd" + (string.IsNullOrEmpty(titleVersion) ? string.Empty : string.Format(".{0}", (object) titleVersion)))));
    }

    private void downloadTitle(
      string titleId,
      string titleVersion,
      string outputDir,
      bool alternative_path,
      string wadName,
      StoreType[] storeTypes)
    {
      this.fireDebug("Downloading Title {0} v{1}...", (object) titleId, string.IsNullOrEmpty(titleVersion) ? (object) "[Latest]" : (object) titleVersion);
      if (storeTypes.Length < 1)
      {
        this.fireDebug("  No store types were defined...");
        throw new Exception("You must at least define one store type!");
      }
      string path1 = string.Format("{0}{1}/", (object) this.nusUrl, (object) titleId);
      bool flag1 = false;
      bool flag2 = false;
      bool flag3 = false;
      this.fireProgress(0);
      foreach (int storeType in storeTypes)
      {
        switch (storeType)
        {
          case 0:
            this.fireDebug("    [=] Storing Encrypted Content...");
            flag1 = true;
            break;
          case 1:
            this.fireDebug("    [=] Storing Decrypted Content...");
            flag2 = true;
            break;
          case 2:
            this.fireDebug("    [=] Storing WAD...");
            flag3 = true;
            break;
          case 3:
            this.fireDebug("    [=] Storing Decrypted Content...");
            this.fireDebug("    [=] Storing Encrypted Content...");
            this.fireDebug("    [=] Storing WAD...");
            flag2 = true;
            flag1 = true;
            flag3 = true;
            break;
        }
      }
      if (!Directory.Exists(outputDir))
        Directory.CreateDirectory(outputDir);
      string path2 = "tmd" + (string.IsNullOrEmpty(titleVersion) ? string.Empty : string.Format(".{0}", (object) titleVersion));
      this.fireDebug("  - Downloading TMD...");
      byte[] tmdFile;
      TMD tmd;
      try
      {
        tmdFile = this.wcNus.DownloadData(path1 + path2);
        tmd = TMD.Load(tmdFile);
      }
      catch (Exception ex)
      {
        this.fireDebug("   + Downloading TMD Failed...");
        throw new Exception("Downloading TMD Failed:\n" + ex.Message);
      }
      if (alternative_path)
      {
        if (!Directory.Exists(Path.Combine(outputDir, titleId + "v" + (object) tmd.TitleVersion)))
          Directory.CreateDirectory(Path.Combine(outputDir, titleId + "v" + (object) tmd.TitleVersion));
        outputDir = Path.Combine(outputDir, titleId + "v" + (object) tmd.TitleVersion);
      }
      else
      {
        if (!Directory.Exists(Path.Combine(outputDir, titleId)))
          Directory.CreateDirectory(Path.Combine(outputDir, titleId));
        outputDir = Path.Combine(outputDir, titleId);
      }
      this.fireDebug("  - Parsing TMD...");
      if (string.IsNullOrEmpty(titleVersion))
        this.fireDebug("    + Title Version: {0}", (object) tmd.TitleVersion);
      this.fireDebug("    + {0} Contents", (object) tmd.NumOfContents);
      if (!alternative_path)
      {
        if (!Directory.Exists(Path.Combine(outputDir, tmd.TitleVersion.ToString())))
          Directory.CreateDirectory(Path.Combine(outputDir, tmd.TitleVersion.ToString()));
        outputDir = Path.Combine(outputDir, tmd.TitleVersion.ToString());
      }
      this.titleversion = (int) tmd.TitleVersion;
      System.IO.File.WriteAllBytes(Path.Combine(outputDir, path2), tmd.ToByteArray());
      this.fireProgress(5);
      this.fireDebug("  - Downloading Ticket...");
      try
      {
        this.wcNus.DownloadFile(Path.Combine(path1, "cetk"), Path.Combine(outputDir, "cetk"));
      }
      catch (Exception ex)
      {
        if (!this.continueWithoutTicket || !flag1)
        {
          this.fireDebug("   + Downloading Ticket Failed...");
          throw new Exception("Downloading Ticket Failed:\n" + ex.Message);
        }
        if (!System.IO.File.Exists(Path.Combine(outputDir, "cetk")))
        {
          flag2 = false;
          flag3 = false;
        }
      }
      this.fireProgress(10);
      Ticket tik = new Ticket();
      if (System.IO.File.Exists(Path.Combine(outputDir, "cetk")))
      {
        this.fireDebug("   + Parsing Ticket...");
        tik = Ticket.Load(Path.Combine(outputDir, "cetk"));
        if (this.nusUrl == "http://nus.cdn.t.shop.nintendowifi.net/ccs/download/")
          tik.DSiTicket = true;
      }
      else
        this.fireDebug("   + Ticket Unavailable...");
      string[] strArray = new string[(int) tmd.NumOfContents];
      for (int index = 0; index < (int) tmd.NumOfContents; ++index)
      {
        this.fireDebug("  - Downloading Content #{0} of {1}... ({2} bytes)", (object) (index + 1), (object) tmd.NumOfContents, (object) tmd.Contents[index].Size);
        this.fireProgress((index + 1) * 60 / (int) tmd.NumOfContents + 10);
        if (this.useLocalFiles)
        {
          if (System.IO.File.Exists(Path.Combine(outputDir, tmd.Contents[index].ContentID.ToString("x8"))))
          {
            this.fireDebug("   + Using Local File, Skipping...");
            continue;
          }
        }
        try
        {
          this.wcNus.DownloadFile(path1 + tmd.Contents[index].ContentID.ToString("x8"), Path.Combine(outputDir, tmd.Contents[index].ContentID.ToString("x8")));
          strArray[index] = tmd.Contents[index].ContentID.ToString("x8");
        }
        catch (Exception ex)
        {
          this.fireDebug("  - Downloading Content #{0} of {1} failed...", (object) (index + 1), (object) tmd.NumOfContents);
          throw new Exception("Downloading Content Failed:\n" + ex.Message);
        }
      }
      if (flag2 || flag3)
      {
        SHA1 shA1 = SHA1.Create();
        for (int contentIndex = 0; contentIndex < (int) tmd.NumOfContents; ++contentIndex)
        {
          this.fireDebug("  - Decrypting Content #{0} of {1}...", (object) (contentIndex + 1), (object) tmd.NumOfContents);
          this.fireProgress((contentIndex + 1) * 20 / (int) tmd.NumOfContents + 75);
          byte[] array = this.decryptContent(System.IO.File.ReadAllBytes(Path.Combine(outputDir, tmd.Contents[contentIndex].ContentID.ToString("x8"))), contentIndex, tik, tmd);
          Array.Resize<byte>(ref array, (int) tmd.Contents[contentIndex].Size);
          if (!Shared.CompareByteArrays(shA1.ComputeHash(array), tmd.Contents[contentIndex].Hash))
            this.fireDebug("   + Hashes do not match! (Invalid Output)");
          System.IO.File.WriteAllBytes(Path.Combine(outputDir, tmd.Contents[contentIndex].ContentID.ToString("x8") + ".app"), array);
        }
        shA1.Clear();
      }
      if (flag3)
      {
        this.fireDebug("  - Building Certificate Chain...");
        CertificateChain cert = CertificateChain.FromTikTmd(Path.Combine(outputDir, "cetk"), tmdFile);
        byte[][] contents = new byte[(int) tmd.NumOfContents][];
        for (int index = 0; index < (int) tmd.NumOfContents; ++index)
          contents[index] = System.IO.File.ReadAllBytes(Path.Combine(outputDir, tmd.Contents[index].ContentID.ToString("x8") + ".app"));
        this.fireDebug("  - Creating WAD...");
        WAD wad = WAD.Create(cert, tik, tmd, contents);
        wad.RemoveFooter();
        wadName = wadName.Replace("[v]", "v" + this.TitleVersion.ToString());
        if (Path.DirectorySeparatorChar.ToString() != "/" && Path.AltDirectorySeparatorChar.ToString() != "/")
          wadName = wadName.Replace("/", "");
        if (wadName.Contains(Path.DirectorySeparatorChar.ToString()) || wadName.Contains(Path.AltDirectorySeparatorChar.ToString()))
          wad.Save(wadName);
        else
          wad.Save(Path.Combine(outputDir, wadName));
      }
      if (!flag1)
      {
        this.fireDebug("  - Deleting Encrypted Contents...");
        for (int index = 0; index < strArray.Length; ++index)
        {
          if (System.IO.File.Exists(Path.Combine(outputDir, strArray[index])))
            System.IO.File.Delete(Path.Combine(outputDir, strArray[index]));
        }
      }
      if (flag3 && !flag2)
      {
        this.fireDebug("  - Deleting Decrypted Contents...");
        for (int index = 0; index < strArray.Length; ++index)
        {
          if (System.IO.File.Exists(Path.Combine(outputDir, strArray[index] + ".app")))
            System.IO.File.Delete(Path.Combine(outputDir, strArray[index] + ".app"));
        }
      }
      if (!flag2 && !flag1)
      {
        this.fireDebug("  - Deleting TMD and Ticket...");
        System.IO.File.Delete(Path.Combine(outputDir, path2));
        System.IO.File.Delete(Path.Combine(outputDir, "cetk"));
      }
      this.fireDebug("Downloading Title {0} v{1} Finished...", (object) titleId, (object) tmd.TitleVersion);
      this.fireProgress(100);
    }

    private byte[] decryptContent(byte[] content, int contentIndex, Ticket tik, TMD tmd)
    {
      Array.Resize<byte>(ref content, Shared.AddPadding(content.Length, 16));
      byte[] titleKey = tik.TitleKey;
      byte[] numArray = new byte[16];
      byte[] bytes = BitConverter.GetBytes(tmd.Contents[contentIndex].Index);
      numArray[0] = bytes[1];
      numArray[1] = bytes[0];
      RijndaelManaged rijndaelManaged = new RijndaelManaged();
      rijndaelManaged.Mode = CipherMode.CBC;
      rijndaelManaged.Padding = PaddingMode.None;
      rijndaelManaged.KeySize = 128;
      rijndaelManaged.BlockSize = 128;
      rijndaelManaged.Key = titleKey;
      rijndaelManaged.IV = numArray;
      ICryptoTransform decryptor = rijndaelManaged.CreateDecryptor();
      MemoryStream memoryStream = new MemoryStream(content);
      CryptoStream cryptoStream = new CryptoStream((Stream) memoryStream, decryptor, CryptoStreamMode.Read);
      byte[] buffer = new byte[content.Length];
      cryptoStream.Read(buffer, 0, buffer.Length);
      cryptoStream.Dispose();
      memoryStream.Dispose();
      return buffer;
    }

    private bool CheckInet()
    {
      try
      {
        Dns.GetHostEntry("www.google.com");
        return true;
      }
      catch
      {
        return false;
      }
    }

    public event EventHandler<ProgressChangedEventArgs> Progress;

    public event EventHandler<MessageEventArgs> Debug;

    private void fireDebug(string debugMessage, params object[] args)
    {
      EventHandler<MessageEventArgs> debug = this.Debug;
      if (debug == null)
        return;
      debug(new object(), new MessageEventArgs(string.Format(debugMessage, args)));
    }

    private void fireProgress(int progressPercentage)
    {
      EventHandler<ProgressChangedEventArgs> progress = this.Progress;
      if (progress == null)
        return;
      progress(new object(), new ProgressChangedEventArgs(progressPercentage, (object) string.Empty));
    }
  }
}
