﻿using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.FolderItemListing
{
    public record FolderAndArchiveDisplaySettingEntry
    {
        [BsonId]
        public string Path { get; init; }

        public FileSortType Sort { get; init; }

        public bool IsTitleDigitInterpolation { get; init; }
    }

    public record FolderAndArchiveChildFileDisplaySettingEntry
    {
        [BsonId]
        public string Path { get; init; }

        public FileSortType? ChildItemDefaultSort { get; init; }
    }

    public record FileDisplaySettingEntry
    {
        [BsonId]
        public string Path { get; init; }


        public FileSortType Sort { get; init; }

        public bool IsTitleDigitInterpolation { get; init; }
    }

    public sealed class DisplaySettingsByPathRepository
    {
        public sealed class InternalFolderAndArchiveDisplaySettingsByPathRepository : LiteDBServiceBase<FolderAndArchiveDisplaySettingEntry>
        {
            public InternalFolderAndArchiveDisplaySettingsByPathRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
            }

            public FolderAndArchiveDisplaySettingEntry FindById(string path)
            {
                return _collection.FindById(path);
            }

            public int DeleteUnderPath(string path)
            {
                return _collection.DeleteMany(x => x.Path.StartsWith(path));
            }

            internal void FolderChanged(string oldPath, string newPath)
            {
                var entries = _collection.Find(x => x.Path.StartsWith(oldPath)).ToList();
                foreach (var entry in entries)
                {
                    var newEntry = entry with { Path = entry.Path.Replace(oldPath, newPath) };
                    _collection.Update(newEntry);
                    Debug.WriteLine($"FnADisplaySettings path {entry.Path} ===> {newEntry.Path}");
                }
            }
        }

        public sealed class InternalFolderAndArchiveChildFileDisplaySettingsByPathRepository : LiteDBServiceBase<FolderAndArchiveChildFileDisplaySettingEntry>
        {
            public InternalFolderAndArchiveChildFileDisplaySettingsByPathRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
            }

            public FolderAndArchiveChildFileDisplaySettingEntry FindById(string path)
            {
                return _collection.FindById(path);
            }

            public int DeleteUnderPath(string path)
            {
                return _collection.DeleteMany(x => x.Path.StartsWith(path));
            }

            internal void FolderChanged(string oldPath, string newPath)
            {
                var entries = _collection.Find(x => x.Path.StartsWith(oldPath)).ToList();
                foreach (var entry in entries)
                {
                    var newEntry = entry with { Path = entry.Path.Replace(oldPath, newPath) };
                    _collection.Update(newEntry);
                    Debug.WriteLine($"FnAChildFileDisplaySettings path {entry.Path} ===> {newEntry.Path}");
                }
            }
        }


        private readonly InternalFolderAndArchiveDisplaySettingsByPathRepository _internalFolderAndArchiveRepository;
        private readonly InternalFolderAndArchiveChildFileDisplaySettingsByPathRepository _internalChildFileRepository;

        public DisplaySettingsByPathRepository(
            InternalFolderAndArchiveDisplaySettingsByPathRepository folderAndArchiveRepository,
            InternalFolderAndArchiveChildFileDisplaySettingsByPathRepository childFileRepository
            )
        {
            _internalFolderAndArchiveRepository = folderAndArchiveRepository;
            _internalChildFileRepository = childFileRepository;
        }

        public FolderAndArchiveDisplaySettingEntry GetFolderAndArchiveSettings(string path)
        {
            return _internalFolderAndArchiveRepository.FindById(path);
        }

        public void SetFolderAndArchiveSettings(string path, FileSortType sortType, bool withTitleDigitInterpolation)
        {
            _internalFolderAndArchiveRepository.UpdateItem(new FolderAndArchiveDisplaySettingEntry() 
            {
                Path =  path,
                Sort = sortType,
                IsTitleDigitInterpolation = withTitleDigitInterpolation
            });
        }

        public void ClearFolderAndArchiveSettings(string path)
        {
            _internalFolderAndArchiveRepository.DeleteUnderPath(path);
        }


        public FileSortType? GetFileParentSettings(string path)
        {
            return _internalChildFileRepository.FindById(path)?.ChildItemDefaultSort;
        }

        public FolderAndArchiveChildFileDisplaySettingEntry GetFileParentSettingsUpStreamToRoot(string path)
        {
            while (!string.IsNullOrEmpty(path))
            {
                if (_internalChildFileRepository.FindById(path) is not null and var entry)
                {
                    return entry;
                }

                path = Path.GetDirectoryName(path);
            }

            return null;
        }


        public void SetFileParentSettings(string path, FileSortType? sort)
        {
            _internalChildFileRepository.UpdateItem(new FolderAndArchiveChildFileDisplaySettingEntry()
            {
                Path = path,
                ChildItemDefaultSort = sort,
            });
        }

        public void Delete(string path)
        {
            _internalFolderAndArchiveRepository.DeleteItem(path);
            _internalChildFileRepository.DeleteItem(path);
        }


        public void DeleteUnderPath(string path)
        {
            _internalFolderAndArchiveRepository.DeleteUnderPath(path);
            _internalChildFileRepository.DeleteUnderPath(path);
        }

        public void FolderChanged(string oldPath, string newPath)
        {
            _internalFolderAndArchiveRepository.FolderChanged(oldPath, newPath);
            _internalChildFileRepository.FolderChanged(oldPath, newPath);
        }
    }
}
