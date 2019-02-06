using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using DBreeze;
using DBreeze.Utils;
using Newtonsoft.Json;

namespace BRhodium.Node.Utilities
{
    public sealed class DBreezeStorage<T> where T : new()
    {
        /// <summary> Gets the folder path. </summary>
        public string FolderPath { get; }
        public string DatabaseName { get; }
        private readonly DBreezeEngine dbreeze;
        private readonly object optimizeLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="DBreezeStorage{T}"/> class.
        /// </summary>
        /// <param name="folderPath">The path of the folder in which the files are to be stored.</param>
        public DBreezeStorage(string folderPath, string databaseName)
        {
            Guard.NotEmpty(folderPath, nameof(folderPath));

            this.FolderPath = Path.Combine(folderPath, databaseName);
            this.DatabaseName = databaseName;

            // Create a folder if none exists.
            if (!Directory.Exists(this.FolderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            if (this.dbreeze == null)
                this.dbreeze = new DBreezeEngine(this.FolderPath);
        }

        /// <summary>
        /// Saves an object to a file, optionally keeping a backup of it.
        /// </summary>
        /// <param name="toSave">Object to save as a file.</param>
        /// <param name="idKey">Name of the key to be saved.</param>
        /// <param name="saveBackupFile">A value indicating whether to save a backup of the file.</param>
        public void SaveToStorage(T toSave, string idKey, NBitcoin.Network network)
        {
            Guard.NotEmpty(idKey, nameof(idKey));
            Guard.NotNull(toSave, nameof(toSave));

            MemoryStream memorystream = new MemoryStream();
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(memorystream, toSave);
            byte[] byteObject = memorystream.ToArray();

            lock (this.optimizeLock)
            {
                using (var transaction = this.dbreeze.GetTransaction())
                {
                    transaction.Technical_SetTable_OverwriteIsNotAllowed(this.DatabaseName);
                    transaction.Insert<string, byte[]>(this.DatabaseName, idKey, byteObject);
                    transaction.Commit();
                }
            }
        }

        /// <summary>
        /// Optimize storage
        /// </summary>
        public void OptimizeStorage()
        {
            lock (this.optimizeLock)
            {
                using (var transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(this.DatabaseName, "optimizedTable");
                    foreach (var row in transaction.SelectForward<string, byte[]>(this.DatabaseName))
                    {
                        transaction.Technical_SetTable_OverwriteIsNotAllowed("optimizedTable");
                        transaction.Insert("optimizedTable", row.Key, row.Value);
                    }

                    transaction.Commit();
                }

                this.dbreeze.Scheme.DeleteTable(this.DatabaseName);
                this.dbreeze.Scheme.RenameTable("optimizedTable", this.DatabaseName);
            }
        }

        /// <summary>
        /// Checks whether a file with the specified name exists in the folder.
        /// </summary>
        /// <param name="idKey">The name of key to look for.</param>
        /// <returns>A value indicating whether the file exists in the file system.</returns>
        public bool Exists(string idKey)
        {
            using (var transaction = this.dbreeze.GetTransaction())
            {
                var row = transaction.Select<string, string>(this.DatabaseName, idKey);
                if (row.Exists)
                {
                    return true;
                }
            }

            return false;
        }

        public IEnumerable<string> GetDatabaseKeys()
        {
            var keys = new List<string>();

            using (var transaction = this.dbreeze.GetTransaction())
            {
                foreach (var row in transaction.SelectForward<string, byte[]>(this.DatabaseName))
                {
                    keys.Add(row.Key);
                }
            }

            return keys;
        }

        public T LoadByKey(string idKey, NBitcoin.Network network)
        {
            Guard.NotEmpty(idKey, nameof(idKey));

            using (var transaction = this.dbreeze.GetTransaction())
            {
                var row = transaction.Select<string, byte[]>(this.DatabaseName, idKey);
                if (row.Exists)
                {
                    return default(T);
                } else
                {
                    MemoryStream memorystream = new MemoryStream(row.Value);
                    BinaryFormatter bf = new BinaryFormatter();
                    return (T)bf.Deserialize(memorystream);
                }
            }
        }

        public IEnumerable<T> LoadAll(NBitcoin.Network network)
        {
            var objects = new List<T>();

            var serializer = new DBreezeSerializer();
            serializer.Initialize(network);

            using (var transaction = this.dbreeze.GetTransaction())
            {
                foreach (var row in transaction.SelectForward<string, byte[]>(this.DatabaseName))
                {
                    MemoryStream memorystream = new MemoryStream(row.Value);
                    BinaryFormatter bf = new BinaryFormatter();
                    objects.Add((T)bf.Deserialize(memorystream));
                }
            }

            return objects;
        }
    }
}
