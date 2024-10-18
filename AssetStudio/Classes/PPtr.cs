using System;

namespace AssetStudio
{
    public sealed class PPtr<T> where T : Object
    {
        public int m_FileID;
        public long m_PathID;

        private SerializedFile _assetsFile;
        private int _index = -2; //-2 - Prepare, -1 - Missing

        public PPtr(ObjectReader reader)
        {
            m_FileID = reader.ReadInt32();
            m_PathID = reader.m_Version < SerializedFileFormatVersion.Unknown_14 ? reader.ReadInt32() : reader.ReadInt64();
            _assetsFile = reader.assetsFile;
        }

        public PPtr() { }

        public void SetAssetsFile(SerializedFile assetsFile)
        {
            _assetsFile = assetsFile;
        }

        private bool TryGetAssetsFile(out SerializedFile result)
        {
            result = null;
            if (m_FileID == 0)
            {
                result = _assetsFile;
                return true;
            }

            if (m_FileID > 0 && m_FileID - 1 < _assetsFile.m_Externals.Count)
            {
                var assetsManager = _assetsFile.assetsManager;
                var assetsFileList = assetsManager.assetsFileList;
                var assetsFileIndexCache = assetsManager.assetsFileIndexCache;

                if (_index == -2)
                {
                    var m_External = _assetsFile.m_Externals[m_FileID - 1];
                    var name = m_External.fileName;
                    if (!assetsFileIndexCache.TryGetValue(name, out _index))
                    {
                        _index = assetsFileList.FindIndex(x => x.fileName.Equals(name, StringComparison.OrdinalIgnoreCase));
                        assetsFileIndexCache.Add(name, _index);
                    }
                }

                if (_index >= 0)
                {
                    result = assetsFileList[_index];
                    return true;
                }
            }

            return false;
        }

        public bool TryGet(out T result, SerializedFile assetsFile = null)
        {
            _assetsFile = _assetsFile ?? assetsFile;
            if (!IsNull && TryGetAssetsFile(out var sourceFile))
            {
                if (sourceFile.ObjectsDic.TryGetValue(m_PathID, out var obj))
                {
                    if (obj is T variable)
                    {
                        result = variable;
                        return true;
                    }
                }
            }

            result = null;
            return false;
        }

        public bool TryGet<T2>(out T2 result, SerializedFile assetsFile = null) where T2 : Object
        {
            _assetsFile = _assetsFile ?? assetsFile;
            if (!IsNull && TryGetAssetsFile(out var sourceFile))
            {
                if (sourceFile.ObjectsDic.TryGetValue(m_PathID, out var obj))
                {
                    if (obj is T2 variable)
                    {
                        result = variable;
                        return true;
                    }
                }
            }

            result = null;
            return false;
        }

        public void Set(T m_Object)
        {
            var name = m_Object.assetsFile.fileName;
            if (string.Equals(_assetsFile.fileName, name, StringComparison.OrdinalIgnoreCase))
            {
                m_FileID = 0;
            }
            else
            {
                m_FileID = _assetsFile.m_Externals.FindIndex(x => string.Equals(x.fileName, name, StringComparison.OrdinalIgnoreCase));
                if (m_FileID == -1)
                {
                    _assetsFile.m_Externals.Add(new FileIdentifier
                    {
                        fileName = m_Object.assetsFile.fileName
                    });
                    m_FileID = _assetsFile.m_Externals.Count;
                }
                else
                {
                    m_FileID += 1;
                }
            }

            var assetsManager = _assetsFile.assetsManager;
            var assetsFileList = assetsManager.assetsFileList;
            var assetsFileIndexCache = assetsManager.assetsFileIndexCache;

            if (!assetsFileIndexCache.TryGetValue(name, out _index))
            {
                _index = assetsFileList.FindIndex(x => x.fileName.Equals(name, StringComparison.OrdinalIgnoreCase));
                assetsFileIndexCache.Add(name, _index);
            }

            m_PathID = m_Object.m_PathID;
        }

        public bool IsNull => m_PathID == 0 || m_FileID < 0;
    }
}
