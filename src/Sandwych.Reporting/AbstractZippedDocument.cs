﻿//2010-09-02
using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Threading.Tasks;
using Fluid;
using System.Collections.Concurrent;
using System.IO.Compression;

namespace Sandwych.Reporting
{
    public abstract class AbstractZippedDocument : IDocument
    {
        private readonly ConcurrentDictionary<string, byte[]> _documentEntries = new ConcurrentDictionary<string, byte[]>();

        public IDictionary<string, byte[]> Entries => _documentEntries;

        public void Load(Stream inStream) =>
            this.LoadAsync(inStream).GetAwaiter().GetResult();

        public async Task LoadAsync(Stream inStream)
        {
            if (inStream == null)
            {
                throw new ArgumentNullException(nameof(inStream));
            }

            _documentEntries.Clear();

            // Load zipped content into the memory
            using (var archive = new ZipArchive(inStream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry ze in archive.Entries)
                {
                    using (var zs = ze.Open())
                    {
                        var buf = new byte[ze.Length];
                        var nread = await zs.ReadAsync(buf, 0, (int)ze.Length);
                        if (nread != ze.Length)
                        {
                            throw new IOException("Failed to read zip entry: " + ze.FullName);
                        }
                        _documentEntries[ze.FullName] = buf;
                    }
                }
            }
        }

        public virtual async Task SaveAsync(Stream outStream)
        {
            using (var zip = new ZipArchive(outStream, ZipArchiveMode.Create))
            {
                foreach (var item in _documentEntries)
                {
                    await this.AppendZipEntryAsync(zip, item.Key);
                }
            }
        }

        public virtual void Save(Stream outStream) =>
            this.SaveAsync(outStream).GetAwaiter().GetResult();


        public async Task AppendZipEntryAsync(ZipArchive archive, string name)
        {
            Debug.Assert(archive != null);
            Debug.Assert(!string.IsNullOrEmpty(name));
            Debug.Assert(this._documentEntries.ContainsKey(name));

            var data = this._documentEntries[name];

            var extensionName = Path.GetExtension(name).ToUpperInvariant();
            var cl = CompressionLevel.Fastest;
            switch (extensionName)
            {
                case "JPEG":
                case "JPG":
                case "PNG":
                case "MP3":
                case "MP4":
                    cl = CompressionLevel.NoCompression;
                    break;

                default:
                    cl = CompressionLevel.Fastest;
                    break;
            }
            var zae = archive.CreateEntry(name, cl);
            using (var zs = zae.Open())
            {
                await zs.WriteAsync(data, 0, data.Length);
            }
        }

        public void AppendZipEntry(ZipArchive archive, string name)
        {
            this.AppendZipEntryAsync(archive, name).GetAwaiter().GetResult();
        }

        public ICollection<string> EntryPaths
        {
            get { return this._documentEntries.Keys; }
        }

        public Stream GetEntryInputStream(string entryPath)
        {
            if (string.IsNullOrEmpty(entryPath))
            {
                throw new ArgumentNullException(nameof(entryPath));
            }

            var data = this._documentEntries[entryPath];
            return new MemoryStream(data);
        }

        public Stream GetEntryOutputStream(string entryPath)
        {
            if (string.IsNullOrEmpty(entryPath))
            {
                throw new ArgumentNullException(nameof(entryPath));
            }
            var oms = new OutputMemoryStream(entryPath, this._documentEntries);
            return oms;
        }

        public bool EntryExists(string entryPath)
        {
            if (string.IsNullOrEmpty(entryPath))
            {
                throw new ArgumentNullException(nameof(entryPath));
            }
            return this._documentEntries.ContainsKey(entryPath);
        }

        public virtual byte[] GetBuffer()
        {
            using (var ms = new MemoryStream())
            {
                //this.Save(ms);
                return ms.ToArray();
            }
        }

        public string ToBase64String()
        {
            return Convert.ToBase64String(this.GetBuffer());
        }

        protected static void CopyStream(Stream src, Stream dest)
        {
            if (src == null)
            {
                throw new ArgumentNullException("src");
            }

            if (dest == null)
            {
                throw new ArgumentNullException("dest");
            }

            var bufSize = 2048;
            var buf = new byte[bufSize];
            int nRead = 0;
            while ((nRead = src.Read(buf, 0, bufSize)) > 0)
            {
                dest.Write(buf, 0, nRead);
            }
        }

        public void CopyTo(AbstractZippedDocument destDoc)
        {
            if (destDoc == null)
            {
                throw new ArgumentNullException("destDoc");
            }

            foreach (var item in this.EntryPaths)
            {
                using (var inStream = this.GetEntryInputStream(item))
                using (var outStream = destDoc.GetEntryOutputStream(item))
                {
                    CopyStream(inStream, outStream);
                }
            }
        }

    }
}