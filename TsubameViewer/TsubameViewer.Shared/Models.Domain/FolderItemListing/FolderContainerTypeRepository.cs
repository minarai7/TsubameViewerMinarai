﻿using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Infrastructure;
using Windows.Storage;

namespace TsubameViewer.Models.Domain.FolderItemListing
{
    public enum FolderContainerType
    {
        OnlyImages,
        Other,
    }

    public sealed class FolderContainerTypeManager
    {
        private readonly FolderContainerTypeRepository _folderContainerTypeRepository;

        public FolderContainerTypeManager(FolderContainerTypeRepository folderContainerTypeRepository)
        {
            _folderContainerTypeRepository = folderContainerTypeRepository;
        }

        public async ValueTask<FolderContainerType> GetFolderContainerTypeWithCacheAsync(StorageFolder folder, CancellationToken ct)
        {
            var containerType = _folderContainerTypeRepository.GetContainerType(folder.Path);
            
            if (containerType != null) { return containerType.Value; }

            return await GetLatestFolderContainerTypeAndUpdateCacheAsync(folder, ct);
        }

        public async Task<FolderContainerType> GetLatestFolderContainerTypeAndUpdateCacheAsync(StorageFolder folder, CancellationToken ct)
        {
            var query = folder.CreateFileQueryWithOptions(new Windows.Storage.Search.QueryOptions(Windows.Storage.Search.CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.GetAllSupportedFileExtensions()) { FolderDepth = Windows.Storage.Search.FolderDepth.Shallow });
            var count = await query.GetItemCountAsync().AsTask(ct);
            if (count == 0)
            {
                _folderContainerTypeRepository.SetContainerType(folder.Path, FolderContainerType.Other);
                return FolderContainerType.Other;
            }

            var containerType = await query.ToAsyncEnumerable(ct).AllAsync(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.FileType), ct)
                ? FolderContainerType.OnlyImages
                : FolderContainerType.Other
                ;
            _folderContainerTypeRepository.SetContainerType(folder.Path, containerType);
            return containerType;
        }


        public void Delete(string path)
        {
            _folderContainerTypeRepository.DeleteItem(path);
        }

        public void DeleteAllUnderPath(string path)
        {
            _folderContainerTypeRepository.DeleteAllUnderPath(path);
        }

        internal void SetContainerType(StorageFolder folder, FolderContainerType folderContainerType)
        {
            _folderContainerTypeRepository.SetContainerType(folder.Path, folderContainerType);
        }

        public sealed class FolderContainerTypeRepository : LiteDBServiceBase<FolderContainerEntry>
        {
            public FolderContainerTypeRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
            }

            public void SetContainerType(string path, FolderContainerType type)
            {
                _collection.Upsert(new FolderContainerEntry() { Path = path, ContainerType = type });
            }

            public FolderContainerType? GetContainerType(string path)
            {
                return _collection.Exists(x => x.Path == path)
                    ? _collection.FindById(path).ContainerType
                    : default(FolderContainerType?)
                    ;
            }

            internal void DeleteAllUnderPath(string path)
            {
                _collection.DeleteMany(x => path.StartsWith(x.Path));
            }
        }

        public class FolderContainerEntry
        {
            [BsonId]
            public string Path { get; set; }

            [BsonField]
            public FolderContainerType ContainerType { get; set; }
        }

    }
}
